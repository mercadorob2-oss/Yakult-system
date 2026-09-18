using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Yakult.Inventory.App.Forms.CartridgeManagement;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    /// <summary>
    /// Main ViewModel for the WPF Cartridge Management window.
    ///
    /// Differential refresh: groups are NEVER cleared on reload.  Only changed
    /// groups are added/removed/updated so scroll position and selection survive
    /// background refreshes (AJAX-style behaviour).
    ///
    /// Auto-refresh runs every 30 s and is paused while a request is selected or
    /// a fulfillment is in progress.
    /// </summary>
    public sealed class CartridgeManagementViewModel : ViewModelBase, IDisposable
    {
        // ── Repositories ──────────────────────────────────────────────────────────
        private readonly CartridgeManagementRepository _repo;

        // ── Raw data (full list, unsorted) ────────────────────────────────────────
        private List<CartridgeRequestDto>           _allRequests  = new List<CartridgeRequestDto>();
        private Dictionary<string, RequestSessionGroup> _allGroups = new Dictionary<string, RequestSessionGroup>();

        // ── Left panel — paged session cards ─────────────────────────────────────
        public ObservableCollection<SessionGroupViewModel> PagedGroups { get; }
            = new ObservableCollection<SessionGroupViewModel>();

        // Full VM dictionary for differential updates (keyed by SessionKey)
        private readonly Dictionary<string, SessionGroupViewModel> _vmByKey
            = new Dictionary<string, SessionGroupViewModel>();

        // ── Search / filter ───────────────────────────────────────────────────────
        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    _currentPage = 1;
                    ApplyPagedView();
                }
            }
        }

        // ── Date range filter ─────────────────────────────────────────────────────
        private DateTime? _dateFrom;
        private DateTime? _dateTo;
        private string _selectedQuickRange = "All";

        public DateTime? DateFrom
        {
            get => _dateFrom;
            set
            {
                if (SetField(ref _dateFrom, value))
                {
                    _currentPage = 1;
                    ApplyPagedView();
                }
            }
        }

        public DateTime? DateTo
        {
            get => _dateTo;
            set
            {
                if (SetField(ref _dateTo, value))
                {
                    _currentPage = 1;
                    ApplyPagedView();
                }
            }
        }

        public string SelectedQuickRange
        {
            get => _selectedQuickRange;
            set
            {
                if (SetField(ref _selectedQuickRange, value))
                    ApplyQuickRange();
            }
        }

        public string[] QuickRanges { get; } =
        {
            "All", "Last 7 days", "Last 30 days", "Last 90 days", "This month", "This year"
        };

        // ── Model type filter ─────────────────────────────────────────────────────
        private string _selectedModelFilter = "All";
        public string SelectedModelFilter
        {
            get => _selectedModelFilter;
            set
            {
                if (SetField(ref _selectedModelFilter, value))
                {
                    _currentPage = 1;
                    ApplyPagedView();
                }
            }
        }

        public string[] ModelFilterOptions { get; } = { "All", "Single Model", "Multi-Model" };

        public ICommand ClearDateFilterCommand { get; }

        private void ApplyQuickRange()
        {
            var now = DateTime.Today;
            switch (_selectedQuickRange)
            {
                case "Last 7 days":
                    _dateFrom = now.AddDays(-7);  _dateTo = now; break;
                case "Last 30 days":
                    _dateFrom = now.AddDays(-30); _dateTo = now; break;
                case "Last 90 days":
                    _dateFrom = now.AddDays(-90); _dateTo = now; break;
                case "This month":
                    _dateFrom = new DateTime(now.Year, now.Month, 1); _dateTo = now; break;
                case "This year":
                    _dateFrom = new DateTime(now.Year, 1, 1); _dateTo = now; break;
                default:
                    _dateFrom = null; _dateTo = null; break;
            }
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            _currentPage = 1;
            ApplyPagedView();
        }

        private bool MatchesSearch(SessionGroupViewModel g)
        {
            // Text filter
            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var words = _searchText.ToLowerInvariant()
                                      .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                var groupLabel = g.IsMultiModel ? "multi-model" : "single model";
                var bag = string.Join(" ",
                    g.EmployeeName   ?? "",
                    g.CompanyName    ?? "",
                    g.BranchName     ?? "",
                    g.DepartmentName ?? "",
                    groupLabel).ToLowerInvariant();
                bool textMatch = words.Any(w =>
                    bag.Contains(w) ||
                    g.Requests.Any(r =>
                        r.ModelNumber?.ToLowerInvariant().Contains(w)      == true ||
                        r.TypedModelNumber?.ToLowerInvariant().Contains(w) == true ||
                        r.ReqId.ToString().Contains(w)));
                if (!textMatch) return false;
            }

            // Model type filter
            if (_selectedModelFilter == "Single Model" && g.IsMultiModel)  return false;
            if (_selectedModelFilter == "Multi-Model"  && !g.IsMultiModel) return false;

            // Date range filter (by session submission date)
            if (_dateFrom.HasValue && g.DateCreated.Date < _dateFrom.Value.Date) return false;
            if (_dateTo.HasValue   && g.DateCreated.Date > _dateTo.Value.Date)   return false;

            return true;
        }

        // ── Sort ──────────────────────────────────────────────────────────────────
        private bool _sortAscending = true;
        public bool SortAscending
        {
            get => _sortAscending;
            set
            {
                if (SetField(ref _sortAscending, value))
                {
                    OnPropertyChanged(nameof(SortIndicatorText));
                    ApplyPagedView();
                }
            }
        }
        public string SortIndicatorText
            => _sortAscending
               ? "↑ Asc — Oldest First (Priority Order)"
               : "↓ Desc — Newest First";

        public string SortIndicatorColor
            => _sortAscending ? "#27AE60" : "#3498DB";

        // ── Pagination ────────────────────────────────────────────────────────────
        private int _currentPage = 1;
        private int _totalPages  = 1;
        private const int PageSize = 5;

        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (SetField(ref _currentPage, value))
                {
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(CanGoFirst));
                    OnPropertyChanged(nameof(CanGoPrev));
                    OnPropertyChanged(nameof(CanGoNext));
                    OnPropertyChanged(nameof(CanGoLast));
                }
            }
        }
        public int TotalPages  => _totalPages;
        public string PageInfo { get; private set; } = "No pending requests";

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < _totalPages;
        public bool CanGoLast  => _currentPage < _totalPages;

        // ── Loading / busy state ──────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            private set => SetField(ref _isLoading, value);
        }

        // ── Auto-refresh countdown ────────────────────────────────────────────────
        private DispatcherTimer _autoRefreshTimer;
        private int _autoRefreshSecondsLeft = 30;
        private string _autoRefreshText = "⏱ 30s";
        public string AutoRefreshText
        {
            get => _autoRefreshText;
            private set => SetField(ref _autoRefreshText, value);
        }

        // ── Selection state ───────────────────────────────────────────────────────
        private CartridgeRequestDto _selectedRequest;
        private bool _fulfillmentInProgress;

        private CartridgeRequestDto SelectedRequest
        {
            get => _selectedRequest;
            set
            {
                _selectedRequest = value;
                ExchangePanel.Clear();
            }
        }

        // ── Exchange panel ────────────────────────────────────────────────────────
        public ExchangePanelViewModel ExchangePanel { get; } = new ExchangePanelViewModel();

        // ── Cancellation for stock lookups ────────────────────────────────────────
        private CancellationTokenSource _buildCts;
        private int _buildVersion;

        // ── Commands ─────────────────────────────────────────────────────────────
        public ICommand RefreshCommand      { get; }
        public ICommand ToggleSortCommand   { get; }
        public ICommand FirstPageCommand    { get; }
        public ICommand PrevPageCommand     { get; }
        public ICommand NextPageCommand     { get; }
        public ICommand LastPageCommand     { get; }
        public ICommand SelectRequestCommand { get; }
        public ICommand OpenRequestCommand  { get; }
        public ICommand FulfillCommand      { get; }
        public ICommand ClearCommand        { get; }
        public ICommand ForceDeleteCommand  { get; }

        // Fired after a successful fulfillment commit — carries the transmittal data for printing.
        // The View subscribes and shows the print dialog before navigating to Send Notifications.
        public event EventHandler<CartridgeTransmittalViewModel> FulfillmentCompleted;

        // ── Constructor ───────────────────────────────────────────────────────────
        public CartridgeManagementViewModel()
        {
            _repo = new CartridgeManagementRepository();

            ClearDateFilterCommand = new RelayCommand(() =>
            {
                _dateFrom = null;
                _dateTo   = null;
                _selectedQuickRange  = "All";
                _selectedModelFilter = "All";
                OnPropertyChanged(nameof(DateFrom));
                OnPropertyChanged(nameof(DateTo));
                OnPropertyChanged(nameof(SelectedQuickRange));
                OnPropertyChanged(nameof(SelectedModelFilter));
                _currentPage = 1;
                ApplyPagedView();
            });

            RefreshCommand       = new RelayCommand(async () => await LoadAsync(resetPage: false), () => !_isLoading);
            ToggleSortCommand    = new RelayCommand(() => SortAscending = !_sortAscending);
            FirstPageCommand     = new RelayCommand(() => GoToPage(1),             () => CanGoFirst);
            PrevPageCommand      = new RelayCommand(() => GoToPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand      = new RelayCommand(() => GoToPage(_currentPage + 1), () => CanGoNext);
            LastPageCommand      = new RelayCommand(() => GoToPage(_totalPages),     () => CanGoLast);
            SelectRequestCommand = new RelayCommand<CartridgeRequestDto>(async r => await OnRequestSelectedAsync(r));
            OpenRequestCommand   = new RelayCommand(OnOpenRequest, () => _selectedRequest != null);
            FulfillCommand       = new RelayCommand(OnFulfill,    () => _selectedRequest != null && !_fulfillmentInProgress);
            ClearCommand         = new RelayCommand(OnClear);
            ForceDeleteCommand   = new RelayCommand(OnForceDelete, () => _selectedRequest != null);

            StartAutoRefreshTimer();
        }

        // ── Initial load ──────────────────────────────────────────────────────────
        public async Task InitializeAsync()
        {
            await LoadAsync(resetPage: true);
        }

        // ── Core load / differential sync ────────────────────────────────────────
        private async Task LoadAsync(bool resetPage)
        {
            if (_isLoading) return;

            IsLoading = true;
            ResetAutoRefreshTimer();

            try
            {
                var requests = await Task.Run(() => _repo.GetPendingCartridgeRequests());
                _allRequests = requests ?? new List<CartridgeRequestDto>();
                _allGroups   = BuildSessionGroups(_allRequests);

                if (resetPage) _currentPage = 1;

                DifferentialSync(_allGroups);
                ApplyPagedView();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading pending requests:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        // ── Differential sync (never clear — only add/remove/update) ──────────────
        private void DifferentialSync(Dictionary<string, RequestSessionGroup> incoming)
        {
            // Remove VMs for groups that disappeared
            var toRemove = _vmByKey.Keys.Except(incoming.Keys).ToList();
            foreach (var key in toRemove)
                _vmByKey.Remove(key);

            // Update existing VMs in-place
            foreach (var kvp in incoming)
            {
                if (_vmByKey.TryGetValue(kvp.Key, out var existing))
                    existing.UpdateFrom(kvp.Value);
                else
                    _vmByKey[kvp.Key] = new SessionGroupViewModel(kvp.Value);
            }
        }

        // ── Paged view projection ─────────────────────────────────────────────────
        private void ApplyPagedView()
        {
            var filtered = (IEnumerable<SessionGroupViewModel>)_vmByKey.Values.Where(MatchesSearch);

            var ordered = _sortAscending
                ? filtered.OrderBy(g => g.GroupIndex)
                : (IEnumerable<SessionGroupViewModel>)filtered.OrderByDescending(g => g.GroupIndex);

            var allVms   = ordered.ToList();
            int total    = allVms.Count;
            _totalPages  = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));

            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            var page = allVms
                .Skip((_currentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            // ── Smart-sync PagedGroups — preserve existing VMs ────────────────
            var pageKeys = page.Select(g => g.SessionKey).ToHashSet();

            // Remove VMs not in the new page
            for (int i = PagedGroups.Count - 1; i >= 0; i--)
                if (!pageKeys.Contains(PagedGroups[i].SessionKey))
                    PagedGroups.RemoveAt(i);

            // Add / reorder
            for (int i = 0; i < page.Count; i++)
            {
                var vm = page[i];
                int cur = PagedGroups.IndexOf(vm);
                if (cur < 0)
                    PagedGroups.Insert(i, vm);
                else if (cur != i)
                    PagedGroups.Move(cur, i);
            }

            UpdatePageInfo(total);
        }

        private void UpdatePageInfo(int totalGroups)
        {
            if (totalGroups == 0)
                PageInfo = "No pending requests";
            else
                PageInfo = $"Page {_currentPage} of {_totalPages}  ({totalGroups} groups)";

            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(TotalPages));
            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
        }

        private void GoToPage(int page)
        {
            _currentPage = Math.Max(1, Math.Min(page, _totalPages));
            ApplyPagedView();
        }

        // ── Grouping logic (mirrors WinForms GroupRequestsIntoSessions) ───────────
        private static Dictionary<string, RequestSessionGroup> BuildSessionGroups(
            List<CartridgeRequestDto> requests)
        {
            var sessions  = new Dictionary<string, RequestSessionGroup>();
            int groupIndex = 0;
            var sorted    = requests.OrderBy(r => r.DateCreated).ThenBy(r => r.ReqId).ToList();
            var processed = new HashSet<int>();

            foreach (var req in sorted)
            {
                if (processed.Contains(req.ReqId)) continue;

                if (req.SubmissionSessionId.HasValue)
                {
                    var related = requests
                        .Where(r => r.SubmissionSessionId.HasValue
                                 && r.SubmissionSessionId.Value == req.SubmissionSessionId.Value
                                 && !processed.Contains(r.ReqId))
                        .OrderBy(r => r.ReqId)
                        .ToList();

                    if (!related.Any()) continue;

                    int originalCount = related.Max(r => r.OriginalSubmissionCount);
                    var key           = $"G{req.SubmissionSessionId.Value:N}";

                    var session = new RequestSessionGroup
                    {
                        SessionKey     = key,
                        GroupIndex     = groupIndex++,
                        EmpId          = req.EmpId,
                        EmployeeName   = req.EmployeeName,
                        CompanyName    = req.CompanyName,
                        BranchName     = req.BranchName,
                        DepartmentName = req.DepartmentName,
                        DateCreated    = req.DateCreated,
                        Requests       = related,
                        IsMultiModel   = originalCount > 1
                    };
                    session.BranchDept    = $"{session.BranchName ?? "Unknown Branch"} / {session.DepartmentName ?? "Unknown Department"}";
                    session.ModelCount    = related.Count;
                    session.TotalQuantity = related.Sum(r => r.Quantity);

                    sessions[key] = session;
                    related.ForEach(r => processed.Add(r.ReqId));
                }
                else
                {
                    var key     = $"L{req.ReqId}";
                    var session = new RequestSessionGroup
                    {
                        SessionKey     = key,
                        GroupIndex     = groupIndex++,
                        EmpId          = req.EmpId,
                        EmployeeName   = req.EmployeeName,
                        CompanyName    = req.CompanyName,
                        BranchName     = req.BranchName,
                        DepartmentName = req.DepartmentName,
                        DateCreated    = req.DateCreated,
                        Requests       = new List<CartridgeRequestDto> { req },
                        IsMultiModel   = false
                    };
                    session.BranchDept    = $"{session.BranchName ?? "Unknown Branch"} / {session.DepartmentName ?? "Unknown Department"}";
                    session.ModelCount    = 1;
                    session.TotalQuantity = req.Quantity;

                    sessions[key] = session;
                    processed.Add(req.ReqId);
                }
            }

            return sessions;
        }

        private string GetSessionKeyForRequest(CartridgeRequestDto req)
            => req.SubmissionSessionId.HasValue
               ? $"G{req.SubmissionSessionId.Value:N}"
               : $"L{req.ReqId}";

        // ── Request selection (async stock lookup) ────────────────────────────────
        private async Task OnRequestSelectedAsync(CartridgeRequestDto request)
        {
            if (request == null) { OnClear(); return; }

            // Cancel any in-flight stock lookup
            _buildCts?.Cancel();
            _buildCts = new CancellationTokenSource();
            var token  = _buildCts.Token;
            int myVer  = Interlocked.Increment(ref _buildVersion);

            _selectedRequest = request;
            ResetAutoRefreshTimer();

            string key = GetSessionKeyForRequest(request);
            _allGroups.TryGetValue(key, out var session);

            bool isMulti = session != null && session.IsMultiModel
                        && session.Requests != null && session.Requests.Count > 1;

            if (isMulti)
            {
                ExchangePanel.LoadMultiModel(session);

                // Parallel stock lookup for all models
                var tasks = session.Requests.Select(req => Task.Run(() =>
                {
                    int bn = 0, rf = 0;
                    try
                    {
                        string ms = req.TypedModelNumber ?? req.ModelNumber;
                        int? id   = !string.IsNullOrWhiteSpace(ms)
                                    ? _repo.GetCartridgeModelIdByModelNumber(ms)
                                    : null;
                        if (!id.HasValue && req.CartridgeModelId.HasValue && req.CartridgeModelId.Value > 0)
                            id = req.CartridgeModelId;
                        bn = _repo.GetAvailableIssuableStockByCondition(id, "Brand New");
                        rf = _repo.GetAvailableIssuableStockByCondition(id, "Refilled");
                    }
                    catch { }
                    return (req.ReqId, BrandNew: bn, Refilled: rf);
                }, token));

                var results = await Task.WhenAll(tasks);
                if (token.IsCancellationRequested || myVer != _buildVersion) return;

                var stockMap = results.ToDictionary(r => r.ReqId, r => (r.BrandNew, r.Refilled));
                foreach (var req in session.Requests)
                {
                    stockMap.TryGetValue(req.ReqId, out var s);
                    ExchangePanel.AddMultiModelRow(new MultiModelRowViewModel(req, s.BrandNew, s.Refilled));
                }
            }
            else
            {
                string modelStr  = request.TypedModelNumber ?? request.ModelNumber;
                int? dtoCmid     = request.CartridgeModelId;

                var (bn, rf) = await Task.Run(() =>
                {
                    int? resolvedId = !string.IsNullOrWhiteSpace(modelStr)
                        ? _repo.GetCartridgeModelIdByModelNumber(modelStr)
                        : null;
                    if (!resolvedId.HasValue && dtoCmid.HasValue && dtoCmid.Value > 0)
                        resolvedId = dtoCmid;

                    int brandNew = _repo.GetAvailableIssuableStockByCondition(resolvedId, "Brand New");
                    int refilled  = _repo.GetAvailableIssuableStockByCondition(resolvedId, "Refilled");
                    return (brandNew, refilled);
                }, token);

                if (token.IsCancellationRequested || myVer != _buildVersion) return;

                ExchangePanel.LoadSingleModel(request, bn, rf);
            }
        }

        // ── Button handlers ───────────────────────────────────────────────────────
        private void OnOpenRequest()
        {
            if (_selectedRequest == null)
            {
                MessageBox.Show("Please select a request to open.", "No Selection",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void OnFulfill()
        {
            if (_selectedRequest == null) return;
            if (!ValidateExchange())      return;

            _fulfillmentInProgress = true;
            ResetAutoRefreshTimer();

            try
            {
                string confirmMsg = BuildConfirmMessage();
                var result = MessageBox.Show(confirmMsg, "Confirm Fulfillment",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                if (CommitExchange())
                {
                    // Build transmittal VM before clearing the selection
                    var transmittalVm = BuildTransmittalViewModel();
                    OnClear();
                    // Notify the View — it will show the print dialog then navigate to Send Notifications
                    FulfillmentCompleted?.Invoke(this, transmittalVm);
                }
            }
            finally
            {
                _fulfillmentInProgress = false;
                ResetAutoRefreshTimer();
            }
        }

        private CartridgeTransmittalViewModel BuildTransmittalViewModel()
        {
            if (ExchangePanel.IsMultiModelMode)
            {
                var reqs = ExchangePanel.MultiModelRows
                    .Select(r => _allRequests.FirstOrDefault(req => req.ReqId == r.ReqId))
                    .Where(r => r != null)
                    .ToList();
                return CartridgeTransmittalViewModel.FromMultiModel(reqs);
            }
            return CartridgeTransmittalViewModel.FromRequest(_selectedRequest);
        }

        private void OnClear()
        {
            _selectedRequest = null;
            ExchangePanel.Clear();
        }

        private void OnForceDelete()
        {
            if (_selectedRequest == null) return;

            var confirm = MessageBox.Show(
                $"🚨 FINAL WARNING — FORCE DELETE 🚨\n\n" +
                $"This will PERMANENTLY DELETE cartridge request #{_selectedRequest.ReqId}.\n\n" +
                "This will also:\n" +
                "• Restore issued cartridge stock (if any)\n" +
                "• Remove auto-registered returned empty cartridges (if any)\n" +
                "• Delete child rows in dbo.CartridgeRequestModel (if any)\n\n" +
                "THIS CANNOT BE UNDONE!\n\n" +
                "Only proceed if this was a DATA ENTRY ERROR.\n\n" +
                "Click 'OK' to confirm.",
                "⚠ FORCE DELETE CONFIRMATION ⚠",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Stop);

            if (confirm != MessageBoxResult.OK) return;

            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            var (success, message) = _repo.ForceDeleteCartridgeRequest(_selectedRequest.ReqId, userId);

            if (!success)
            {
                MessageBox.Show(message, "Force Delete Failed",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            MessageBox.Show(message, "Force Delete Completed",
                MessageBoxButton.OK, MessageBoxImage.Warning);

            OnClear();
            _ = LoadAsync(resetPage: false);
        }

        // ── Validation ────────────────────────────────────────────────────────────
        private bool ValidateExchange()
        {
            if (ExchangePanel.IsMultiModelMode)
            {
                var errors = new List<string>();
                foreach (var row in ExchangePanel.MultiModelRows)
                {
                    int total = row.IssuedBrandNewQty + row.IssuedRefilledQty;
                    if (row.IssuedBrandNewQty < 0) errors.Add($"Req #{row.ReqId} {row.DisplayModel}: Brand New qty cannot be negative.");
                    if (row.IssuedRefilledQty  < 0) errors.Add($"Req #{row.ReqId} {row.DisplayModel}: Refilled qty cannot be negative.");
                    if (total > row.RequestedQty)   errors.Add($"Req #{row.ReqId} {row.DisplayModel}: Issued ({total}) exceeds requested ({row.RequestedQty}).");
                }
                if (errors.Any())
                {
                    MessageBox.Show("Validation failed:\n\n• " + string.Join("\n• ", errors),
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
                return true;
            }

            var singleErrors = new List<string>();
            int issuedBn  = ExchangePanel.IssuedBrandNewQty;
            int issuedRf  = ExchangePanel.IssuedRefilledQty;
            int total2    = issuedBn + issuedRf;
            int requested = ExchangePanel.RequestedQty;

            if (issuedBn < 0) singleErrors.Add("Brand New quantity cannot be negative.");
            if (issuedRf < 0) singleErrors.Add("Refilled quantity cannot be negative.");
            if (total2 > requested)
                singleErrors.Add($"Total issued ({total2}) must not exceed requested ({requested}).");

            if (singleErrors.Any())
            {
                MessageBox.Show("Validation failed:\n\n• " + string.Join("\n• ", singleErrors),
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        // ── Exchange commit ───────────────────────────────────────────────────────
        private bool CommitExchange()
        {
            try
            {
                int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

                if (ExchangePanel.IsMultiModelMode)
                    return CommitMultiModel(userId);

                return CommitSingleModel(userId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error committing exchange:\n\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool CommitSingleModel(int userId)
        {
            int issuedBn  = ExchangePanel.IssuedBrandNewQty;
            int issuedRf  = ExchangePanel.IssuedRefilledQty;
            int total     = issuedBn + issuedRf;
            int requested = _selectedRequest.Quantity;

            if (total > requested)
            {
                MessageBox.Show(
                    $"Cannot issue more than requested.\n\nRequested: {requested}\nAttempting: {total}",
                    "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            string model = _selectedRequest.TypedModelNumber ?? _selectedRequest.ModelNumber ?? "N/A";
            int? modelId = ResolveModelId(model, _selectedRequest.CartridgeModelId);

            var bnIds = issuedBn > 0
                ? (_repo.GetIssuableItemIdsByCondition(modelId, "Brand New", issuedBn) ?? new List<int>())
                : new List<int>();
            var rfIds = issuedRf > 0
                ? (_repo.GetIssuableItemIdsByCondition(modelId, "Refilled", issuedRf) ?? new List<int>())
                : new List<int>();

            string remarks = CartridgeExchangeRemarks.GenerateRemarks(total, requested, model);

            _repo.FulfillCartridgeExchangeByCondition(
                _selectedRequest.ReqId, requested,
                bnIds, rfIds, issuedBn, issuedRf,
                userId, _selectedRequest.RequestModelId, remarks);

            // Notify the portal requester of the fulfillment outcome
            string notifType, notifTitle, notifMsg;
            if (total == 0)
            {
                notifType  = NotificationType.RequestUnfulfilled;
                notifTitle = "Request Unfulfilled";
                notifMsg   = $"Your cartridge request (Req #{_selectedRequest.ReqId}) could not be fulfilled due to insufficient stock.";
            }
            else if (total < requested)
            {
                notifType  = NotificationType.RequestPartiallyFulfilled;
                notifTitle = "Request Partially Fulfilled";
                notifMsg   = $"Your cartridge request (Req #{_selectedRequest.ReqId}) was partially fulfilled: {total} of {requested} issued.";
            }
            else
            {
                notifType  = NotificationType.RequestFulfilled;
                notifTitle = "Request Fulfilled";
                notifMsg   = $"Your cartridge request (Req #{_selectedRequest.ReqId}) has been fulfilled in full.";
            }
            try
            {
                SendFulfillmentNotification(
                    _selectedRequest.EmpId,
                    _selectedRequest.SubmissionSessionId,
                    notifType, notifTitle, notifMsg);
            }
            catch { /* notification is non-critical */ }

            return true;
        }

        private bool CommitMultiModel(int userId)
        {
            foreach (var row in ExchangePanel.MultiModelRows)
            {
                var req = _allRequests.FirstOrDefault(r => r.ReqId == row.ReqId);
                if (req == null) continue;

                int issuedBn = row.IssuedBrandNewQty;
                int issuedRf = row.IssuedRefilledQty;
                int total    = issuedBn + issuedRf;
                int requested = req.Quantity;

                if (total > requested)
                {
                    string m = req.TypedModelNumber ?? req.ModelNumber ?? "Unknown";
                    MessageBox.Show(
                        $"Cannot issue more than requested for model {m}.\n\nRequested: {requested}, Attempting: {total}",
                        "Invalid Quantity", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                string model = req.TypedModelNumber ?? req.ModelNumber ?? "N/A";
                int? modelId = ResolveModelId(model, req.CartridgeModelId);

                var bnIds = issuedBn > 0
                    ? (_repo.GetIssuableItemIdsByCondition(modelId, "Brand New", issuedBn) ?? new List<int>())
                    : new List<int>();
                var rfIds = issuedRf > 0
                    ? (_repo.GetIssuableItemIdsByCondition(modelId, "Refilled", issuedRf) ?? new List<int>())
                    : new List<int>();

                string remarks = CartridgeExchangeRemarks.GenerateRemarks(total, requested, model);

                _repo.FulfillCartridgeExchangeByCondition(
                    req.ReqId, requested,
                    bnIds, rfIds, issuedBn, issuedRf,
                    userId, req.RequestModelId, remarks);
            }

            // Notify the portal requester based on overall multi-model outcome.
            // Use the anchor request's EmpId + SubmissionSessionId — same proven path as the web app.
            int totalIssued    = ExchangePanel.MultiModelRows.Sum(r2 => r2.IssuedBrandNewQty + r2.IssuedRefilledQty);
            int totalRequested = ExchangePanel.MultiModelRows.Sum(r2 =>
            {
                var req2 = _allRequests.FirstOrDefault(r3 => r3.ReqId == r2.ReqId);
                return req2?.Quantity ?? 0;
            });
            var anchorReq = ExchangePanel.MultiModelRows.Count > 0
                ? _allRequests.FirstOrDefault(r2 => r2.ReqId == ExchangePanel.MultiModelRows[0].ReqId)
                : null;
            if (anchorReq != null)
            {
                string mNotifType, mNotifTitle, mNotifMsg;
                if (totalIssued == 0)
                {
                    mNotifType  = NotificationType.RequestUnfulfilled;
                    mNotifTitle = "Request Unfulfilled";
                    mNotifMsg   = "Your cartridge request could not be fulfilled due to insufficient stock.";
                }
                else if (totalIssued < totalRequested)
                {
                    mNotifType  = NotificationType.RequestPartiallyFulfilled;
                    mNotifTitle = "Request Partially Fulfilled";
                    mNotifMsg   = $"Your cartridge request was partially fulfilled: {totalIssued} of {totalRequested} units issued across all models.";
                }
                else
                {
                    mNotifType  = NotificationType.RequestFulfilled;
                    mNotifTitle = "Request Fulfilled";
                    mNotifMsg   = "Your cartridge request has been fulfilled in full.";
                }
                try
                {
                    SendFulfillmentNotification(
                        anchorReq.EmpId,
                        anchorReq.SubmissionSessionId,
                        mNotifType, mNotifTitle, mNotifMsg);
                }
                catch { /* notification is non-critical */ }
            }
            return true;
        }

        // ── Fulfillment notification ──────────────────────────────────────────────
        private void SendFulfillmentNotification(int empId, Guid? sessionId, string notificationType, string title, string message)
        {
            try
            {
                var notifRepo = new NotificationRepository();
                var (userId, authId) = notifRepo.GetUserAndAuthIdForFulfillment(empId, sessionId);
                if (userId == null)
                {
                    Debug.WriteLine($"[SendFulfillmentNotification] No user found — EmpId={empId}, SessionId={sessionId} — notification skipped.");
                    return;
                }

                notifRepo.Create(new NotificationCreateDto
                {
                    UserId           = userId.Value,
                    Title            = title,
                    Message          = message,
                    NotificationType = notificationType,
                    ReferenceId      = authId
                });
                Debug.WriteLine($"[SendFulfillmentNotification] Sent: UserId={userId}, AuthId={authId}, Type={notificationType}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SendFulfillmentNotification] Failed for EmpId={empId}: {ex.Message}");
            }
        }

        private int? ResolveModelId(string modelStr, int? dtoCmid)
        {
            int? id = !string.IsNullOrWhiteSpace(modelStr)
                ? _repo.GetCartridgeModelIdByModelNumber(modelStr)
                : null;
            if (!id.HasValue && dtoCmid.HasValue && dtoCmid.Value > 0)
                id = dtoCmid;
            return id;
        }

        // ── Post-fulfillment confirmation dialog ──────────────────────────────────
        private string BuildConfirmMessage()
        {
            if (ExchangePanel.IsMultiModelMode)
            {
                var rows = ExchangePanel.MultiModelRows;
                var list = string.Join("\n", rows.Select(r =>
                    $"  • Req #{r.ReqId} {r.DisplayModel}: {r.IssuedFullQty} / {r.RequestedQty} pcs"));
                return $"Fulfill all {rows.Count} models in this session?\n\n{list}\n\nThis will record cartridge movements for all models above.";
            }

            return $"Fulfill request #{_selectedRequest.ReqId}?\n\n" +
                   "This will:\n• Record cartridge movements\n• Update cartridge statuses\n" +
                   "• Mark request as fulfilled\n• Group with related multi-model requests (if applicable)";
        }

        private void FinalizeAndRefresh()
        {
            OnClear();
            _ = LoadAsync(resetPage: false);
        }

        // ── Auto-refresh timer ────────────────────────────────────────────────────
        private void StartAutoRefreshTimer()
        {
            _autoRefreshSecondsLeft = 30;
            _autoRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _autoRefreshTimer.Tick += AutoRefreshTick;
            _autoRefreshTimer.Start();
        }

        private void ResetAutoRefreshTimer()
        {
            if (_autoRefreshTimer == null) return;
            _autoRefreshSecondsLeft = 30;
            UpdateAutoRefreshText(paused: false);
            _autoRefreshTimer.Stop();
            _autoRefreshTimer.Start();
        }

        private void AutoRefreshTick(object sender, EventArgs e)
        {
            if (_selectedRequest != null || _fulfillmentInProgress || _isLoading)
            {
                UpdateAutoRefreshText(paused: true);
                return;
            }

            _autoRefreshSecondsLeft--;
            UpdateAutoRefreshText(paused: false);

            if (_autoRefreshSecondsLeft <= 0)
            {
                _autoRefreshSecondsLeft = 30;
                _ = LoadAsync(resetPage: false);
            }
        }

        private void UpdateAutoRefreshText(bool paused)
        {
            AutoRefreshText = paused
                ? "⏸ paused"
                : $"⏱ {_autoRefreshSecondsLeft}s";
        }

        // ── IDisposable ───────────────────────────────────────────────────────────
        public void Dispose()
        {
            _autoRefreshTimer?.Stop();
            _buildCts?.Cancel();
            _buildCts?.Dispose();
        }
    }
}
