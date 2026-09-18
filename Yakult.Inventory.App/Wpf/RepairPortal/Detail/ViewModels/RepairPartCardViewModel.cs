using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels
{
    /// <summary>Backs one RepairPartCard: owns lazy GetPartDetailAsync (first expand only) and
    /// its own note/evidence/status/delete commands, keeping RepairPartCard.xaml.cs thin — matches
    /// the pattern of the parent Detail VM owning state and the View just binding to it. File
    /// pickers/confirmations are bridged to the View via events since Application.Current is
    /// always null in this hosted-WPF app.</summary>
    public sealed class RepairPartCardViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;

        public static readonly List<string> StatusOptions = new List<string>
        {
            "WaitingDiagnosis", "Diagnosing", "Repairing", "WaitingParts", "Testing", "Repaired", "CannotRepair"
        };

        private RepairPart _part;
        public RepairPart Part
        {
            get => _part;
            private set
            {
                if (SetField(ref _part, value))
                {
                    // SetField only raises PropertyChanged("Part") — every property below is
                    // computed from it (Part.Status, Part.Severity, ...) but WPF has no way to know
                    // that without being told explicitly. Without this, the status badge and the
                    // "current status" highlight on the Change Status buttons kept showing
                    // whatever Part looked like at construction time, never refreshing after a
                    // save or the parent's periodic summary refresh.
                    OnPropertyChanged(nameof(RepairPartId));
                    OnPropertyChanged(nameof(PartDisplayName));
                    OnPropertyChanged(nameof(ProblemDescription));
                    OnPropertyChanged(nameof(Status));
                    OnPropertyChanged(nameof(Severity));
                    OnPropertyChanged(nameof(ImageCount));
                    OnPropertyChanged(nameof(VideoCount));
                    OnPropertyChanged(nameof(DocumentCount));
                    OnPropertyChanged(nameof(NoteCount));
                    OnPropertyChanged(nameof(HasUnsavedStatus));

                    // Reset the pending selection to match the server's current status whenever the
                    // Part is (re)loaded — e.g. on first construction or after the parent's periodic
                    // summary refresh — so an in-progress-but-unsaved pick isn't silently clobbered
                    // mid-edit, but a fresh card starts pointed at the real status.
                    if (!_hasPendingSelection)
                        PendingStatus = value?.Status;
                }
            }
        }

        private bool _hasPendingSelection;
        private string _pendingStatus;
        /// <summary>Status the technician has clicked but not yet saved — Change Status buttons
        /// only update this locally; nothing is sent to the server until Save is clicked. Prevents
        /// stray/exploratory clicks on the status row from silently changing the real status.</summary>
        public string PendingStatus
        {
            get => _pendingStatus;
            set
            {
                if (SetField(ref _pendingStatus, value))
                {
                    _hasPendingSelection = !string.Equals(value, Part?.Status, StringComparison.OrdinalIgnoreCase);
                    OnPropertyChanged(nameof(HasUnsavedStatus));
                    SaveStatusCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>True when PendingStatus differs from the saved Status — gates the Save button.</summary>
        public bool HasUnsavedStatus => !string.Equals(PendingStatus, Status, StringComparison.OrdinalIgnoreCase);

        public int RepairPartId => Part.RepairPartId;
        public string PartDisplayName => Part.PartDisplayName;
        public string ProblemDescription => Part.ProblemDescription;
        public string Status => Part.Status;
        public string Severity => Part.Severity;
        public int ImageCount => Part.ImageCount;
        public int VideoCount => Part.VideoCount;
        public int DocumentCount => Part.DocumentCount;
        public int NoteCount => Part.NoteCount;

        private bool _isExpanded;
        public bool IsExpanded { get => _isExpanded; private set => SetField(ref _isExpanded, value); }

        private bool _isLoadingDetail;
        public bool IsLoadingDetail { get => _isLoadingDetail; private set => SetField(ref _isLoadingDetail, value); }

        private bool _detailLoaded;

        public ObservableCollection<RepairPartNoteItem> DiagnosticNotes { get; } = new ObservableCollection<RepairPartNoteItem>();
        public ObservableCollection<RepairPartNoteItem> RepairNotes { get; } = new ObservableCollection<RepairPartNoteItem>();
        public ObservableCollection<PartAttachmentDisplayItem> Attachments { get; } = new ObservableCollection<PartAttachmentDisplayItem>();
        public ObservableCollection<RepairPartHistoryItem> History { get; } = new ObservableCollection<RepairPartHistoryItem>();

        private string _newDiagnosticNoteText = string.Empty;
        public string NewDiagnosticNoteText { get => _newDiagnosticNoteText; set => SetField(ref _newDiagnosticNoteText, value); }

        private string _newRepairNoteText = string.Empty;
        public string NewRepairNoteText { get => _newRepairNoteText; set => SetField(ref _newRepairNoteText, value); }

        /// <summary>Raised after any mutation (status change, note, evidence, delete) so the
        /// parent Detail VM can refresh the ticket header — the roll-up stored proc may have
        /// changed RepairTicket.Status server-side.</summary>
        public event Action PartChanged;
        /// <summary>Raised after a successful delete so the parent removes this card.</summary>
        public event Action<RepairPartCardViewModel> Deleted;

        public event Action<string, string> RequestError;
        public event Func<string, string, bool> RequestConfirm;
        public event Func<List<string>> RequestFilePicker;
        /// <summary>Raised when a Part status transition targets a terminal status (Repaired /
        /// CannotRepair) — the View shows CompletionDatePromptForm and returns the chosen date, or
        /// null if the technician cancelled (in which case the status change is aborted).</summary>
        public event Func<string, DateTime?> RequestCompletionDate;

        public RelayCommand ToggleExpandCommand { get; }
        public RelayCommand AddDiagnosticNoteCommand { get; }
        public RelayCommand AddRepairNoteCommand { get; }
        public RelayCommand UploadEvidenceCommand { get; }
        /// <summary>Clicking a Change Status button only updates PendingStatus locally — nothing is
        /// sent to the server until SaveStatusCommand runs.</summary>
        public RelayCommand<string> SelectStatusCommand { get; }
        public RelayCommand SaveStatusCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand DeleteSelectedAttachmentsCommand { get; }

        /// <summary>True when at least one Evidence thumbnail's checkbox is checked — gates the
        /// "Delete Selected" button.</summary>
        public bool HasSelectedAttachments => Attachments.Any(a => a.IsSelected);

        private void AttachmentSelection_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(PartAttachmentDisplayItem.IsSelected)) return;
            OnPropertyChanged(nameof(HasSelectedAttachments));
            DeleteSelectedAttachmentsCommand.RaiseCanExecuteChanged();
        }

        public RepairPartCardViewModel(RepairPart part, IRepairTicketRepository repository)
        {
            _part = part;
            _pendingStatus = part?.Status;
            _repository = repository ?? new RepairTicketRepository();

            ToggleExpandCommand = new RelayCommand(async () => await ToggleExpandAsync());
            AddDiagnosticNoteCommand = new RelayCommand(async () => await AddNoteAsync("Diagnostic"));
            AddRepairNoteCommand = new RelayCommand(async () => await AddNoteAsync("Repair"));
            UploadEvidenceCommand = new RelayCommand(async () => await UploadEvidenceViaPickerAsync());
            SelectStatusCommand = new RelayCommand<string>(status => PendingStatus = status);
            SaveStatusCommand = new RelayCommand(async () => await SaveStatusAsync(), () => HasUnsavedStatus);
            DeleteCommand = new RelayCommand(async () => await DeleteAsync());
            DeleteSelectedAttachmentsCommand = new RelayCommand(async () => await DeleteSelectedAttachmentsAsync(), () => HasSelectedAttachments);
        }

        private async Task DeleteSelectedAttachmentsAsync()
        {
            var selected = Attachments.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0) return;

            var confirmed = RequestConfirm?.Invoke(
                "Delete Evidence",
                $"Permanently delete {selected.Count} selected file{(selected.Count == 1 ? "" : "s")}? This cannot be undone.") ?? false;
            if (!confirmed) return;

            try
            {
                var userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null;
                foreach (var item in selected)
                    await _repository.DeletePartAttachmentAsync(item.AttachmentId, userId);

                _detailLoaded = false;
                await LoadDetailAsync();
                IsExpanded = true;
                PartChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Delete Failed", "Failed to delete evidence: " + ex.Message);
            }
        }

        private async Task ToggleExpandAsync()
        {
            IsExpanded = !IsExpanded;
            if (IsExpanded && !_detailLoaded)
                await LoadDetailAsync();
        }

        private async Task LoadDetailAsync()
        {
            IsLoadingDetail = true;
            try
            {
                var detail = await _repository.GetPartDetailAsync(RepairPartId);
                if (detail == null) return;

                DiagnosticNotes.Clear();
                foreach (var n in detail.DiagnosticNotes) DiagnosticNotes.Add(n);

                RepairNotes.Clear();
                foreach (var n in detail.RepairNotes) RepairNotes.Add(n);

                // Only rebuild Attachments if the set actually changed — this method re-runs after
                // every status change (not just evidence uploads), and rebuilding unconditionally
                // cleared the carousel and re-fetched every image's bytes from the DB each time,
                // causing a visible flash/reload for images that hadn't changed at all.
                var newIds = new HashSet<int>(detail.Attachments.Select(a => a.PartAttachmentId));
                var currentIds = new HashSet<int>(Attachments.Select(a => a.AttachmentId));
                if (!newIds.SetEquals(currentIds))
                {
                    Attachments.Clear();
                    foreach (var a in detail.Attachments)
                    {
                        var item = new PartAttachmentDisplayItem(a);
                        item.PropertyChanged += AttachmentSelection_PropertyChanged;
                        Attachments.Add(item);
                    }

                    // Fetch bytes for every attachment (not just images) so videos have something
                    // to actually play in the fullscreen viewer, and it can navigate the whole
                    // gallery without an extra round-trip per item.
                    foreach (var item in Attachments)
                        _ = LoadAttachmentBytesAsync(item);
                }

                History.Clear();
                foreach (var h in detail.History) History.Add(h);

                _detailLoaded = true;
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Load Failed", "Failed to load part detail: " + ex.Message);
            }
            finally
            {
                IsLoadingDetail = false;
            }
        }

        private async Task LoadAttachmentBytesAsync(PartAttachmentDisplayItem item)
        {
            try
            {
                var full = await _repository.GetPartAttachmentAsync(item.AttachmentId);
                if (full != null) item.Bytes = full.FileBytes;
            }
            catch
            {
                // Non-critical.
            }
        }

        /// <summary>Called by the parent after it reloads Parts from the server, so this card's
        /// collapsed summary (status/counts) stays in sync without a full detail reload.</summary>
        public void UpdateSummary(RepairPart refreshed)
        {
            if (refreshed != null) Part = refreshed;
        }

        private async Task AddNoteAsync(string noteType)
        {
            var text = noteType == "Repair" ? NewRepairNoteText : NewDiagnosticNoteText;
            if (string.IsNullOrWhiteSpace(text)) return;

            try
            {
                await _repository.AddPartNoteAsync(RepairPartId, noteType, text.Trim(),
                    AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null, AppSession.CurrentEmployeeId);

                if (noteType == "Repair") NewRepairNoteText = string.Empty;
                else NewDiagnosticNoteText = string.Empty;

                _detailLoaded = false;
                await LoadDetailAsync();
                IsExpanded = true;
                PartChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Add Note Failed", ex.Message);
            }
        }

        private async Task UploadEvidenceViaPickerAsync()
        {
            var paths = RequestFilePicker?.Invoke();
            if (paths == null || paths.Count == 0) return;

            await AddAttachmentsAsync(paths);
        }

        /// <summary>Matches the mobile app's own per-file upload limit (see YakultScanner's
        /// MAX_UPLOAD_BYTES) — keeps evidence files a consistent, sane size regardless of which
        /// side they were attached from. A run of oversized video files in particular was a
        /// contributing factor to a WPF render-thread crash when browsing the Evidence Gallery.</summary>
        private const long MaxAttachmentBytes = 20L * 1024 * 1024;

        /// <summary>Total evidence (whole-equipment + every Part combined) a single ticket may
        /// accumulate — separate from, and on top of, the 20 MB per-file cap above. Prevents a
        /// ticket from quietly growing to gigabytes of attachments over its lifetime.</summary>
        private const long MaxTicketEvidenceBytes = 50L * 1024 * 1024;

        public async Task AddAttachmentsAsync(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return;

            try
            {
                var skipped = new List<string>();
                var skippedForTicketCap = new List<string>();
                var runningTotal = await _repository.GetTotalEvidenceSizeBytesAsync(Part.RepairTicketId);

                foreach (var path in filePaths)
                {
                    if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;

                    var fileSize = new FileInfo(path).Length;
                    if (fileSize > MaxAttachmentBytes)
                    {
                        skipped.Add(Path.GetFileName(path));
                        continue;
                    }

                    if (runningTotal + fileSize > MaxTicketEvidenceBytes)
                    {
                        skippedForTicketCap.Add(Path.GetFileName(path));
                        continue;
                    }

                    var bytes = File.ReadAllBytes(path);
                    var fileName = Path.GetFileName(path);
                    var (type, mime) = InferAttachmentType(path);

                    await _repository.AddPartAttachmentAsync(RepairPartId, type, fileName, mime, bytes,
                        AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null);
                    runningTotal += fileSize;
                }

                _detailLoaded = false;
                await LoadDetailAsync();
                PartChanged?.Invoke();

                if (skipped.Count > 0)
                    RequestError?.Invoke("Some Files Skipped",
                        $"{skipped.Count} file(s) over the 20 MB limit weren't uploaded:\n" + string.Join("\n", skipped));

                if (skippedForTicketCap.Count > 0)
                    RequestError?.Invoke("Ticket Evidence Limit Reached",
                        $"This ticket has reached its 50 MB total evidence limit. {skippedForTicketCap.Count} file(s) weren't uploaded:\n" + string.Join("\n", skippedForTicketCap));
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Upload Failed", "Failed to upload evidence: " + ex.Message);
            }
        }

        private static (string Type, string Mime) InferAttachmentType(string path)
        {
            var ext = Path.GetExtension(path)?.TrimStart('.').ToLowerInvariant() ?? string.Empty;

            switch (ext)
            {
                case "jpg": case "jpeg": return ("Image", "image/jpeg");
                case "png": return ("Image", "image/png");
                case "gif": return ("Image", "image/gif");
                case "bmp": return ("Image", "image/bmp");
                case "webp": return ("Image", "image/webp");
                case "mp4": return ("Video", "video/mp4");
                case "mov": return ("Video", "video/quicktime");
                case "avi": return ("Video", "video/x-msvideo");
                case "wmv": return ("Video", "video/x-ms-wmv");
                case "mkv": return ("Video", "video/x-matroska");
                case "pdf": return ("Document", "application/pdf");
                default: return ("Document", "application/octet-stream");
            }
        }

        private async Task SaveStatusAsync()
        {
            var status = PendingStatus;
            if (string.IsNullOrWhiteSpace(status) || status == Status) return;

            DateTime? completedDate = null;
            var isTerminal = string.Equals(status, "Repaired", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(status, "CannotRepair", StringComparison.OrdinalIgnoreCase);
            if (isTerminal)
            {
                var chosen = RequestCompletionDate?.Invoke(status);
                if (chosen == null)
                    return; // Cancelled — abort the status change rather than silently using "now".

                // CompletionDatePromptForm's DateTimePicker.Value is local wall-clock time, but
                // every timestamp column in this schema (including CompletedAt) is stored as UTC
                // (SYSUTCDATETIME()) and read back via ToManilaLocal — passing the local value
                // through unconverted double-applies the Manila offset on display.
                completedDate = DateTime.SpecifyKind(chosen.Value, DateTimeKind.Local).ToUniversalTime();
            }

            try
            {
                await _repository.SetPartStatusAsync(RepairPartId, status,
                    AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null, null, completedDate);

                // Saved — the pending pick is now the real status. Clear the "unsaved" flag before
                // the refresh below so a subsequent Part reassignment doesn't fight it.
                _hasPendingSelection = false;

                _detailLoaded = false;
                await LoadDetailAsync();
                PartChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Status Update Failed", ex.Message);
            }
        }

        private async Task DeleteAsync()
        {
            var confirmed = RequestConfirm?.Invoke("Delete Part",
                $"Delete {PartDisplayName}? This removes its notes, evidence, and history permanently.") ?? false;
            if (!confirmed) return;

            try
            {
                await _repository.DeletePartAsync(RepairPartId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null);
                Deleted?.Invoke(this);
                PartChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Delete Failed", ex.Message);
            }
        }
    }
}
