using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestPortal.AuthorizeRequest.ViewModels
{
    public class AuthorizeRequestViewModel : ViewModelBase
    {
        private readonly CartridgeAuthorizationRepository _repo
            = new CartridgeAuthorizationRepository();

        private readonly List<PendingAuthItemViewModel> _allItems
            = new List<PendingAuthItemViewModel>();

        private DispatcherTimer _pollTimer;

        // ── Queue / filter ────────────────────────────────────────────────────

        public ObservableCollection<PendingAuthItemViewModel> FilteredQueue { get; }
            = new ObservableCollection<PendingAuthItemViewModel>();

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        // ── Selection ─────────────────────────────────────────────────────────

        private PendingAuthItemViewModel _selectedRequest;
        public PendingAuthItemViewModel SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                var previous = _selectedRequest;
                if (!SetField(ref _selectedRequest, value)) return;

                // When navigating away from a request that was completed by another user,
                // remove it from the list now and clear the banner
                if (previous != null && _isExternallyCompleted)
                {
                    _isExternallyCompleted      = false;
                    _externalCompletionMessage  = string.Empty;
                    OnPropertyChanged(nameof(IsExternallyCompleted));
                    OnPropertyChanged(nameof(ExternalCompletionMessage));
                    _allItems.Remove(previous);
                    ApplyFilter();
                }
                else if (_isExternallyCompleted)
                {
                    IsExternallyCompleted = false;
                }

                OnPropertyChanged(nameof(HasSelection));
                OnPropertyChanged(nameof(NoSelection));
                OnPropertyChanged(nameof(CanAct));
                OnPropertyChanged(nameof(RequesterName));
                OnPropertyChanged(nameof(RequesterPosition));
                OnPropertyChanged(nameof(FulfillmentMethod));
                OnPropertyChanged(nameof(ReceivedByName));
                OnPropertyChanged(nameof(HasFulfillmentMethod));
                OnPropertyChanged(nameof(ShowReceivedBy));
                LoadCartridgeLines();
            }
        }

        public bool HasSelection => _selectedRequest != null;
        public bool NoSelection  => _selectedRequest == null;

        // ── Cartridge lines for selected request ──────────────────────────────

        public ObservableCollection<CartridgeLineViewModel> CartridgeLines { get; }
            = new ObservableCollection<CartridgeLineViewModel>();

        // ── Authorization statement — signer info from session ─────────────────

        public string SignerName
            => AppSession.CurrentEmployeeName ?? AppSession.CurrentUserName ?? "—";

        public string SignerPosition
            => AppSession.CurrentEmployeePosition ?? "—";

        public string SignerOrgLine
            => $"{AppSession.CurrentCompanyName ?? "—"} – " +
               $"{AppSession.CurrentDepartmentName ?? "—"}, " +
               $"{AppSession.CurrentBranchName ?? "—"}";

        public string RequesterName     => _selectedRequest?.EmployeeName     ?? "—";
        public string RequesterPosition => _selectedRequest?.EmployeePosition ?? "—";

        public string FulfillmentMethod  => _selectedRequest?.FulfillmentMethod ?? string.Empty;
        public string ReceivedByName     => _selectedRequest?.ReceivedByName    ?? string.Empty;
        public bool   HasFulfillmentMethod => !string.IsNullOrWhiteSpace(FulfillmentMethod);
        public bool   ShowReceivedBy       => FulfillmentMethod.Equals("Pickup", StringComparison.OrdinalIgnoreCase)
                                              && !string.IsNullOrWhiteSpace(ReceivedByName);

        public string TodayFormatted => DateTime.Now.ToString("MMMM dd, yyyy");

        // ── Tour dummy-data state ─────────────────────────────────────────────

        private bool _tourDummyInjected;

        internal void AddTourDummyItems()
        {
            if (_tourDummyInjected) return;
            _tourDummyInjected = true;

            var dummyLines = new List<CartridgeLineViewModel>
            {
                new CartridgeLineViewModel { Model = "TK-1110", Qty = 2, Good = 2, Damaged = 0 },
                new CartridgeLineViewModel { Model = "TK-3410", Qty = 1, Good = 0, Damaged = 1 },
            };

            var item1 = new PendingAuthItemViewModel
            {
                AuthorizationId   = -1,
                EmployeeId        = -1,
                EmployeeName      = "Maria Santos",
                EmployeePosition  = "Supervisor",
                DepartmentName    = "Warehouse",
                BranchName        = "Main Branch",
                CompanyName       = "Yakult Philippines",
                CreatedDate       = DateTime.Today,
                CreatedDateText   = DateTime.Today.ToString("MM/dd/yyyy"),
                ApprovalLevel     = "PendingSupervisor",
                ApprovalLevelLabel = "Pending Supervisor",
                StatusTooltip     = "Awaiting supervisor approval",
                FulfillmentMethod = "Pickup",
                ReceivedByName    = "Juan dela Cruz",
                CartridgeLines    = dummyLines,
            };

            var item2 = new PendingAuthItemViewModel
            {
                AuthorizationId   = -2,
                EmployeeId        = -2,
                EmployeeName      = "Carlo Reyes",
                EmployeePosition  = "Manager",
                DepartmentName    = "Operations",
                BranchName        = "Main Branch",
                CompanyName       = "Yakult Philippines",
                CreatedDate       = DateTime.Today.AddDays(-1),
                CreatedDateText   = DateTime.Today.AddDays(-1).ToString("MM/dd/yyyy"),
                ApprovalLevel     = "PendingManager",
                ApprovalLevelLabel = "Pending Manager",
                StatusTooltip     = "Awaiting manager approval",
                FulfillmentMethod = "Delivery",
                ReceivedByName    = string.Empty,
                CartridgeLines    = new List<CartridgeLineViewModel>
                {
                    new CartridgeLineViewModel { Model = "TK-5150", Qty = 3, Good = 3, Damaged = 0 },
                },
            };

            _allItems.Insert(0, item1);
            _allItems.Insert(1, item2);
            ApplyFilter();
            SelectFirstItemForTour();
        }

        internal void RemoveTourDummyItems()
        {
            if (!_tourDummyInjected) return;
            _tourDummyInjected = false;

            bool selectedWasDummy = _selectedRequest != null && _selectedRequest.AuthorizationId < 0;
            if (selectedWasDummy)
                SelectedRequest = null;

            var dummies = _allItems.Where(x => x.AuthorizationId < 0).ToList();
            foreach (var d in dummies)
            {
                _allItems.Remove(d);
                FilteredQueue.Remove(d);
            }
            OnPropertyChanged(nameof(QueueCount));
        }

        private void SelectFirstItemForTour()
        {
            if (FilteredQueue.Count > 0)
                SelectedRequest = FilteredQueue[0];
        }

        // ── State flags ───────────────────────────────────────────────────────

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        private bool _isProcessing;
        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (SetField(ref _isProcessing, value))
                    OnPropertyChanged(nameof(CanAct));
            }
        }

        public bool CanAct => HasSelection && !_isProcessing && !_isExternallyCompleted;

        private bool _isExternallyCompleted;
        public bool IsExternallyCompleted
        {
            get => _isExternallyCompleted;
            set
            {
                if (SetField(ref _isExternallyCompleted, value))
                    OnPropertyChanged(nameof(CanAct));
            }
        }

        private string _externalCompletionMessage = string.Empty;
        public string ExternalCompletionMessage
        {
            get => _externalCompletionMessage;
            set => SetField(ref _externalCompletionMessage, value);
        }

        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (SetField(ref _statusMessage, value))
                    OnPropertyChanged(nameof(HasStatus));
            }
        }

        public bool HasStatus => !string.IsNullOrEmpty(_statusMessage);

        private bool _isError;
        public bool IsError
        {
            get => _isError;
            set => SetField(ref _isError, value);
        }

        private bool _isSuccess;
        public bool IsSuccess
        {
            get => _isSuccess;
            set => SetField(ref _isSuccess, value);
        }

        public int QueueCount => FilteredQueue.Count;

        // ── Signature ─────────────────────────────────────────────────────────

        private bool _isDrawTab = true;
        public bool IsDrawTab
        {
            get => _isDrawTab;
            set
            {
                if (SetField(ref _isDrawTab, value))
                    OnPropertyChanged(nameof(IsUploadTab));
            }
        }

        public bool IsUploadTab => !_isDrawTab;

        private ImageSource _uploadedImageSource;
        public ImageSource UploadedImageSource
        {
            get => _uploadedImageSource;
            set
            {
                SetField(ref _uploadedImageSource, value);
                OnPropertyChanged(nameof(HasUploadedImage));
            }
        }

        public bool HasUploadedImage => _uploadedImageSource != null;

        // Live preview shown inside the authorization statement document
        private ImageSource _previewSignatureSource;
        public ImageSource PreviewSignatureSource
        {
            get => _previewSignatureSource;
            set
            {
                SetField(ref _previewSignatureSource, value);
                OnPropertyChanged(nameof(HasPreviewSignature));
            }
        }

        public bool HasPreviewSignature => _previewSignatureSource != null;

        // Called by the View's code-behind when ink strokes change
        public void SetDrawPreview(ImageSource source) => PreviewSignatureSource = source;

        private string _uploadedSignatureDataUrl;

        // ── Notes ─────────────────────────────────────────────────────────────

        private string _notes = string.Empty;
        public string Notes
        {
            get => _notes;
            set => SetField(ref _notes, value);
        }

        // ── Delegates wired by View code-behind ───────────────────────────────

        public Func<(string data, string source)> GetSignatureData { get; set; }
        public Action ClearSignatureCanvas { get; set; }

        // ── Commands ──────────────────────────────────────────────────────────

        public ICommand RefreshCommand        { get; }
        public ICommand SwitchToDrawCommand   { get; }
        public ICommand SwitchToUploadCommand { get; }
        public ICommand BrowseImageCommand    { get; }
        public ICommand ApproveCommand        { get; }
        public ICommand RejectCommand         { get; }
        public ICommand ClearSignatureCommand { get; }

        public AuthorizeRequestViewModel()
        {
            RefreshCommand        = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            SwitchToDrawCommand   = new RelayCommand(() => IsDrawTab = true);
            SwitchToUploadCommand = new RelayCommand(() => IsDrawTab = false);
            BrowseImageCommand    = new RelayCommand(BrowseImage);
            ApproveCommand        = new RelayCommand(async () => await ApproveAsync(), () => CanAct);
            RejectCommand         = new RelayCommand(async () => await RejectAsync(),  () => CanAct);
            ClearSignatureCommand = new RelayCommand(DoClearSignature);
        }

        // ── Polling ───────────────────────────────────────────────────────────

        public void StartPolling()
        {
            _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
            _pollTimer.Tick += async (s, e) => await PollAsync();
            _pollTimer.Start();
        }

        public void StopPolling()
        {
            _pollTimer?.Stop();
            _pollTimer = null;
        }

        private async Task PollAsync()
        {
            if (_isLoading || _isProcessing || _tourDummyInjected) return;
            try { await MergePendingAsync(); }
            catch { /* silent — do not surface poll errors to the user */ }
        }

        // Smart in-place merge: updates status dots without rebuilding the list,
        // and keeps the selected request visible even if it left the pending queue.
        //
        // Critical: never calls FilteredQueue.Clear(). ObservableCollection.Clear() fires
        // a Reset notification; WPF ListBox responds by setting SelectedItem = null through
        // the two-way binding, losing the selection before items are re-added.
        // Individual Remove/Add fire targeted notifications that leave the selection intact.
        private async Task MergePendingAsync()
        {
            var freshList = await FetchPendingAsync();
            var freshById = freshList.ToDictionary(m => m.AuthorizationId);
            var q = (_searchText ?? "").Trim().ToLowerInvariant();

            foreach (var item in _allItems.ToList())
            {
                if (freshById.TryGetValue(item.AuthorizationId, out var fresh))
                {
                    var (level, label, tooltip) = DeriveApprovalLevel(fresh.EmployeePosition, fresh.Status);
                    item.ApprovalLevel      = level;
                    item.ApprovalLevelLabel = label;
                    item.StatusTooltip      = tooltip;
                }
                else
                {
                    if (_selectedRequest?.AuthorizationId == item.AuthorizationId)
                    {
                        // Keep it open; show ambient banner; disable action buttons
                        item.ApprovalLevel        = "Approved";
                        item.ApprovalLevelLabel   = "Approved";
                        item.StatusTooltip        = "Processed by another approver";
                        IsExternallyCompleted     = true;
                        ExternalCompletionMessage = "This request has already been processed by another approver.";
                    }
                    else
                    {
                        _allItems.Remove(item);
                        FilteredQueue.Remove(item);
                    }
                }
            }

            // Add items that appeared since the last poll
            foreach (var m in freshList)
            {
                if (!_allItems.Any(x => x.AuthorizationId == m.AuthorizationId))
                {
                    var vm = Map(m);
                    _allItems.Add(vm);
                    if (MatchesFilter(vm, q))
                        FilteredQueue.Add(vm);
                }
            }

            OnPropertyChanged(nameof(QueueCount));
        }

        private bool MatchesFilter(PendingAuthItemViewModel item, string q)
            => string.IsNullOrEmpty(q)
            || (item.EmployeeName   ?? "").ToLowerInvariant().Contains(q)
            || (item.DepartmentName ?? "").ToLowerInvariant().Contains(q)
            || (item.BranchName     ?? "").ToLowerInvariant().Contains(q)
            || (item.CompanyName    ?? "").ToLowerInvariant().Contains(q);

        private async Task<List<CartridgeAuthorizationModel>> FetchPendingAsync()
        {
            if (AppSession.IsDeveloper || AppSession.IsAdmin)
                return await _repo.GetAllPendingAsync();
            return await _repo.GetPendingByScopeAsync(
                AppSession.CurrentCompanyId,
                AppSession.CurrentBranchId,
                AppSession.CurrentDepartmentId,
                AppSession.CurrentEmployeeId);
        }

        // ── Load ──────────────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsLoading     = true;
            IsError       = false;
            IsSuccess     = false;
            StatusMessage = string.Empty;

            try
            {
                await LoadCoreAsync();
            }
            catch (Exception ex)
            {
                IsError       = true;
                StatusMessage = $"Failed to load authorizations: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadCoreAsync()
        {
            var pending = await FetchPendingAsync();
            _allItems.Clear();
            foreach (var m in pending)
                _allItems.Add(Map(m));
            ApplyFilter();
        }

        public async Task SelectByAuthIdAsync(int authorizationId)
        {
            await LoadAsync();
            var item = FilteredQueue.FirstOrDefault(x => x.AuthorizationId == authorizationId)
                    ?? _allItems.FirstOrDefault(x => x.AuthorizationId == authorizationId);
            if (item != null)
                SelectedRequest = item;
        }

        // ── Filter ────────────────────────────────────────────────────────────

        private void ApplyFilter()
        {
            var q = (_searchText ?? "").Trim().ToLowerInvariant();

            FilteredQueue.Clear();
            foreach (var item in _allItems)
            {
                if (MatchesFilter(item, q))
                    FilteredQueue.Add(item);
            }

            OnPropertyChanged(nameof(QueueCount));

            // Don't deselect a request that is intentionally kept open after external completion
            if (_selectedRequest != null && !FilteredQueue.Contains(_selectedRequest) && !_isExternallyCompleted)
                SelectedRequest = null;
        }

        private void LoadCartridgeLines()
        {
            CartridgeLines.Clear();
            if (_selectedRequest == null) return;
            foreach (var line in _selectedRequest.CartridgeLines)
                CartridgeLines.Add(line);
        }

        // ── Approve ───────────────────────────────────────────────────────────

        private async Task ApproveAsync()
        {
            if (_selectedRequest == null) return;

            var (sigData, sigSource) = GetSignatureData?.Invoke() ?? (null, null);

            if (string.IsNullOrEmpty(sigData))
            {
                IsError       = true;
                IsSuccess     = false;
                StatusMessage = "Please draw or upload a signature before approving.";
                return;
            }

            IsProcessing  = true;
            IsError       = false;
            IsSuccess     = false;
            StatusMessage = "Approving…";

            try
            {
                bool ok = await _repo.ApproveWithSignatureAsync(
                    _selectedRequest.AuthorizationId,
                    AppSession.CurrentUserId,
                    sigData,
                    sigSource,
                    AppSession.CurrentEmployeePosition,
                    AppSession.CurrentCompanyName,
                    AppSession.CurrentBranchName);

                if (ok)
                {
                    int approvedAuthId  = _selectedRequest.AuthorizationId;
                    int requesterEmpId  = _selectedRequest.EmployeeId;

                    DoClearSignature();
                    Notes = string.Empty;
                    await LoadAsync();

                    IsSuccess = true;
                    try
                    {
                        var notifRepo    = new NotificationRepository();
                        int? notifUserId = notifRepo.GetUserIdByAuthorizationId(approvedAuthId)
                                        ?? notifRepo.GetUserIdByEmployeeId(requesterEmpId);
                        if (notifUserId.HasValue)
                        {
                            notifRepo.Create(new NotificationCreateDto
                            {
                                UserId           = notifUserId.Value,
                                Title            = "Authorization Approved",
                                Message          = "Your cartridge authorization request has been approved and signed.",
                                NotificationType = NotificationType.AuthorizationApproved,
                                ReferenceId      = approvedAuthId
                            });
                        }
                    }
                    catch (Exception notifEx)
                    {
                        Debug.WriteLine($"[AuthorizeRequestViewModel] Notification error: {notifEx.Message}");
                    }

                    System.Windows.MessageBox.Show(
                        "The authorization has been approved and signed successfully.",
                        "Authorization Approved",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Information);
                }
                else
                {
                    IsError       = true;
                    StatusMessage = "This request was already processed by someone else. Refreshing queue.";
                    await LoadAsync();
                }
            }
            catch (Exception ex)
            {
                IsError       = true;
                StatusMessage = $"Approval failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        // ── Reject ────────────────────────────────────────────────────────────

        private async Task RejectAsync()
        {
            if (_selectedRequest == null) return;

            IsProcessing  = true;
            IsError       = false;
            IsSuccess     = false;
            StatusMessage = "Rejecting…";

            try
            {
                int rejectedAuthId = _selectedRequest.AuthorizationId;
                int rejectEmpId    = _selectedRequest.EmployeeId;
                await _repo.RejectAuthorizationAsync(rejectedAuthId);
                IsSuccess     = true;
                StatusMessage = $"Authorization #{rejectedAuthId} rejected.";

                try
                {
                    var notifRepo    = new NotificationRepository();
                    int? notifUserId = notifRepo.GetUserIdByAuthorizationId(rejectedAuthId)
                                    ?? notifRepo.GetUserIdByEmployeeId(rejectEmpId);
                    if (notifUserId.HasValue)
                    {
                        notifRepo.Create(new NotificationCreateDto
                        {
                            UserId           = notifUserId.Value,
                            Title            = "Authorization Rejected",
                            Message          = "Your cartridge authorization request has been rejected. Please contact your supervisor for details.",
                            NotificationType = NotificationType.AuthorizationRejected,
                            ReferenceId      = rejectedAuthId
                        });
                    }
                }
                catch (Exception notifEx)
                {
                    Debug.WriteLine($"[AuthorizeRequestViewModel] Reject notification error: {notifEx.Message}");
                }

                Notes = string.Empty;
                await LoadAsync();
            }
            catch (Exception ex)
            {
                IsError       = true;
                StatusMessage = $"Rejection failed: {ex.Message}";
            }
            finally
            {
                IsProcessing = false;
            }
        }

        // ── Signature helpers ─────────────────────────────────────────────────

        private void BrowseImage()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                Title  = "Select Signature Image"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                var bytes = File.ReadAllBytes(dlg.FileName);

                if (bytes.Length > 2 * 1024 * 1024)
                {
                    IsError       = true;
                    StatusMessage = "Image exceeds the 2 MB limit.";
                    return;
                }

                var ext  = Path.GetExtension(dlg.FileName).ToLowerInvariant();
                var mime = ext == ".png" ? "image/png" : "image/jpeg";
                _uploadedSignatureDataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";

                UploadedImageSource    = new BitmapImage(new Uri(dlg.FileName));
                PreviewSignatureSource = UploadedImageSource;
                IsDrawTab     = false;
                IsError       = false;
                StatusMessage = string.Empty;
            }
            catch (Exception ex)
            {
                IsError       = true;
                StatusMessage = $"Could not load image: {ex.Message}";
            }
        }

        private void DoClearSignature()
        {
            ClearSignatureCanvas?.Invoke();
            _uploadedSignatureDataUrl = null;
            UploadedImageSource       = null;
            PreviewSignatureSource    = null;
            IsError                   = false;
            StatusMessage             = string.Empty;
        }

        internal (string data, string source) GetUploadedSig()
            => (_uploadedSignatureDataUrl, "upload");

        // ── Mapping ───────────────────────────────────────────────────────────

        private static PendingAuthItemViewModel Map(CartridgeAuthorizationModel m)
        {
            var (level, label, tooltip) = DeriveApprovalLevel(m.EmployeePosition, m.Status);
            return new PendingAuthItemViewModel
            {
                AuthorizationId  = m.AuthorizationId,
                EmployeeId       = m.EmployeeId,
                EmployeeName     = m.EmployeeName     ?? "—",
                EmployeePosition = m.EmployeePosition ?? "—",
                DepartmentName   = m.DepartmentName   ?? "—",
                BranchName       = m.BranchName       ?? "—",
                CompanyName      = m.CompanyName      ?? "—",
                CreatedDate      = m.CreatedDate,
                CreatedDateText  = m.CreatedDate.ToString("MM/dd/yyyy"),
                CartridgeLines    = ParseLines(m.RequestedModels),
                ApprovalLevel     = level,
                ApprovalLevelLabel = label,
                StatusTooltip     = tooltip,
                FulfillmentMethod = m.FulfillmentMethod,
                ReceivedByName    = m.ReceivedByName,
            };
        }

        private static (string level, string label, string tooltip) DeriveApprovalLevel(string position, string status)
        {
            var s = (status ?? "").Trim().ToLowerInvariant();
            if (s == "approved")
                return ("Approved",  "Approved",  "Fully approved");
            if (s == "rejected")
                return ("Rejected",  "Rejected",  "Request was rejected");

            var p = (position ?? "").ToLowerInvariant();
            if (p.Contains("manager") || p.Contains("director") || p.Contains("vp") || p.Contains("president"))
                return ("PendingManager",     "Pending Manager",     "Awaiting manager approval");
            if (p.Contains("supervisor") || p.Contains("team lead") || p.Contains("lead"))
                return ("PendingSupervisor",  "Pending Supervisor",  "Awaiting supervisor approval");

            return ("PendingCoordinator", "Pending Coordinator", "Awaiting coordinator approval");
        }

        private static List<CartridgeLineViewModel> ParseLines(string json)
        {
            var list = new List<CartridgeLineViewModel>();
            if (string.IsNullOrWhiteSpace(json)) return list;
            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    list.Add(new CartridgeLineViewModel
                    {
                        Model   = el.TryGetProperty("model",   out var m) ? m.GetString()  : "—",
                        Qty     = el.TryGetProperty("qty",     out var q) ? q.GetInt32()   : 0,
                        Good    = el.TryGetProperty("good",    out var g) ? g.GetInt32()   : 0,
                        Damaged = el.TryGetProperty("damaged", out var d) ? d.GetInt32()   : 0,
                    });
                }
            }
            catch { /* return partial list */ }
            return list;
        }
    }

    // ── Supporting view-models ────────────────────────────────────────────────

    public class PendingAuthItemViewModel : ViewModelBase
    {
        public int      AuthorizationId  { get; set; }
        public int      EmployeeId       { get; set; }
        public string   EmployeeName     { get; set; }
        public string   EmployeePosition { get; set; }
        public string   DepartmentName   { get; set; }
        public string   BranchName       { get; set; }
        public string   CompanyName      { get; set; }
        public DateTime CreatedDate      { get; set; }
        public string   CreatedDateText  { get; set; }
        public string   FulfillmentMethod { get; set; }
        public string   ReceivedByName    { get; set; }

        // "PendingCoordinator" | "PendingSupervisor" | "PendingManager" | "Approved" | "Rejected"
        private string _approvalLevel = "PendingCoordinator";
        public string ApprovalLevel
        {
            get => _approvalLevel;
            set => SetField(ref _approvalLevel, value);
        }

        private string _approvalLevelLabel = "Pending Coordinator";
        public string ApprovalLevelLabel
        {
            get => _approvalLevelLabel;
            set => SetField(ref _approvalLevelLabel, value);
        }

        private string _statusTooltip = "Awaiting coordinator approval";
        public string StatusTooltip
        {
            get => _statusTooltip;
            set => SetField(ref _statusTooltip, value);
        }

        public List<CartridgeLineViewModel> CartridgeLines { get; set; }
            = new List<CartridgeLineViewModel>();

        public string OrgLine   => $"{CompanyName} · {DepartmentName}";
        public string ModelList => CartridgeLines.Count > 0
            ? string.Join(", ", CartridgeLines.Select(l => l.Model))
            : "—";
    }

    public class CartridgeLineViewModel : ViewModelBase
    {
        public string Model   { get; set; }
        public int    Qty     { get; set; }
        public int    Good    { get; set; }
        public int    Damaged { get; set; }
    }
}
