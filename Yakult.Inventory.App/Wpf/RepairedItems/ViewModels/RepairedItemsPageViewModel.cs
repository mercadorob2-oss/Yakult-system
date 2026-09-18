using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.RepairedItems.ViewModels
{
    /// <summary>
    /// Business logic for the Repaired Items page, ported verbatim from
    /// Pages\Item\ViewRepairedItemsPage.cs (+ .State.cs, now deleted — its persistence logic
    /// is folded in here). Filter/search/sort/paging predicates, summary-count computation,
    /// and the settings-backed UI-state keys (RepairedItems_*) are unchanged. The three nested
    /// dialogs (MarkActionDialog, SellDisposeDialog, RepairHistoryDialog) were extracted to
    /// standalone WinForms classes under Pages\Item\ — unchanged internally — and stay WinForms,
    /// opened by the View via the RequestXxx bridge below.
    /// </summary>
    public sealed partial class RepairedItemsPageViewModel : ViewModelBase
    {
        private readonly string _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
        private readonly ItemRepairHistoryRepository _repairHistoryRepository = new ItemRepairHistoryRepository();
        private readonly ItemLifecycleDecisionRepository _lifecycleDecisionRepository = new ItemLifecycleDecisionRepository();

        private List<RepairedItemRow> _allRows = new List<RepairedItemRow>();
        private List<RepairedItemRow> _filteredRows = new List<RepairedItemRow>();
        private bool _isLoadingData;
        private bool _isExportingCsv;
        private bool _restoringState;
        private bool _pendingSelectionRestore;
        private string _restoreConditionName;
        private string _restoreCategory;
        private string _restoreSortKey;
        private ListSortDirection? _restoreSortDirection;

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;
        private const int PageSize = 8;

        private readonly DispatcherTimer _searchDebounceTimer;
        private readonly DispatcherTimer _stateSaveTimer;

        public RepairedItemsPageViewModel()
        {
            RepairStatusOptions = new ObservableCollection<string> { "All", "Repaired", "Unrepaired" };
            SpareOptions = new ObservableCollection<string> { "All", "Spare only", "Not spare" };
            ActiveOptions = new ObservableCollection<string> { "Active only", "Inactive only", "All" };
            LocationOptions = new ObservableCollection<string> { "All", "Out", "In Set", "Deployed", "In Stock" };
            OriginOptions = new ObservableCollection<string> { "All", "ITCM", "Manual", "Mobile", "Other", "Unknown" };
            ConditionOptions = new ObservableCollection<ConditionChoice> { new ConditionChoice { Id = 0, Name = "All" } };
            CategoryOptions = new ObservableCollection<string> { "All" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedRows = new ObservableCollection<RepairedItemRow>();

            _selectedRepairStatus = "All";
            _selectedSpare = "All";
            _selectedActive = "Active only";
            _selectedLocation = "All";
            _selectedOrigin = "All";
            _selectedCondition = ConditionOptions[0];
            _selectedCategory = "All";

            RefreshCommand = new RelayCommand(async () => { ResetSummaryCardFilters(); await LoadDataAsync(); });
            ViewHistoryCommand = new RelayCommand(OpenSelectedRepairHistory);
            MarkRepairedCommand = new RelayCommand(async () => await MarkSelectedAsync("Repaired"));
            MarkUnrepairedCommand = new RelayCommand(async () => await MarkSelectedAsync("Unrepaired"));
            MarkSpareCommand = new RelayCommand(async () => await MarkSelectedAsync(RepairedItemRow.SpareRepairAction));
            DisposeCommand = new RelayCommand(async () => await ExecuteLifecycleActionAsync("Disposed"));
            SellCommand = new RelayCommand(async () => await ExecuteLifecycleActionAsync("Sold"));
            ExportCsvCommand = new RelayCommand(async () => await ExportToCsvAsync());
            CopySerialCommand = new RelayCommand(CopySelectedSerial);
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindGridPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindGridPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage++; BindGridPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = GetTotalPages(); BindGridPage(); });

            TotalCardClickCommand = new RelayCommand(() => { SelectedRepairStatus = "All"; SelectedSpare = "All"; ApplyFilters(true); });
            RepairedCardClickCommand = new RelayCommand(() => { SelectedRepairStatus = "Repaired"; SelectedSpare = "All"; ApplyFilters(true); });
            UnrepairedCardClickCommand = new RelayCommand(() => { SelectedRepairStatus = "Unrepaired"; SelectedSpare = "All"; ApplyFilters(true); });
            SpareCardClickCommand = new RelayCommand(() => { SelectedSpare = "Spare only"; ApplyFilters(true); });
            DamagedCardClickCommand = new RelayCommand(() =>
            {
                var changed = TrySelectConditionByName("Damaged");
                if (!changed) { UpdateSummaryCounts(); ApplyFilters(true); }
            });

            _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(resetToFirstPage: true); };

            _stateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _stateSaveTimer.Tick += (s, e) => { _stateSaveTimer.Stop(); SaveUiState(); };

            _setFilterDebounceTimer.Tick += async (s, e) => { _setFilterDebounceTimer.Stop(); await LoadDataAsync(); };

            RestoreUiState();
            _ = LoadDataAsync();
        }

        // ── Search ────────────────────────────────────────────────────────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                {
                    ScheduleSaveState();
                    if (IsDisposed) return;
                    _searchDebounceTimer.Stop();
                    _searchDebounceTimer.Start();
                }
            }
        }

        // Set when the rows currently on screen came from the "Did you mean...?" fuzzy fallback
        // (see FuzzyMatchRepairedItems) rather than an exact search-text match, so the view can show
        // a banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        public bool IsDisposed { get; private set; }
        public void MarkDisposed()
        {
            IsDisposed = true;
            _searchDebounceTimer.Stop();
            _stateSaveTimer.Stop();
            _setFilterDebounceTimer.Stop();
        }

        // ── Filters ───────────────────────────────────────────────────────────

        public ObservableCollection<string> RepairStatusOptions { get; }
        public ObservableCollection<string> SpareOptions { get; }
        public ObservableCollection<string> ActiveOptions { get; }
        public ObservableCollection<string> LocationOptions { get; }
        public ObservableCollection<string> OriginOptions { get; }
        public ObservableCollection<ConditionChoice> ConditionOptions { get; }
        public ObservableCollection<string> CategoryOptions { get; }

        private string _selectedRepairStatus;
        public string SelectedRepairStatus
        {
            get => _selectedRepairStatus;
            set { if (SetField(ref _selectedRepairStatus, value)) { UpdateSummaryCardActiveStates(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private ConditionChoice _selectedCondition;
        public ConditionChoice SelectedCondition
        {
            get => _selectedCondition;
            set { if (SetField(ref _selectedCondition, value)) { UpdateSummaryCounts(); UpdateSummaryCardActiveStates(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetField(ref _selectedCategory, value)) { UpdateSummaryCounts(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private string _selectedSpare;
        public string SelectedSpare
        {
            get => _selectedSpare;
            set { if (SetField(ref _selectedSpare, value)) { UpdateSummaryCardActiveStates(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private string _selectedActive;
        public string SelectedActive
        {
            get => _selectedActive;
            set { if (SetField(ref _selectedActive, value)) { UpdateSummaryCounts(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private string _selectedLocation;
        public string SelectedLocation
        {
            get => _selectedLocation;
            set { if (SetField(ref _selectedLocation, value)) { UpdateSummaryCounts(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private string _selectedOrigin;
        public string SelectedOrigin
        {
            get => _selectedOrigin;
            set { if (SetField(ref _selectedOrigin, value)) { UpdateSummaryCounts(); ApplyFilters(true); ScheduleSaveState(); } }
        }

        private bool _problemsOnly;
        public bool ProblemsOnly
        {
            get => _problemsOnly;
            set { if (SetField(ref _problemsOnly, value)) { ApplyFilters(true); ScheduleSaveState(); } }
        }

        // ── Set-linked filters (SetCode, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) ───────────────────────────────────────────────
        // Unlike the filters above, these aren't columns on RepairedItemRow — an Item can belong
        // to many dbo.SetItem rows across many Sets — so they can't be re-filtered client-side over
        // _allRows. Each is resolved server-side via an EXISTS join in LoadDataAsync (mirrors
        // ItemsPageViewModel's Set-linked filters), so typing triggers a debounced full reload
        // (reusing this page's own 250ms interval, same as _searchDebounceTimer) instead of ApplyFilters().
        private readonly DispatcherTimer _setFilterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        private void QueueSetFilterReload()
        {
            ScheduleSaveState();
            if (IsDisposed) return;
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

        // ── Busy state ────────────────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        private void SetBusyState(bool isBusy, string statusText = null)
        {
            IsBusy = isBusy;
            if (!string.IsNullOrWhiteSpace(statusText))
                PageInfoText = statusText;
        }

        public event Action<string, string> ErrorOccurred;

        /// <summary>Pre-fills the search box and applies the filter. Called from the home page
        /// global search to land the user on this page with results pre-filtered.</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
            ApplyFilters(resetToFirstPage: true);
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears only the three summary-card-linked selections (Repair status, Spare,
        /// Damaged condition) back to their defaults and persists that back to Settings so a
        /// stale card selection doesn't reappear on next login — called from RefreshCommand.
        /// Unlike ResetFiltersCommand (the "Reset" link inside Advanced Filters), this leaves
        /// Category/Active/Location/Origin/Search/ProblemsOnly untouched.</summary>
        private void ResetSummaryCardFilters()
        {
            _selectedRepairStatus = "All";
            OnPropertyChanged(nameof(SelectedRepairStatus));

            _selectedSpare = "All";
            OnPropertyChanged(nameof(SelectedSpare));

            _selectedCondition = ConditionOptions.FirstOrDefault(c => c.Id == 0) ?? ConditionOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedCondition));

            UpdateSummaryCardActiveStates();
            ScheduleSaveState();
        }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            IsFuzzyResults = false;
            FuzzyNoticeText = "";

            _selectedRepairStatus = "All";
            OnPropertyChanged(nameof(SelectedRepairStatus));

            _selectedCondition = ConditionOptions.FirstOrDefault(c => c.Id == 0) ?? ConditionOptions.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedCondition));

            _selectedCategory = "All";
            OnPropertyChanged(nameof(SelectedCategory));

            _selectedSpare = "All";
            OnPropertyChanged(nameof(SelectedSpare));

            _selectedActive = "Active only";
            OnPropertyChanged(nameof(SelectedActive));

            _selectedLocation = "All";
            OnPropertyChanged(nameof(SelectedLocation));

            _selectedOrigin = "All";
            OnPropertyChanged(nameof(SelectedOrigin));

            _problemsOnly = false;
            OnPropertyChanged(nameof(ProblemsOnly));

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

            UpdateSummaryCounts();
            UpdateSummaryCardActiveStates();
            ApplyFilters(resetToFirstPage: true);
            ScheduleSaveState();
            _ = LoadDataAsync();
        }
    }
}
