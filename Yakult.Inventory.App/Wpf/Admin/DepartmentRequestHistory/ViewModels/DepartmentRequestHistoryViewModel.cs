using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.RequestPortal.RequestHistory.ViewModels;

namespace Yakult.Inventory.App.WPF.Admin.DepartmentRequestHistory.ViewModels
{
    /// <summary>
    /// Admin Portal → Account Management → Department Request History.
    /// Request Portal submissions per Company / Branch / Department, both Dept. Level (no
    /// employee) and employees' own, whether or not a department account exists yet. Uses
    /// the same department rule as a department account's Request History, so this is exactly
    /// what that account sees (or will see once created).
    /// </summary>
    public class DepartmentRequestHistoryViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private readonly DepartmentRequestHistoryRepository _repo = new DepartmentRequestHistoryRepository();
        private List<DepartmentRequestHistoryRowViewModel> _allRows      = new List<DepartmentRequestHistoryRowViewModel>();
        private List<DepartmentRequestHistoryRowViewModel> _filteredRows = new List<DepartmentRequestHistoryRowViewModel>();
        private bool _optionsLoaded;

        // Fixed department, set when opened from a Department Accounts row.
        private readonly int? _lockedComId, _lockedBranchId, _lockedDeptId;

        public DepartmentRequestHistoryViewModel() : this(null, null, null) { }

        public DepartmentRequestHistoryViewModel(int? lockedComId, int? lockedBranchId, int? lockedDeptId)
        {
            _lockedComId    = lockedComId;
            _lockedBranchId = lockedBranchId;
            _lockedDeptId   = lockedDeptId;

            LoadCommand         = new RelayCommand(async () => await LoadAsync(), () => !_isLoading);
            ClearFiltersCommand = new RelayCommand(async () => await ClearFiltersAsync(), () => !_isLoading);
            PreviousPageCommand = new RelayCommand(() => ApplyPage(_currentPage - 1), () => CanGoPrev);
            NextPageCommand     = new RelayCommand(() => ApplyPage(_currentPage + 1), () => CanGoNext);
            ToggleCardCommand   = new RelayCommand<string>(ToggleCard);
        }

        public ICommand ToggleCardCommand { get; }

        public bool IsScopeLocked  => _lockedComId.HasValue && _lockedBranchId.HasValue && _lockedDeptId.HasValue;
        public bool CanChangeScope => !IsScopeLocked;

        // ── Filters ───────────────────────────────────────────────────────────
        public ObservableCollection<DeptScopeOption> Companies   { get; } = new ObservableCollection<DeptScopeOption>();
        public ObservableCollection<DeptScopeOption> Branches    { get; } = new ObservableCollection<DeptScopeOption>();
        public ObservableCollection<DeptScopeOption> Departments { get; } = new ObservableCollection<DeptScopeOption>();

        public List<string> RequesterTypes { get; } = new List<string> { AllRequests, DeptLevelOnly, EmployeesOnly };
        private const string AllRequests   = "All requests";
        private const string DeptLevelOnly = "Dept. Level only";
        private const string EmployeesOnly = "Employees only";

        private DeptScopeOption _selectedCompany, _selectedBranch, _selectedDepartment;
        public DeptScopeOption SelectedCompany    { get => _selectedCompany;    set => SetField(ref _selectedCompany, value); }
        public DeptScopeOption SelectedBranch     { get => _selectedBranch;     set => SetField(ref _selectedBranch, value); }
        public DeptScopeOption SelectedDepartment { get => _selectedDepartment; set => SetField(ref _selectedDepartment, value); }

        private string _selectedRequesterType = AllRequests;
        public string SelectedRequesterType { get => _selectedRequesterType; set => SetField(ref _selectedRequesterType, value); }

