using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels
{
    /// <summary>Lightweight wrapper around RepairTicketAttachmentSummary that lazily carries the
    /// full byte[] once loaded, so ImageCarouselControl can bind to it without pulling every
    /// attachment's bytes eagerly for non-image types.</summary>
    public sealed class AttachmentDisplayItem : ViewModelBase
    {
        public RepairTicketAttachmentSummary Summary { get; }
        public int AttachmentId => Summary.AttachmentId;
        public string AttachmentType => Summary.AttachmentType;
        public string FileName => Summary.FileName;
        public string MimeType => Summary.MimeType;
        public DateTime UploadedAt => Summary.UploadedAt;
        public string UploadedByName => Summary.UploadedByName;
        public bool IsImage => string.Equals(AttachmentType, "Image", StringComparison.OrdinalIgnoreCase);
        public bool IsVideo => string.Equals(AttachmentType, "Video", StringComparison.OrdinalIgnoreCase);

        private byte[] _bytes;
        public byte[] Bytes { get => _bytes; set => SetField(ref _bytes, value); }

        private bool _isSelected;
        /// <summary>Multi-select checkbox state for bulk deletion from the Evidence carousel.</summary>
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public AttachmentDisplayItem(RepairTicketAttachmentSummary summary)
        {
            Summary = summary;
        }
    }

    /// <summary>Loads full ticket detail (header + attachments + notes + history) and exposes
    /// commands for the status-transition buttons, Add Note, and evidence upload. File
    /// pickers/MessageBox confirmations are bridged to the View via events, since
    /// Application.Current is always null in this hosted-WPF app.</summary>
    public sealed class RepairTicketDetailViewModel : ViewModelBase
    {
        private readonly IRepairTicketRepository _repository;
        public int RepairTicketId { get; }

        public ObservableCollection<AttachmentDisplayItem> Attachments { get; } = new ObservableCollection<AttachmentDisplayItem>();
        public ObservableCollection<RepairTicketNoteItem> Notes { get; } = new ObservableCollection<RepairTicketNoteItem>();
        public ObservableCollection<RepairTicketHistoryItem> History { get; } = new ObservableCollection<RepairTicketHistoryItem>();

        public ObservableCollection<ObservationCardViewModel> Observations { get; } = new ObservableCollection<ObservationCardViewModel>();
        public ObservableCollection<RepairPartCardViewModel> Parts { get; } = new ObservableCollection<RepairPartCardViewModel>();
        public ObservableCollection<AttachmentGalleryItem> EvidenceGallery { get; } = new ObservableCollection<AttachmentGalleryItem>();

        private RepairTicketDetail _detail;
        public RepairTicketDetail Detail { get => _detail; private set => SetField(ref _detail, value); }

        private string _evidenceUsageText = string.Empty;
        /// <summary>"X MB used of Y MB (Z MB remaining)" for the 50 MB per-ticket evidence cap —
        /// shown under the Part Diagnostics header so uploads never silently hit the wall.</summary>
        public string EvidenceUsageText { get => _evidenceUsageText; private set => SetField(ref _evidenceUsageText, value); }

        private bool _evidenceUsageNearLimit;
        public bool EvidenceUsageNearLimit { get => _evidenceUsageNearLimit; private set => SetField(ref _evidenceUsageNearLimit, value); }

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        private string _newNoteText = string.Empty;
        public string NewNoteText { get => _newNoteText; set => SetField(ref _newNoteText, value); }

        private string _newObservationText = string.Empty;
        public string NewObservationText { get => _newObservationText; set => SetField(ref _newObservationText, value); }

        private bool _allPartsTerminal;
        /// <summary>True when Parts.Count > 0 and every part is Repaired or CannotRepair —
        /// gates the Repair Conclusion section's Save button.</summary>
        public bool AllPartsTerminal { get => _allPartsTerminal; private set => SetField(ref _allPartsTerminal, value); }

        private string _rootCauseInput = string.Empty;
        public string RootCauseInput { get => _rootCauseInput; set => SetField(ref _rootCauseInput, value); }

        private string _workPerformedInput = string.Empty;
        public string WorkPerformedInput { get => _workPerformedInput; set => SetField(ref _workPerformedInput, value); }

        private string _finalOutcomeInput = string.Empty;
        public string FinalOutcomeInput { get => _finalOutcomeInput; set => SetField(ref _finalOutcomeInput, value); }

        private string _recommendationsInput = string.Empty;
        public string RecommendationsInput { get => _recommendationsInput; set => SetField(ref _recommendationsInput, value); }

        private RepairConclusion _conclusion;
        public RepairConclusion Conclusion { get => _conclusion; private set => SetField(ref _conclusion, value); }

        // ── Unrepairable disposition (Discard / Replace) ─────────────────────
        private bool _isUnrepairable;
        /// <summary>Gates the Disposition card's visibility — recomputed on every LoadAsync().</summary>
        public bool IsUnrepairable { get => _isUnrepairable; private set => SetField(ref _isUnrepairable, value); }

        private string _dispositionSummaryText = string.Empty;
        public string DispositionSummaryText { get => _dispositionSummaryText; private set => SetField(ref _dispositionSummaryText, value); }

        // ── Spare item loaner (Repairing status) ─────────────────────────────
        private bool _isRepairing;
        /// <summary>Gates the Spare Item card's visibility — recomputed on every LoadAsync().</summary>
        public bool IsRepairing { get => _isRepairing; private set => SetField(ref _isRepairing, value); }

        private bool _hasActiveSpare;
        public bool HasActiveSpare { get => _hasActiveSpare; private set => SetField(ref _hasActiveSpare, value); }

        private string _spareSummaryText = string.Empty;
        public string SpareSummaryText { get => _spareSummaryText; private set => SetField(ref _spareSummaryText, value); }

        // ── Repaired By (multi-select, IT Dept. only) ────────────────────────
        public ObservableCollection<OrgLookupOption> RepairedByCandidates { get; } = new ObservableCollection<OrgLookupOption>();

        private List<int> _selectedRepairedByEmpIds = new List<int>();

        private string _repairedByDisplayText = "";
        /// <summary>Read-only comma-joined names shown next to the "Repaired By..." button —
        /// updated whenever the picker dialog is confirmed or a saved Conclusion is loaded.</summary>
        public string RepairedByDisplayText { get => _repairedByDisplayText; private set => SetField(ref _repairedByDisplayText, value); }

        // ── 3rd Party Handover (vendor, optional) ────────────────────────────
        public ObservableCollection<Yakult.Inventory.App.Pages.VendorDto> VendorOptions { get; } = new ObservableCollection<Yakult.Inventory.App.Pages.VendorDto>();

        private Yakult.Inventory.App.Pages.VendorDto _selectedVendor;
        /// <summary>Null = repaired in-house, no vendor involved. The View adds a leading "null"
        /// item to VendorOptions for that choice — see LoadVendorOptionsAsync.</summary>
        public Yakult.Inventory.App.Pages.VendorDto SelectedVendor { get => _selectedVendor; set => SetField(ref _selectedVendor, value); }

        // ── Requested By inline edit ─────────────────────────────────────────
        public ObservableCollection<string> RequestedByTypeOptions { get; } = new ObservableCollection<string> { "Department", "Employee" };
        public ObservableCollection<OrgLookupOption> DepartmentOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> EmployeeOptions { get; } = new ObservableCollection<OrgLookupOption>();

        /// <summary>Company -> Branch -> Department cascade for editing a Department-mode request
        /// — see NewRepairTicketViewModel's equivalent RequestedCompanyOptions/RequestedBranchOptions
        /// for the same cascade concept (this VM keeps the existing "Edit..." naming convention).</summary>
        public ObservableCollection<OrgLookupOption> EditRequestedCompanyOptions { get; } = new ObservableCollection<OrgLookupOption>();
        public ObservableCollection<OrgLookupOption> EditRequestedBranchOptions { get; } = new ObservableCollection<OrgLookupOption>();

        private bool _isEditingRequestedBy;
        public bool IsEditingRequestedBy { get => _isEditingRequestedBy; private set => SetField(ref _isEditingRequestedBy, value); }

        private string _editRequestedByType = "Department";
        public string EditRequestedByType
        {
            get => _editRequestedByType;
            set
            {
                if (SetField(ref _editRequestedByType, value))
                {
                    OnPropertyChanged(nameof(IsEditRequestedByDepartment));
                    OnPropertyChanged(nameof(IsEditRequestedByEmployee));
                }
            }
        }

        public bool IsEditRequestedByDepartment => string.Equals(EditRequestedByType, "Department", StringComparison.OrdinalIgnoreCase);
        public bool IsEditRequestedByEmployee => string.Equals(EditRequestedByType, "Employee", StringComparison.OrdinalIgnoreCase);

        /// <summary>Set while BeginEditRequestedByAsync is re-applying the ticket's saved
        /// Company/Branch/Department onto the freshly-loaded cascade options, so the property
        /// setters below don't kick off a second, overlapping cascade reload on top of the explicit
        /// awaited ones already sequenced there.</summary>
        private bool _suppressEditRequestedByCascade;

        private OrgLookupOption _selectedEditRequestedCompany;
        public OrgLookupOption SelectedEditRequestedCompany
        {
            get => _selectedEditRequestedCompany;
            set
            {
                if (SetField(ref _selectedEditRequestedCompany, value) && !_suppressEditRequestedByCascade)
                    _ = ReloadEditRequestedBranchAndDeptAsync();
            }
        }

        private OrgLookupOption _selectedEditRequestedBranch;
        public OrgLookupOption SelectedEditRequestedBranch
        {
            get => _selectedEditRequestedBranch;
            set
            {
                if (SetField(ref _selectedEditRequestedBranch, value) && !_suppressEditRequestedByCascade)
                    _ = ReloadEditRequestedDeptAsync();
            }
        }

        private OrgLookupOption _selectedRequestedDept;
        public OrgLookupOption SelectedRequestedDept { get => _selectedRequestedDept; set => SetField(ref _selectedRequestedDept, value); }

        private OrgLookupOption _selectedRequestedEmployee;
        public OrgLookupOption SelectedRequestedEmployee { get => _selectedRequestedEmployee; set => SetField(ref _selectedRequestedEmployee, value); }

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Func<System.Collections.Generic.List<string>> RequestFilePicker;
        public event Func<string, string, bool> RequestConfirm;
        /// <summary>Raised when a Ticket status transition targets a terminal status
        /// (Completed/Unrepairable) — the View shows CompletionDatePromptForm and returns the
        /// chosen date, or null if the technician cancelled (in which case the status change is
        /// aborted, not silently defaulted to "now").</summary>
        public event Func<string, DateTime?> RequestCompletionDate;
        /// <summary>Raised when "+ Add Part" is clicked — the View owns the WPF Window and opens
        /// AddPartDialog, then calls LoadAsync() again once a part is created.</summary>
        public event Action RequestAddPart;
        /// <summary>Raised when "Repaired By..." is clicked — the View shows SelectRepairedByDialog
        /// (candidates, currently-selected ids in) and returns the confirmed id list, or null if
        /// the technician cancelled (selection stays unchanged).</summary>
        public event Func<List<OrgLookupOption>, List<int>, List<int>> RequestSelectRepairedBy;
        public event Action DetailChanged;
        /// <summary>Raised after the ticket is successfully deleted so the View can close this
        /// window (there's nothing left to display).</summary>
        public event Action RequestCloseWindow;
        /// <summary>Raised when "Replace Item" is clicked — the View shows ReplacementItemPickerWindow
        /// (blank search, browsing same-category unassigned stock by default) and returns the chosen
        /// ItemId, or null if the technician cancelled (in which case the disposition change is
        /// aborted, same "null return aborts" convention as RequestCompletionDate).</summary>
        public event Func<int?> RequestReplacementItemPicker;
        /// <summary>Raised when "Assign Spare" is clicked — the View shows the existing
        /// WpfBorrowableItemsPickerDialog (Wpf\BorrowItems\) and returns the picked item's serial
        /// number, or null if the technician cancelled.</summary>
        public event Func<string> RequestSpareItemPicker;

        public RelayCommand AddNoteCommand { get; }
        public RelayCommand UploadEvidenceCommand { get; }
        public RelayCommand<string> SetStatusCommand { get; }
        public RelayCommand PrintReportCommand { get; }
        /// <summary>Full reload — e.g. to pick up evidence uploaded from the mobile app since this
        /// window was opened, without having to close and reopen it.</summary>
        public RelayCommand RefreshCommand { get; }
        public RelayCommand AddObservationCommand { get; }
        public RelayCommand AddPartCommand { get; }
        public RelayCommand SaveConclusionCommand { get; }
        public RelayCommand SelectRepairedByCommand { get; }
        public RelayCommand EditRequestedByCommand { get; }
        public RelayCommand SaveRequestedByCommand { get; }
        public RelayCommand CancelEditRequestedByCommand { get; }
        public RelayCommand<string> SetEditRequestedByTypeCommand { get; }
        public RelayCommand DeleteTicketCommand { get; }
        public RelayCommand DeleteSelectedAttachmentsCommand { get; }
        public RelayCommand DiscardDispositionCommand { get; }
        public RelayCommand ReplaceDispositionCommand { get; }
        public RelayCommand UnlinkDispositionCommand { get; }
        public RelayCommand AssignSpareCommand { get; }
        public RelayCommand UnlinkSpareCommand { get; }

        /// <summary>True when at least one Evidence thumbnail's checkbox is checked — gates the
        /// "Delete Selected" button.</summary>
        public bool HasSelectedAttachments => Attachments.Any(a => a.IsSelected);

        private void AttachmentSelection_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(AttachmentDisplayItem.IsSelected)) return;
            OnPropertyChanged(nameof(HasSelectedAttachments));
            DeleteSelectedAttachmentsCommand.RaiseCanExecuteChanged();
        }

        public RepairTicketDetailViewModel(int repairTicketId, IRepairTicketRepository repository)
        {
            RepairTicketId = repairTicketId;
            _repository = repository ?? new RepairTicketRepository();

            AddNoteCommand = new RelayCommand(async () => await AddNoteAsync());
            UploadEvidenceCommand = new RelayCommand(async () => await UploadEvidenceViaPickerAsync());
            SetStatusCommand = new RelayCommand<string>(async status => await SetStatusAsync(status));
            PrintReportCommand = new RelayCommand(async () => await PrintReportAsync());
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            AddObservationCommand = new RelayCommand(async () => await AddObservationAsync());
            AddPartCommand = new RelayCommand(() => RequestAddPart?.Invoke());
            DeleteTicketCommand = new RelayCommand(async () => await DeleteTicketAsync());
            DeleteSelectedAttachmentsCommand = new RelayCommand(async () => await DeleteSelectedAttachmentsAsync(), () => HasSelectedAttachments);
            SaveConclusionCommand = new RelayCommand(async () => await SaveConclusionAsync(), () => AllPartsTerminal);
            SelectRepairedByCommand = new RelayCommand(SelectRepairedBy);
            EditRequestedByCommand = new RelayCommand(async () => await BeginEditRequestedByAsync());
            SaveRequestedByCommand = new RelayCommand(async () => await SaveRequestedByAsync());
            CancelEditRequestedByCommand = new RelayCommand(() => IsEditingRequestedBy = false);
            SetEditRequestedByTypeCommand = new RelayCommand<string>(type => EditRequestedByType = type);
            DiscardDispositionCommand = new RelayCommand(async () => await SetDispositionAsync("Discard"));
            ReplaceDispositionCommand = new RelayCommand(async () => await SetDispositionAsync("Replace"));
            UnlinkDispositionCommand = new RelayCommand(async () => await UnlinkDispositionAsync());
            AssignSpareCommand = new RelayCommand(async () => await AssignSpareAsync());
            UnlinkSpareCommand = new RelayCommand(async () => await UnlinkSpareAsync());

            _ = LoadAsync();
            _ = LoadRequestedByOptionsAsync();
            _ = LoadRepairedByAndVendorOptionsAsync();
        }

        private async Task LoadRequestedByOptionsAsync()
        {
            try
            {
                var companies = await _repository.GetCompanyOptionsAsync();
                EditRequestedCompanyOptions.Clear();
                foreach (var c in companies) EditRequestedCompanyOptions.Add(c);

                var emps = await _repository.GetTechnicianOptionsAsync();
                EmployeeOptions.Clear();
                foreach (var e in emps) EmployeeOptions.Add(e);
            }
            catch
            {
                // Non-critical — the dropdowns just stay empty if this fails.
            }
        }

        private static readonly Yakult.Inventory.App.Pages.VendorDto NoVendorOption =
            new Yakult.Inventory.App.Pages.VendorDto { VendorId = 0, VendorName = "— None (repaired in-house) —" };

        private async Task LoadRepairedByAndVendorOptionsAsync()
        {
            try
            {
                var candidates = await _repository.GetItDepartmentEmployeesAsync();
                RepairedByCandidates.Clear();
                foreach (var c in candidates) RepairedByCandidates.Add(c);

                var vendors = await new VendorRepository().GetAllVendorsAsync();
                VendorOptions.Clear();
                VendorOptions.Add(NoVendorOption);
                foreach (var v in vendors.Where(v => v.IsActive && !v.IsArchived))
                    VendorOptions.Add(v);

                // Re-apply the saved selection now that the option lists exist — LoadAsync() may
                // have already run and stashed a HandedOverToVendorId with nothing to select yet.
                if (Conclusion?.HandedOverToVendorId is int vendorId)
                    SelectedVendor = VendorOptions.FirstOrDefault(v => v.VendorId == vendorId) ?? NoVendorOption;
                else
                    SelectedVendor = NoVendorOption;
            }
            catch
            {
                // Non-critical — the picker/dropdown just stay empty if this fails.
            }
        }

        private void SelectRepairedBy()
        {
            var result = RequestSelectRepairedBy?.Invoke(RepairedByCandidates.ToList(), _selectedRepairedByEmpIds);
            if (result == null) return;   // cancelled

            _selectedRepairedByEmpIds = result;
            RepairedByDisplayText = _selectedRepairedByEmpIds.Count == 0
                ? ""
                : string.Join(", ", RepairedByCandidates.Where(c => _selectedRepairedByEmpIds.Contains(c.Id)).Select(c => c.Name));
        }

        /// <summary>Company changed: reload Branch options for the new Company (cascade step 1) and
        /// Department options for the new Company with no Branch filter yet, clearing any
        /// previously selected Branch/Department since they may no longer be valid under the new
        /// Company. Same cascade concept as NewRepairTicketViewModel.ReloadRequestedBranchAndDeptAsync.</summary>
        private async Task ReloadEditRequestedBranchAndDeptAsync()
        {
            SelectedEditRequestedBranch = null;
            SelectedRequestedDept = null;
            EditRequestedBranchOptions.Clear();
            DepartmentOptions.Clear();

            if (SelectedEditRequestedCompany == null) return;

            try
            {
                var branches = await _repository.GetBranchesForCompanyAsync(SelectedEditRequestedCompany.Id);
                foreach (var b in branches) EditRequestedBranchOptions.Add(b);

                var depts = await _repository.GetDepartmentsForCompanyBranchAsync(SelectedEditRequestedCompany.Id, null);
                foreach (var d in depts) DepartmentOptions.Add(d);
            }
            catch
            {
                // Non-critical — the dropdowns just stay empty if this fails.
            }
        }

        /// <summary>Branch changed: reload Department options narrowed to Company + Branch.</summary>
        private async Task ReloadEditRequestedDeptAsync()
        {
            SelectedRequestedDept = null;
            DepartmentOptions.Clear();

            if (SelectedEditRequestedCompany == null) return;

            try
            {
                var depts = await _repository.GetDepartmentsForCompanyBranchAsync(SelectedEditRequestedCompany.Id, SelectedEditRequestedBranch?.Id);
                foreach (var d in depts) DepartmentOptions.Add(d);
            }
            catch
            {
                // Non-critical — the dropdown just stays empty if this fails.
            }
        }

        // ── Requested By inline edit ─────────────────────────────────────────

        private async Task BeginEditRequestedByAsync()
        {
            EditRequestedByType = Detail?.RequestedByType ?? "Department";
            SelectedRequestedEmployee = Detail?.RequestedByEmpId.HasValue == true
                ? EmployeeOptions.FirstOrDefault(e => e.Id == Detail.RequestedByEmpId.Value) : null;

            if (IsEditRequestedByDepartment && Detail?.RequestedByComId.HasValue == true)
            {
                // Setting SelectedEditRequestedCompany triggers ReloadEditRequestedBranchAndDeptAsync,
                // which clears/reloads Branch + Department — await it, then re-apply the ticket's
                // current Branch/Department selection now that the cascaded lists are populated.
                var targetComId = Detail.RequestedByComId.Value;
                var targetBranchId = Detail.RequestedByBranchId;
                var targetDeptId = Detail.RequestedByDeptId;

                _suppressEditRequestedByCascade = true;
                try
                {
                    SelectedEditRequestedCompany = EditRequestedCompanyOptions.FirstOrDefault(c => c.Id == targetComId);
                    await ReloadEditRequestedBranchAndDeptAsync();

                    if (targetBranchId.HasValue)
                    {
                        SelectedEditRequestedBranch = EditRequestedBranchOptions.FirstOrDefault(b => b.Id == targetBranchId.Value);
                        await ReloadEditRequestedDeptAsync();
                    }

                    if (targetDeptId.HasValue)
                        SelectedRequestedDept = DepartmentOptions.FirstOrDefault(d => d.Id == targetDeptId.Value);
                }
                finally
                {
                    _suppressEditRequestedByCascade = false;
                }
            }
            else
            {
                SelectedEditRequestedCompany = null;
                SelectedEditRequestedBranch = null;
                SelectedRequestedDept = null;
            }

            IsEditingRequestedBy = true;
        }

        private async Task SaveRequestedByAsync()
        {
            if (IsEditRequestedByDepartment && SelectedEditRequestedCompany == null)
            {
                RequestError?.Invoke("Requested By Required", "Please select the company requesting this repair.");
                return;
            }

            if (IsEditRequestedByDepartment && SelectedRequestedDept == null)
            {
                RequestError?.Invoke("Requested By Required", "Please select the department requesting this repair.");
                return;
            }

            if (IsEditRequestedByEmployee && SelectedRequestedEmployee == null)
            {
                RequestError?.Invoke("Requested By Required", "Please select the employee requesting this repair.");
                return;
            }

            try
            {
                await _repository.UpdateRequestedByAsync(
                    RepairTicketId,
                    EditRequestedByType,
                    IsEditRequestedByDepartment ? SelectedRequestedDept?.Id : null,
                    IsEditRequestedByEmployee ? SelectedRequestedEmployee?.Id : null,
                    AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null,
                    IsEditRequestedByDepartment ? SelectedEditRequestedCompany?.Id : null,
                    IsEditRequestedByDepartment ? SelectedEditRequestedBranch?.Id : null);

                IsEditingRequestedBy = false;
                // RefreshAfterPartChangeAsync only re-syncs Status/CompletedAt/UpdatedAt — RequestedBy*
                // needs a full reload since it isn't part of that lightweight refresh's field set.
                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Save Failed", ex.Message);
            }
        }

        public async Task LoadAsync()
        {
            IsBusy = true;
            try
            {
                var detail = await _repository.GetTicketDetailAsync(RepairTicketId);
                if (detail == null)
                {
                    RequestError?.Invoke("Not Found", "This repair ticket could not be found.");
                    return;
                }

                // Pre-redesign tickets have a legacy flat Problem note but no Observations yet.
                // Lazily promote it into a real, editable Observation #1 the first time the
                // Detail page is opened, instead of showing it as separate read-only "legacy"
                // text forever.
                if (detail.Observations.Count == 0 && !string.IsNullOrWhiteSpace(detail.Problem))
                {
                    try
                    {
                        await _repository.AddObservationAsync(RepairTicketId, detail.Problem, null);
                        detail = await _repository.GetTicketDetailAsync(RepairTicketId) ?? detail;
                    }
                    catch
                    {
                        // Non-critical — the legacy Problem text is still shown as a fallback.
                    }
                }

                Detail = detail;

                Attachments.Clear();
                foreach (var a in detail.Attachments)
                {
                    var item = new AttachmentDisplayItem(a);
                    item.PropertyChanged += AttachmentSelection_PropertyChanged;
                    Attachments.Add(item);
                }

                Notes.Clear();
                foreach (var n in detail.Notes)
                    Notes.Add(n);

                History.Clear();
                foreach (var h in detail.History)
                    History.Add(h);

                // Lazily fetch bytes for every attachment (not just images) so videos have
                // something to actually play and the fullscreen viewer can navigate through the
                // whole gallery — including videos/documents — without an extra round-trip.
                foreach (var item in Attachments)
                    _ = LoadAttachmentBytesAsync(item);

                Observations.Clear();
                var orderedObservations = detail.Observations.OrderBy(x => x.SortOrder).ToList();
                for (var i = 0; i < orderedObservations.Count; i++)
                {
                    var card = BuildObservationCard(orderedObservations[i]);
                    card.CanMoveUp = i > 0;
                    card.CanMoveDown = i < orderedObservations.Count - 1;
                    Observations.Add(card);
                }

                Parts.Clear();
                foreach (var p in detail.Parts)
                    Parts.Add(BuildPartCard(p));

                Conclusion = detail.Conclusion;
                // Also true once the ticket has been auto-completed by a resolved disposition
                // (RepairTicketRepository.Disposition.cs's SetDispositionAsync flips Status to
                // "Completed" once Discard/Replace resolves) — the Disposition card, including
                // Unlink, must stay reachable even though Status no longer literally says
                // "Unrepairable".
                IsUnrepairable = string.Equals(detail.Status, "Unrepairable", StringComparison.OrdinalIgnoreCase)
                    || !string.IsNullOrEmpty(detail.Conclusion?.Disposition);
                DispositionSummaryText = BuildDispositionSummaryText(detail.Conclusion);

                IsRepairing = string.Equals(detail.Status, "Repairing", StringComparison.OrdinalIgnoreCase);
                await LoadActiveSpareAsync();

                RootCauseInput = detail.Conclusion?.RootCause ?? string.Empty;
                WorkPerformedInput = detail.Conclusion?.WorkPerformed ?? string.Empty;
                FinalOutcomeInput = detail.Conclusion?.FinalOutcome ?? string.Empty;
                RecommendationsInput = detail.Conclusion?.Recommendations ?? string.Empty;

                _selectedRepairedByEmpIds = detail.Conclusion?.RepairedByEmpIds != null
                    ? new List<int>(detail.Conclusion.RepairedByEmpIds)
                    : new List<int>();
                RepairedByDisplayText = detail.Conclusion?.RepairedByNames != null && detail.Conclusion.RepairedByNames.Count > 0
                    ? string.Join(", ", detail.Conclusion.RepairedByNames)
                    : "";
                // VendorOptions may not be loaded yet on the very first LoadAsync() (it runs
                // concurrently with LoadRepairedByAndVendorOptionsAsync) — that method re-applies
                // this selection itself once the option list exists, so a miss here is harmless.
                SelectedVendor = detail.Conclusion?.HandedOverToVendorId is int vId
                    ? (VendorOptions.FirstOrDefault(v => v.VendorId == vId) ?? NoVendorOption)
                    : NoVendorOption;

                RecomputeAllPartsTerminal();

                EvidenceGallery.Clear();
                try
                {
                    var gallery = await _repository.GetEvidenceGalleryAsync(RepairTicketId);
                    foreach (var g in gallery)
                        EvidenceGallery.Add(g);
                }
                catch
                {
                    // Non-critical — the aggregate gallery is a convenience view.
                }

                await RefreshEvidenceUsageAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Load Failed", "Failed to load ticket detail: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Re-sums the 50 MB per-ticket evidence cap and refreshes the display text under
        /// the Part Diagnostics header. Called after any load or upload, from either the
        /// ticket-level or Part-level attachment paths.</summary>
        private async Task RefreshEvidenceUsageAsync()
        {
            try
            {
                var used = await _repository.GetTotalEvidenceSizeBytesAsync(RepairTicketId);
                var usedMb = used / (1024.0 * 1024.0);
                var limitMb = MaxTicketEvidenceBytes / (1024.0 * 1024.0);
                var remainingMb = Math.Max(0, MaxTicketEvidenceBytes - used) / (1024.0 * 1024.0);
                EvidenceUsageText = $"Evidence storage: {usedMb:0.#} MB used of {limitMb:0} MB ({remainingMb:0.#} MB remaining)";
                EvidenceUsageNearLimit = used >= MaxTicketEvidenceBytes * 0.9;
            }
            catch
            {
                // Non-critical — the cap is still enforced server-side even if this display fails.
            }
        }

        private void RecomputeAllPartsTerminal()
        {
            // Parts are an optional sub-workflow — a ticket with none logged at all (a simple
            // single-item repair the technician never broke into parts) must still be able to save
            // a Conclusion. Only block on "not every part is done yet" when parts actually exist.
            AllPartsTerminal = Parts.Count == 0 || Parts.All(p => p.Status == "Repaired" || p.Status == "CannotRepair");
            SaveConclusionCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Lightweight refresh after a Part mutation — re-fetches just the ticket header
        /// (Status/CompletedAt may have changed via the server-side roll-up) and each Part card's
        /// summary counts, without collapsing already-expanded cards or reloading Observations.</summary>
        private async Task RefreshAfterPartChangeAsync()
        {
            try
            {
                var detail = await _repository.GetTicketDetailAsync(RepairTicketId);
                if (detail == null) return;

                if (Detail != null)
                {
                    Detail.Status = detail.Status;
                    Detail.CompletedAt = detail.CompletedAt;
                    Detail.UpdatedAt = detail.UpdatedAt;
                    OnPropertyChanged(nameof(Detail));
                }

                foreach (var card in Parts)
                {
                    var refreshed = detail.Parts.FirstOrDefault(p => p.RepairPartId == card.RepairPartId);
                    if (refreshed != null) card.UpdateSummary(refreshed);
                }

                // Pick up newly created parts that this refresh path doesn't otherwise see, and
                // drop any that a delete already removed from this VM's Parts collection.
                foreach (var p in detail.Parts)
                {
                    if (Parts.All(c => c.RepairPartId != p.RepairPartId))
                        Parts.Add(BuildPartCard(p));
                }

                RecomputeAllPartsTerminal();
                await RefreshEvidenceUsageAsync();
                DetailChanged?.Invoke();
            }
            catch
            {
                // Non-critical — the next full LoadAsync() (e.g. window reopen) will resync.
            }
        }

        private async Task LoadAttachmentBytesAsync(AttachmentDisplayItem item)
        {
            try
            {
                var full = await _repository.GetAttachmentAsync(item.AttachmentId);
                if (full != null)
                    item.Bytes = full.FileBytes;
            }
            catch
            {
                // Non-critical — the placeholder stays if a single attachment fails to load.
            }
        }

        private async Task AddNoteAsync()
        {
            if (string.IsNullOrWhiteSpace(NewNoteText))
                return;

            try
            {
                await _repository.AddNoteAsync(RepairTicketId, NewNoteText.Trim(), "Note", AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null, AppSession.CurrentEmployeeId);
                NewNoteText = string.Empty;
                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Add Note Failed", ex.Message);
            }
        }

        private async Task DeleteSelectedAttachmentsAsync()
        {
            var selected = Attachments.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0) return;

            var confirmed = RequestConfirm?.Invoke(
                "Delete Evidence",
                $"Permanently delete {selected.Count} selected file{(selected.Count == 1 ? "" : "s")}? This cannot be undone.") ?? false;
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                var userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null;
                foreach (var item in selected)
                    await _repository.DeleteAttachmentAsync(item.AttachmentId, userId);

                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Delete Failed", "Failed to delete evidence: " + ex.Message);
            }
            finally
            {
                IsBusy = false;
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

        /// <summary>Shared by both the file-picker button and WPF drag-and-drop on the evidence
        /// panel — both converge here.</summary>
        public async Task AddAttachmentsAsync(IEnumerable<string> filePaths)
        {
            if (filePaths == null) return;

            IsBusy = true;
            try
            {
                var skipped = new List<string>();
                var skippedForTicketCap = new List<string>();
                var runningTotal = await _repository.GetTotalEvidenceSizeBytesAsync(RepairTicketId);

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

                    await _repository.AddAttachmentAsync(RepairTicketId, type, fileName, mime, bytes, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null);
                    runningTotal += fileSize;
                }

                await LoadAsync();
                DetailChanged?.Invoke();

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
            finally
            {
                IsBusy = false;
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

        private async Task SetStatusAsync(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return;

            DateTime? completedAtOverride = null;
            var isTerminal = string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)
                              || string.Equals(status, "Unrepairable", StringComparison.OrdinalIgnoreCase);
            if (isTerminal)
            {
                var chosen = RequestCompletionDate?.Invoke(status);
                if (chosen == null)
                    return; // Cancelled — abort the status change rather than silently using "now".

                // CompletionDatePromptForm's DateTimePicker.Value is local wall-clock time, but
                // every timestamp column in this schema (including CompletedAt) is stored as UTC
                // (SYSUTCDATETIME()) and read back via ToManilaLocal — passing the local value
                // through unconverted double-applies the Manila offset on display (e.g. a ticket
                // actually completed at 8:40 AM shows as 4:40 PM, or wraps to the wrong day).
                completedAtOverride = DateTime.SpecifyKind(chosen.Value, DateTimeKind.Local).ToUniversalTime();
            }

            try
            {
                await _repository.SetStatusAsync(RepairTicketId, status, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null, null, completedAtOverride);

                if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase))
                    await _repository.AutoReturnSpareIfAnyAsync(RepairTicketId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1);

                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Status Update Failed", ex.Message);
            }
        }

        // ── Spare item loaner (Repairing status) ─────────────────────────────

        private async Task LoadActiveSpareAsync()
        {
            try
            {
                var active = await _repository.GetActiveSpareAsync(RepairTicketId);
                HasActiveSpare = active != null;
                SpareSummaryText = active == null
                    ? "No spare currently assigned."
                    : $"Spare assigned: {active.ItemDisplay} — borrowed {active.BorrowedAtLocal} ({active.BorrowedByEmpName ?? active.BorrowedByDeptName}).";
            }
            catch
            {
                // Non-critical — the card just shows "No spare" if this fails (e.g. schema not installed).
                HasActiveSpare = false;
                SpareSummaryText = "No spare currently assigned.";
            }
        }

        private async Task AssignSpareAsync()
        {
            if (Detail == null) return;

            var serialNumber = RequestSpareItemPicker?.Invoke();
            if (string.IsNullOrWhiteSpace(serialNumber))
                return; // Cancelled.

            var confirmed = RequestConfirm?.Invoke(
                "Assign Spare",
                "This will loan the picked item to this ticket's requester until it's returned or the ticket is marked Completed. Continue?") ?? false;
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _repository.AssignSpareAsync(RepairTicketId, serialNumber, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1);
                await LoadActiveSpareAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Assign Spare Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async Task UnlinkSpareAsync()
        {
            var confirmed = RequestConfirm?.Invoke("Unlink Spare", "This marks the spare as returned. Continue?") ?? false;
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _repository.UnlinkSpareAsync(RepairTicketId, AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1);
                await LoadActiveSpareAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Unlink Spare Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string BuildDispositionSummaryText(RepairConclusion conclusion)
        {
            if (conclusion == null || !conclusion.DispositionExecutedAt.HasValue)
                return "No disposition recorded yet.";

            if (string.Equals(conclusion.Disposition, "Replace", StringComparison.OrdinalIgnoreCase))
            {
                var setLabel = conclusion.ReplacementSetCode ?? (conclusion.ReplacementSetId.HasValue ? $"Set #{conclusion.ReplacementSetId}" : "(unknown set)");
                var itemLabel = conclusion.ReplacementItemName ?? "(unknown item)";
                return $"Replacement requested: {itemLabel} — {setLabel}, decided {conclusion.DispositionDecidedAt:MMM d, yyyy h:mm tt}.";
            }

            return $"Discarded on {conclusion.DispositionDecidedAt:MMM d, yyyy h:mm tt}.";
        }

        /// <summary>Sets or changes the Unrepairable disposition. Always changeable: re-picking either
        /// option (or a different replacement item) reverses the prior choice's side effects on the
        /// server before applying the new one — see RepairTicketRepository.Disposition.cs.</summary>
        private async Task SetDispositionAsync(string disposition)
        {
            if (Detail == null) return;

            int? replacementItemId = null;
            if (string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase))
            {
                replacementItemId = RequestReplacementItemPicker?.Invoke();
                if (replacementItemId == null)
                    return; // Cancelled.
            }

            var confirmed = RequestConfirm?.Invoke(
                "Confirm Disposition",
                string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase)
                    ? "This will retire the broken item and issue the chosen replacement to the original requester. Continue?"
                    : "This will retire the broken item permanently. Continue?") ?? false;
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _repository.SetDispositionAsync(
                    RepairTicketId, disposition, replacementItemId,
                    AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1,
                    AppSession.CurrentUserName);

                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Disposition Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>"Unlink" — reverses an already-executed disposition and clears it back to
        /// "no disposition recorded," for correcting a mistake without forcing an immediate new
        /// choice. No-ops server-side if nothing is currently executed.</summary>
        private async Task UnlinkDispositionAsync()
        {
            if (Detail == null) return;

            var confirmed = RequestConfirm?.Invoke(
                "Unlink Disposition",
                "This will undo the current disposition — reactivating a discarded item, or removing a spawned replacement request. Continue?") ?? false;
            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _repository.ClearDispositionAsync(
                    RepairTicketId,
                    AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1,
                    AppSession.CurrentUserName);

                await LoadAsync();
                DetailChanged?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Unlink Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        /// <summary>Permanently deletes this ticket — for correcting an accidental intake mistake
        /// (e.g. the wrong item was selected). Confirms first since this cannot be undone.</summary>
        private async Task DeleteTicketAsync()
        {
            var itemLabel = Detail?.ItemNameSnapshot ?? "this item";
            var confirmed = RequestConfirm?.Invoke(
                "Delete Ticket",
                $"Permanently delete this repair ticket for \"{itemLabel}\"? This removes all observations, parts, notes, evidence, and history logged on it. This cannot be undone.") ?? false;

            if (!confirmed) return;

            IsBusy = true;
            try
            {
                await _repository.DeleteTicketAsync(RepairTicketId);
                RequestCloseWindow?.Invoke();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Delete Failed", ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        // Guards against RelayCommand's Execute being effectively "async void" and re-entered by a
        // still-bubbling click while the report's own nested dialogs (signatory picker, attachment
        // picker) are pumping their own message loop — same reasoning as
        // RepairReportsListViewModel's _isGeneratingReport.
        private bool _isGeneratingReport;

        /// <summary>Generates this ticket's own Repair Report directly from its Detail window —
        /// the exact same flow as "View Report" from the Reports list, just without needing to
        /// leave this screen first.</summary>
        private async Task PrintReportAsync()
        {
            if (_isGeneratingReport) return;
            _isGeneratingReport = true;
            try
            {
                await RepairReportBuilder.ShowRepairReportAsync(RepairTicketId);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Report Failed", "Failed to generate report: " + ex.Message);
            }
            finally
            {
                _isGeneratingReport = false;
            }
        }

        // ── Observations (Reported Problem section) ─────────────────────────

        private ObservationCardViewModel BuildObservationCard(RepairItemObservation model)
        {
            var card = new ObservationCardViewModel(model)
            {
                OnSave = async c =>
                {
                    try
                    {
                        await _repository.UpdateObservationAsync(c.ObservationId, c.EditText.Trim());
                        await LoadAsync();
                    }
                    catch (Exception ex)
                    {
                        RequestError?.Invoke("Save Failed", ex.Message);
                    }
                },
                OnDelete = async c =>
                {
                    var confirmed = RequestConfirm?.Invoke("Delete Observation", "Delete this observation?") ?? false;
                    if (!confirmed) return;

                    try
                    {
                        await _repository.DeleteObservationAsync(c.ObservationId);
                        await LoadAsync();
                    }
                    catch (Exception ex)
                    {
                        RequestError?.Invoke("Delete Failed", ex.Message);
                    }
                },
                OnMoveUp = async c => await MoveObservationAsync(c, -1),
                OnMoveDown = async c => await MoveObservationAsync(c, 1)
            };

            return card;
        }

        private async Task MoveObservationAsync(ObservationCardViewModel card, int direction)
        {
            var index = Observations.IndexOf(card);
            var targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= Observations.Count) return;

            var orderedIds = Observations.Select(o => o.ObservationId).ToList();
            var id = orderedIds[index];
            orderedIds.RemoveAt(index);
            orderedIds.Insert(targetIndex, id);

            try
            {
                await _repository.ReorderObservationsAsync(RepairTicketId, orderedIds);
                await LoadAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Reorder Failed", ex.Message);
            }
        }

        private async Task AddObservationAsync()
        {
            if (string.IsNullOrWhiteSpace(NewObservationText)) return;

            try
            {
                await _repository.AddObservationAsync(RepairTicketId, NewObservationText.Trim(), AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : (int?)null);
                NewObservationText = string.Empty;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Add Observation Failed", ex.Message);
            }
        }

        // ── Parts (Part Diagnostics section) ─────────────────────────────────

        private RepairPartCardViewModel BuildPartCard(RepairPart part)
        {
            var card = new RepairPartCardViewModel(part, _repository);
            card.PartChanged += async () => await RefreshAfterPartChangeAsync();
            card.Deleted += c =>
            {
                Parts.Remove(c);
                _ = RefreshAfterPartChangeAsync();
            };
            return card;
        }

        // ── Repair Conclusion ─────────────────────────────────────────────────

        private async Task SaveConclusionAsync()
        {
            if (!AllPartsTerminal) return;

            try
            {
                var conclusion = new RepairConclusion
                {
                    RepairTicketId = RepairTicketId,
                    RootCause = string.IsNullOrWhiteSpace(RootCauseInput) ? null : RootCauseInput.Trim(),
                    WorkPerformed = string.IsNullOrWhiteSpace(WorkPerformedInput) ? null : WorkPerformedInput.Trim(),
                    FinalOutcome = string.IsNullOrWhiteSpace(FinalOutcomeInput) ? null : FinalOutcomeInput.Trim(),
                    Recommendations = string.IsNullOrWhiteSpace(RecommendationsInput) ? null : RecommendationsInput.Trim(),
                    RepairedByEmpIds = _selectedRepairedByEmpIds,
                    HandedOverToVendorId = SelectedVendor != null && SelectedVendor.VendorId > 0 ? SelectedVendor.VendorId : (int?)null
                };

                await _repository.SaveConclusionAsync(conclusion);
                await LoadAsync();
                RequestInfo?.Invoke("Saved", "Repair conclusion saved.");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Save Failed", ex.Message);
            }
        }
    }
}
