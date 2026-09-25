using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.RequestManagement.ViewModels
{
    public class UnfulfilledRequestsViewModel : ViewModelBase, IDisposable
    {
        private const int PageSize = 10;

        private readonly RequestRepository _repository;
        private readonly CartridgeManagementRepository _cartridgeRepository = new CartridgeManagementRepository();

        private List<RequestDto> _allRequests = new List<RequestDto>();

        private List<RequestSessionGroupViewModel> _allSessions      = new List<RequestSessionGroupViewModel>();
        private List<RequestSessionGroupViewModel> _filteredSessions = new List<RequestSessionGroupViewModel>();

        private string _searchText  = string.Empty;
        private string _filterBy    = "All Fields";
        private string _sortBy      = "Set # (A→Z)";
        private bool   _isLoading;
        private int    _currentPage = 1;
        private int    _totalPages  = 1;
        private string _pageInfoText = string.Empty;

        private int    _pendingRecords;
        private int    _unfulfilledQty;

        private enum CardSortMode { Default, ByUnfulfilledQtyDesc }
        private CardSortMode _cardSortMode = CardSortMode.Default;

        public bool IsPendingRecordsCardActive => _cardSortMode == CardSortMode.Default;
        public bool IsUnfulfilledQtyCardActive => _cardSortMode == CardSortMode.ByUnfulfilledQtyDesc;

        public ICommand PendingRecordsCardCommand { get; }
        public ICommand UnfulfilledQtyCardCommand { get; }

        private void SetCardSortMode(CardSortMode mode)
        {
            _cardSortMode = mode;
            OnPropertyChanged(nameof(IsPendingRecordsCardActive));
            OnPropertyChanged(nameof(IsUnfulfilledQtyCardActive));
            ApplyFilter();
        }

        private RequestFulfillmentRowViewModel _selectedRow;
        private string _remarksText = "Click a row to view remarks";

        public ObservableCollection<RequestSessionGroupViewModel> PagedSessions { get; }
            = new ObservableCollection<RequestSessionGroupViewModel>();

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string FilterBy
        {
            get => _filterBy;
            set { if (SetField(ref _filterBy, value)) ApplyFilter(); }
        }

        public string SortBy
        {
            get => _sortBy;
            set { if (SetField(ref _sortBy, value)) ApplyFilter(); }
        }

        // ── Set-linked filters (SetCode, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) ──────────────────────────────────────────────
        // Unlike SearchText/FilterBy/SortBy above, these aren't fields on the already-loaded
        // RequestDto rows — they require a server round trip through the repository's CTE, so
        // typing triggers a debounced full LoadData() reload instead of the client-side ApplyFilter().
        private readonly DispatcherTimer _setFilterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };

        private void QueueSetFilterReload()
        {
            _setFilterDebounceTimer.Stop();
            _setFilterDebounceTimer.Start();
        }

        private string _setCodeFilter = string.Empty;
        public string SetCodeFilter
        {
            get => _setCodeFilter;
            set { if (SetField(ref _setCodeFilter, value)) QueueSetFilterReload(); }
        }

        private string _documentNumberFilter = string.Empty;
        public string DocumentNumberFilter
        {
            get => _documentNumberFilter;
            set { if (SetField(ref _documentNumberFilter, value)) QueueSetFilterReload(); }
        }

        private string _companyFilter = string.Empty;
        public string CompanyFilter
        {
            get => _companyFilter;
            set { if (SetField(ref _companyFilter, value)) QueueSetFilterReload(); }
        }

        private string _departmentFilter = string.Empty;
        public string DepartmentFilter
        {
            get => _departmentFilter;
            set { if (SetField(ref _departmentFilter, value)) QueueSetFilterReload(); }
        }

        private string _branchFilter = string.Empty;
        public string BranchFilter
        {
            get => _branchFilter;
            set { if (SetField(ref _branchFilter, value)) QueueSetFilterReload(); }
        }

        private string _employeeFilter = string.Empty;
        public string EmployeeFilter
        {
            get => _employeeFilter;
            set { if (SetField(ref _employeeFilter, value)) QueueSetFilterReload(); }
        }

        private string _referenceCodeFilter = string.Empty;
        public string ReferenceCodeFilter
        {
            get => _referenceCodeFilter;
            set { if (SetField(ref _referenceCodeFilter, value)) QueueSetFilterReload(); }
        }

        private string _parentTagFilter = string.Empty;
        public string ParentTagFilter
        {
            get => _parentTagFilter;
            set { if (SetField(ref _parentTagFilter, value)) QueueSetFilterReload(); }
        }

        private string _reqIdFilter = string.Empty;
        public string ReqIdFilter
        {
            get => _reqIdFilter;
            set { if (SetField(ref _reqIdFilter, value)) QueueSetFilterReload(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetField(ref _isLoading, value);
        }

        public int    CurrentPage  { get => _currentPage;  set => SetField(ref _currentPage, value); }
        public int    TotalPages   { get => _totalPages;   set => SetField(ref _totalPages, value); }
        public string PageInfoText { get => _pageInfoText; set => SetField(ref _pageInfoText, value); }

        public int PendingRecords
        {
            get => _pendingRecords;
            set => SetField(ref _pendingRecords, value);
        }

        public int UnfulfilledQty
        {
            get => _unfulfilledQty;
            set => SetField(ref _unfulfilledQty, value);
        }

        public RequestFulfillmentRowViewModel SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                {
                    OnPropertyChanged(nameof(CanFulfill));
                    UpdateRemarks();
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string RemarksText
        {
            get => _remarksText;
            set => SetField(ref _remarksText, value);
        }

        public bool CanFulfill => _selectedRow?.IsPending == true;

        public bool CanGoFirst => _currentPage > 1;
        public bool CanGoPrev  => _currentPage > 1;
        public bool CanGoNext  => _currentPage < _totalPages;
        public bool CanGoLast  => _currentPage < _totalPages;

        public ICommand RefreshCommand         { get; }
        public ICommand FulfillSelectedCommand { get; }
        public ICommand FirstPageCommand       { get; }
        public ICommand PrevPageCommand        { get; }
        public ICommand NextPageCommand        { get; }
        public ICommand LastPageCommand        { get; }

        public event Action<List<RequestFulfillmentRowViewModel>> FulfillRequested;

        public UnfulfilledRequestsViewModel()
        {
            _repository = new RequestRepository();

            RefreshCommand         = new RelayCommand(LoadData);
            FulfillSelectedCommand = new RelayCommand(OnFulfillSelected, () => CanFulfill);
            FirstPageCommand       = new RelayCommand(() => NavigateTo(1),              () => CanGoFirst);
            PrevPageCommand        = new RelayCommand(() => NavigateTo(_currentPage - 1), () => CanGoPrev);
            NextPageCommand        = new RelayCommand(() => NavigateTo(_currentPage + 1), () => CanGoNext);
            LastPageCommand        = new RelayCommand(() => NavigateTo(_totalPages),    () => CanGoLast);

            PendingRecordsCardCommand = new RelayCommand(() => SetCardSortMode(CardSortMode.Default));
            UnfulfilledQtyCardCommand = new RelayCommand(() => SetCardSortMode(
                _cardSortMode == CardSortMode.ByUnfulfilledQtyDesc ? CardSortMode.Default : CardSortMode.ByUnfulfilledQtyDesc));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _setFilterDebounceTimer.Tick += (s, e) => { _setFilterDebounceTimer.Stop(); LoadData(); };
        }

        public ICommand ResetFiltersCommand { get; }

        /// <summary>Raised so the View can reset the code-behind-driven Filter/Sort ComboBoxes
        /// (they aren't bound via SelectedItem, just SelectionChanged) back to index 0.</summary>
        public event Action FiltersReset;

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _filterBy = "All Fields";
            OnPropertyChanged(nameof(FilterBy));

            _sortBy = "Set # (A→Z)";
            OnPropertyChanged(nameof(SortBy));

            _cardSortMode = CardSortMode.Default;
            OnPropertyChanged(nameof(IsPendingRecordsCardActive));
            OnPropertyChanged(nameof(IsUnfulfilledQtyCardActive));

            _setFilterDebounceTimer.Stop();
            _setCodeFilter = string.Empty;
            _documentNumberFilter = string.Empty;
            _companyFilter = string.Empty;
            _departmentFilter = string.Empty;
            _branchFilter = string.Empty;
            _employeeFilter = string.Empty;
            _referenceCodeFilter = string.Empty;
            _parentTagFilter = string.Empty;
            _reqIdFilter = string.Empty;
            OnPropertyChanged(nameof(SetCodeFilter));
            OnPropertyChanged(nameof(DocumentNumberFilter));
            OnPropertyChanged(nameof(CompanyFilter));
            OnPropertyChanged(nameof(DepartmentFilter));
            OnPropertyChanged(nameof(BranchFilter));
            OnPropertyChanged(nameof(EmployeeFilter));
            OnPropertyChanged(nameof(ReferenceCodeFilter));
            OnPropertyChanged(nameof(ParentTagFilter));
            OnPropertyChanged(nameof(ReqIdFilter));

            FiltersReset?.Invoke();
            LoadData();
        }

        public void LoadData()
        {
            try
            {
                IsLoading   = true;
                SelectedRow = null;

                _allRequests = _repository.GetUnfulfilledRequestsFullSet(
                                   setCodeFilter: _setCodeFilter,
                                   documentNumberFilter: _documentNumberFilter,
                                   companyFilter: _companyFilter,
                                   departmentFilter: _departmentFilter,
                                   branchFilter: _branchFilter,
                                   employeeFilter: _employeeFilter,
                                   referenceCodeFilter: _referenceCodeFilter,
                                   parentTagFilter: _parentTagFilter,
                                   reqIdFilter: _reqIdFilter)
                               ?? new List<RequestDto>();

                _allSessions = BuildSessionViewModels(GroupIntoSessions(_allRequests));
                UpdateStats();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error loading data:\n\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void UpdateStats()
        {
            PendingRecords = _allRequests.Count;
            UnfulfilledQty = _allRequests.Sum(r => Math.Max(0, r.Quantity - r.IssuedQty));
        }

        private static List<SessionData> GroupIntoSessions(List<RequestDto> rows)
        {
            var result     = new List<SessionData>();
            var setMap     = new Dictionary<int, SessionData>();
            var sessionMap = new Dictionary<Guid, SessionData>();

            foreach (var row in rows)
            {
                if (row.SetId.HasValue && row.SetId.Value > 0)
                {
                    if (!setMap.TryGetValue(row.SetId.Value, out var grp))
                    {
                        grp = new SessionData { SetId = row.SetId, SessionId = row.SubmissionSessionId };
                        setMap[row.SetId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                else if (row.SubmissionSessionId.HasValue)
                {
                    if (!sessionMap.TryGetValue(row.SubmissionSessionId.Value, out var grp))
                    {
                        grp = new SessionData { SessionId = row.SubmissionSessionId };
                        sessionMap[row.SubmissionSessionId.Value] = grp;
                        result.Add(grp);
                    }
                    grp.Rows.Add(row);
                }
                else
                {
                    var grp = new SessionData();
                    grp.Rows.Add(row);
                    result.Add(grp);
                }
            }

            return result;
        }

        private static List<RequestSessionGroupViewModel> BuildSessionViewModels(List<SessionData> sessions)
        {
            return sessions.Select(s =>
            {
                var first = s.Rows.FirstOrDefault();
                return new RequestSessionGroupViewModel(
                    s.SetId,
                    first?.EmployeeName,
                    first?.BranchName,
                    first?.DepartmentName,
                    s.Rows);
            }).ToList();
        }

        private void ApplyFilter()
        {
            string search = _searchText?.Trim() ?? string.Empty;

            _filteredSessions = string.IsNullOrWhiteSpace(search)
                ? _allSessions.ToList()
                : _allSessions.Where(s => SessionMatchesSearch(s, search, _filterBy)).ToList();

            _filteredSessions = _cardSortMode == CardSortMode.ByUnfulfilledQtyDesc
                ? _filteredSessions.OrderByDescending(s => s.TotalPending).ToList()
                : _sortBy == "Set # (Z→A)"
                    ? _filteredSessions.OrderByDescending(s => s.SetId ?? int.MaxValue).ToList()
                    : _filteredSessions.OrderBy(s => s.SetId ?? int.MaxValue).ToList();

            _currentPage = 1;
            RebuildPage();
        }

        private static bool SessionMatchesSearch(RequestSessionGroupViewModel s, string search, string filterBy)
            => s.Rows.Any(r => RowMatchesSearch(r, search, filterBy));

        private static bool RowMatchesSearch(RequestFulfillmentRowViewModel r, string search, string filterBy)
        {
            bool Has(string v) => !string.IsNullOrEmpty(v)
                               && v.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0;
            switch (filterBy)
            {
                case "Requester":  return Has(r.Dto.EmployeeName);
                case "Branch":     return Has(r.Dto.BranchName);
                case "Department": return Has(r.Dto.DepartmentName);
                case "Item":       return Has(r.ItemName);
                case "Req #":      return Has(r.ReqId.ToString());
                default:
                    return Has(r.Dto.EmployeeName) || Has(r.Dto.BranchName)
                        || Has(r.Dto.DepartmentName) || Has(r.ItemName)
                        || Has(r.ReqId.ToString());
            }
        }

        private void NavigateTo(int page)
        {
            _currentPage = page;
            RebuildPage();
        }

        private void RebuildPage()
        {
            int total = _filteredSessions.Count;
            int pages = Math.Max(1, (int)Math.Ceiling((double)total / PageSize));
            _currentPage = Math.Max(1, Math.Min(_currentPage, pages));

            TotalPages   = pages;
            CurrentPage  = _currentPage;
            PageInfoText = total == 0
                ? "No results"
                : $"Page {_currentPage} of {pages}  ({total} sets)";

            PagedSessions.Clear();
            foreach (var s in _filteredSessions.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedSessions.Add(s);

            OnPropertyChanged(nameof(CanGoFirst));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(CanGoLast));
        }

        private void OnFulfillSelected()
        {
            if (_selectedRow == null || !_selectedRow.IsPending) return;

            var session = _filteredSessions
                .FirstOrDefault(s => s.Rows.Any(r => r == _selectedRow));

            var pendingRows = session?.Rows.Where(r => r.IsPending).ToList()
                           ?? new List<RequestFulfillmentRowViewModel> { _selectedRow };

            FulfillRequested?.Invoke(pendingRows);
        }

        public List<FulfillRequestRowStateViewModel> BuildFulfillStates(List<RequestFulfillmentRowViewModel> pendingRows)
        {
            var states = new List<FulfillRequestRowStateViewModel>();
            foreach (var row in pendingRows)
            {
                int available = 0;
                try { available = _repository.GetItemStockOnHand(row.Dto.ItemId); }
                catch { }

                // Cartridge lines of mixed portal submissions are issued as cartridge exchanges.
                MixedCartridgeLineInfo cartridge = null;
                try
                {
                    var info = _cartridgeRepository.GetMixedCartridgeLineInfo(row.Dto.ReqId);
                    if (info != null && info.IsExchangeLine) cartridge = info;
                }
                catch { }

                states.Add(new FulfillRequestRowStateViewModel(row.Dto, available, cartridge));
            }
            return states;
        }

        public void CommitFulfillment(List<FulfillRequestRowStateViewModel> states)
        {
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;
            foreach (var st in states)
            {
                if (st.IssueQty <= 0) continue;
                string remarks = string.IsNullOrWhiteSpace(st.Remarks) ? null : st.Remarks.Trim();
                if (st.IsExchangeLine)
                    _repository.FulfillCartridgeLine(st.Dto.ReqId, st.IssueBrandNewQty, st.IssueRefilledQty, userId, remarks);
                else
                    _repository.FulfillRequest(st.Dto.ReqId, st.IssueQty, userId, remarks);
            }
        }

        private void UpdateRemarks()
        {
            RemarksText = _selectedRow == null
                ? "Click a row to view remarks"
                : (string.IsNullOrWhiteSpace(_selectedRow.Remarks) ? "No remarks" : $"Remarks: {_selectedRow.Remarks}");
        }

        public void Dispose() { }

        private sealed class SessionData
        {
            public int?  SetId     { get; set; }
            public Guid? SessionId { get; set; }
            public List<RequestDto> Rows { get; } = new List<RequestDto>();
        }
    }
}