        private DateTime? _fromDate, _toDate;
        public DateTime? FromDate { get => _fromDate; set => SetField(ref _fromDate, value); }
        public DateTime? ToDate   { get => _toDate;   set => SetField(ref _toDate, value); }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplySearch(); }
        }

        // ── State ─────────────────────────────────────────────────────────────
        public ObservableCollection<DepartmentRequestHistoryRowViewModel> PagedRows { get; }
            = new ObservableCollection<DepartmentRequestHistoryRowViewModel>();

        private DepartmentRequestHistoryRowViewModel _selectedRow;
        public DepartmentRequestHistoryRowViewModel SelectedRow { get => _selectedRow; set => SetField(ref _selectedRow, value); }

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private string _statusMessage = string.Empty;
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        private int _totalCount, _deptLevelCount, _employeeCount, _noAccountCount;
        public int TotalCount     { get => _totalCount;     private set => SetField(ref _totalCount, value); }
        public int DeptLevelCount { get => _deptLevelCount; private set => SetField(ref _deptLevelCount, value); }
        public int EmployeeCount  { get => _employeeCount;  private set => SetField(ref _employeeCount, value); }
        public int NoAccountCount { get => _noAccountCount; private set => SetField(ref _noAccountCount, value); }
        // Rows after the card filter; drives the grid / empty-state visibility.
        private int _shownCount;
        public int ShownCount { get => _shownCount; private set { if (SetField(ref _shownCount, value)) OnPropertyChanged(nameof(HasRows)); } }
        public bool HasRows => _shownCount > 0;

        private string _scopeTitle = "All departments";
        public string ScopeTitle { get => _scopeTitle; private set => SetField(ref _scopeTitle, value); }

        private int _currentPage = 1, _totalPages = 1;
        public string PageInfo  => $"Page {_currentPage} of {_totalPages}";

        // e.g. "Showing 21–40 of 57 submissions" (after search and card filter).
        public string RowInfo
        {
            get
            {
                if (_shownCount == 0) return "No submissions";
                int start = (_currentPage - 1) * PageSize + 1;
                int end   = Math.Min(_currentPage * PageSize, _shownCount);
                string noun = _shownCount == 1 ? "submission" : "submissions";
                return $"Showing {start}–{end} of {_shownCount} {noun}";
            }
        }
        public bool   CanGoPrev => _currentPage > 1;
        public bool   CanGoNext => _currentPage < _totalPages;

        public ICommand LoadCommand         { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand PreviousPageCommand { get; }
        public ICommand NextPageCommand     { get; }

        // ── Loading ───────────────────────────────────────────────────────────

        public async Task LoadAsync()
        {
            IsLoading     = true;
            StatusMessage = "Loading...";
            try
            {
                if (!_optionsLoaded) await LoadFilterOptionsAsync();

                string requesterType =
                    _selectedRequesterType == DeptLevelOnly ? DepartmentRequestHistoryRepository.RequesterDeptLevel :
                    _selectedRequesterType == EmployeesOnly ? DepartmentRequestHistoryRepository.RequesterEmployee  :
                                                              DepartmentRequestHistoryRepository.RequesterAll;

                var lines = await _repo.GetRequestLinesAsync(
                    _selectedCompany?.Id, _selectedBranch?.Id, _selectedDepartment?.Id,
                    requesterType, _fromDate, _toDate);

                _allRows = lines
                    .GroupBy(l => l.SubmissionSessionId.HasValue ? l.SubmissionSessionId.Value.ToString() : "R" + l.ReqId)
                    .Select(g => DepartmentRequestHistoryRowViewModel.Build(g.ToList()))
                    .OrderByDescending(r => r.DateRequested)
                    .ToList();

                ScopeTitle    = BuildScopeTitle();
                StatusMessage = string.Empty;
                ApplySearch();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading history: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadFilterOptionsAsync()
        {
            var (companies, branches, departments) = await _repo.GetFilterOptionsAsync();
            Fill(Companies,   companies,   "All companies");
            Fill(Branches,    branches,    "All branches");
            Fill(Departments, departments, "All departments");

            SelectedCompany    = Pick(Companies,   _lockedComId);
            SelectedBranch     = Pick(Branches,    _lockedBranchId);
            SelectedDepartment = Pick(Departments, _lockedDeptId);
            _optionsLoaded = true;
        }

        private static void Fill(ObservableCollection<DeptScopeOption> target, List<DeptScopeOption> source, string allLabel)
        {
            target.Clear();
            target.Add(new DeptScopeOption { Id = null, Name = allLabel });
            foreach (var o in source) target.Add(o);
        }

        private static DeptScopeOption Pick(ObservableCollection<DeptScopeOption> options, int? id) =>
            options.FirstOrDefault(o => o.Id == id) ?? options.FirstOrDefault();

        private async Task ClearFiltersAsync()
        {
            if (!IsScopeLocked)
            {
                SelectedCompany    = Companies.FirstOrDefault();
                SelectedBranch     = Branches.FirstOrDefault();
                SelectedDepartment = Departments.FirstOrDefault();
            }
            SelectedRequesterType = AllRequests;
            if (_activeCard != null) ToggleCard(_activeCard);
            FromDate   = null;
            ToDate     = null;
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            await LoadAsync();
        }

        private string BuildScopeTitle()
        {
            var parts = new[] { _selectedCompany, _selectedDepartment, _selectedBranch }
                .Where(o => o?.Id != null)
                .Select(o => o.Name)
                .ToList();
            return parts.Count == 0 ? "All departments" : string.Join(" / ", parts);
        }

        // ── Search + paging ───────────────────────────────────────────────────

        // ── Summary card filter (click a card to filter, click it again to clear) ──

        public const string CardAll       = "All";
        public const string CardDeptLevel = "DeptLevel";
        public const string CardEmployee  = "Employee";
        public const string CardNoAccount = "NoAccount";

        private string _activeCard;   // null = no card filter
        public bool IsAllCardActive       => _activeCard == CardAll;
        public bool IsDeptLevelCardActive => _activeCard == CardDeptLevel;
        public bool IsEmployeeCardActive  => _activeCard == CardEmployee;
        public bool IsNoAccountCardActive => _activeCard == CardNoAccount;

        private void ToggleCard(string card)
        {
            _activeCard = _activeCard == card ? null : card;
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsDeptLevelCardActive));
            OnPropertyChanged(nameof(IsEmployeeCardActive));
            OnPropertyChanged(nameof(IsNoAccountCardActive));
            ApplySearch();
        }

        private void ApplySearch()
        {
            string q = (_searchText ?? string.Empty).Trim();
            var searched = string.IsNullOrEmpty(q)
                ? _allRows
                : _allRows.Where(r => r.Matches(q)).ToList();

            // Card counts ignore the card filter, so every card keeps its number.
            TotalCount     = searched.Count;
            DeptLevelCount = searched.Count(r => r.IsDeptLevel);
            EmployeeCount  = searched.Count(r => !r.IsDeptLevel);
            NoAccountCount = searched.Count(r => !r.HasDeptAccount);

            switch (_activeCard)
            {
                case CardDeptLevel: _filteredRows = searched.Where(r => r.IsDeptLevel).ToList();     break;
                case CardEmployee:  _filteredRows = searched.Where(r => !r.IsDeptLevel).ToList();    break;
                case CardNoAccount: _filteredRows = searched.Where(r => !r.HasDeptAccount).ToList(); break;
                default:            _filteredRows = searched;                                         break;
            }
            ShownCount = _filteredRows.Count;

            _totalPages = Math.Max(1, (int)Math.Ceiling(_filteredRows.Count / (double)PageSize));
            _currentPage = 0; // force ApplyPage to refresh
            ApplyPage(1);
            OnPropertyChanged(nameof(RowInfo));   // also changes when the list is empty (ApplyPage still runs)
        }

        private void ApplyPage(int page)
        {
            if (page < 1 || page > _totalPages) return;
            _currentPage = page;

            PagedRows.Clear();
            foreach (var r in _filteredRows.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            OnPropertyChanged(nameof(PageInfo));
            OnPropertyChanged(nameof(RowInfo));
            OnPropertyChanged(nameof(CanGoPrev));
            OnPropertyChanged(nameof(CanGoNext));
            ((RelayCommand)PreviousPageCommand).RaiseCanExecuteChanged();
            ((RelayCommand)NextPageCommand).RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// One submission (a SubmissionSessionId) on the Department Request History page. Extends
    /// the Request Portal's history row so both show the same columns and status colours.
    /// </summary>
    public class DepartmentRequestHistoryRowViewModel : RequestHistoryRowViewModel
    {
        public string CompanyName    { get; set; }
        public string DepartmentName { get; set; }
        public string EncodedBy      { get; set; }
        public string Source         { get; set; }
        public string DeptAccount    { get; set; }
        public bool   HasDeptAccount { get; set; }
        public string Remarks        { get; set; }
        public List<DepartmentRequestLineViewModel> Lines { get; set; }

        public string DeptAccountDisplay => HasDeptAccount ? DeptAccount : "No account yet";

        internal static DepartmentRequestHistoryRowViewModel Build(List<DepartmentRequestLineDto> lines)
        {
            var first = lines[0];
            int good    = lines.Sum(l => l.GoodEmptyQty);
            int damaged = lines.Sum(l => l.DamagedEmptyQty);

            return new DepartmentRequestHistoryRowViewModel
            {
                SetCode           = string.IsNullOrWhiteSpace(first.SetCode) ? "—" : first.SetCode,
                DateRequested     = first.DateRequested,
                RequestedFor      = first.EmployeeName ?? "Dept. Level",   // drives IsDeptLevel
                CartridgeDisplay  = string.Join(", ", lines.Select(l => l.CartridgeModel ?? l.ItemName ?? "—").Distinct()),
                TotalQty          = lines.Sum(l => l.Quantity),
                ReturnInfo        = good > 0 || damaged > 0 ? $"G:{good} D:{damaged}" : string.Empty,
                FulfillmentMethod = ExtractFulfillmentMethod(first.Description),
                CompanyName       = first.CompanyName    ?? "—",
                DepartmentName    = first.DepartmentName ?? "—",
                DestinationBranch = first.BranchName     ?? "—",
                Status            = RequestHistoryViewModel.AggregateGroupStatus(lines.Select(l => l.Status).ToList()),
                EncodedBy         = first.CreatedByName ?? "—",
                Source            = first.RequestSource == "PORTAL_ASSISTED" ? "IT Assisted" : "Portal",
                DeptAccount       = first.DeptAccountUsername,
                HasDeptAccount    = !string.IsNullOrEmpty(first.DeptAccountUsername),
                Remarks           = first.Remarks,
                Lines             = lines.Select(l => new DepartmentRequestLineViewModel
                {
                    Item    = l.CartridgeModel ?? l.ItemName ?? "—",
                    Qty     = l.Quantity,
                    Empties = l.GoodEmptyQty > 0 || l.DamagedEmptyQty > 0 ? $"G:{l.GoodEmptyQty} D:{l.DamagedEmptyQty}" : "—",
                    Status  = l.Status
                }).ToList()
            };
        }

        private static string ExtractFulfillmentMethod(string description)
        {
            string d = description?.ToUpperInvariant() ?? string.Empty;
            if (d.Contains("PICKUP"))   return "PICKUP";
            if (d.Contains("DELIVERY")) return "DELIVERY";
            return "—";
        }

        internal bool Matches(string q) =>
            Contains(SetCode, q) || Contains(RequestedFor, q) || Contains(CartridgeDisplay, q) ||
            Contains(EncodedBy, q) || Contains(CompanyName, q) || Contains(DepartmentName, q) ||
            Contains(DestinationBranch, q) || Contains(Status, q) || Contains(DeptAccountDisplay, q);

        private static bool Contains(string value, string q) =>
            value != null && value.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public class DepartmentRequestLineViewModel
    {
        public string Item    { get; set; }
        public int    Qty     { get; set; }
        public string Empties { get; set; }
        public string Status  { get; set; }
    }
}
