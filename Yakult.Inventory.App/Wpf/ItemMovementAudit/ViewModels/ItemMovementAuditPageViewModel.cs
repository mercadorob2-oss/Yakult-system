using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.ItemMovementAudit.ViewModels
{
    /// <summary>
    /// Business logic for the Item Movement Audit page, ported verbatim from
    /// Pages\Item-Audit\ViewItemMovementAuditPage.cs (+ .State.cs, now deleted — persistence
    /// folded in here). The dead Dgv_CellPainting / MakePill / ConfigurePillHopeButton /
    /// MakePillButton / ApplyMovementFilter methods were confirmed unreachable and are not
    /// ported. Filter/enrichment/classification logic, the cancellation-guarded async load
    /// pipeline, and the settings-backed state keys (ItemMovementAudit_*) are unchanged.
    /// </summary>
    public sealed partial class ItemMovementAuditPageViewModel : ViewModelBase
    {
        private readonly ItemMovementAuditRepository _repo = new ItemMovementAuditRepository();

        private System.Collections.Generic.List<ItemMovementAuditDto> _allMovements;
        private System.Collections.Generic.List<ItemMovementAuditDto> _filteredMovements;

        private bool _isLoading;
        private int _loadVersion;
        private bool _reloadRequested;
        private CancellationTokenSource _applyFiltersCts;
        private int _applyFiltersVersion;

        private bool _restoringState;
        private bool _pendingRestoreAfterLoad;
        private string _restoreMovementCategory;
        private string _restoreMovementType;
        private string _restoreSortKey;
        private ListSortDirection? _restoreSortDirection;

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        private readonly DispatcherTimer _filterDebounceTimer;
        private readonly DispatcherTimer _loadDebounceTimer;
        private readonly DispatcherTimer _stateSaveTimer;

        public ItemMovementAuditPageViewModel(bool autoLoad = true)
        {
            ViewModeOptions = new ObservableCollection<string> { "Latest only", "Show all events" };
            TopRowsOptions = new ObservableCollection<int> { 200, 500, 1000, 5000 };
            PageSizeOptions = new ObservableCollection<int> { 10, 25, 50, 100 };
            DirectionOptions = new ObservableCollection<string> { "All", "IN", "OUT" };
            SourceOptions = new ObservableCollection<string> { "All", "Inventory", "Request", "MobileUpdate", "Borrow", "AuditTrail", "RepairHistory", "ITCM" };
            CategoryOptions = new ObservableCollection<string> { "All" };
            TypeOptions = new ObservableCollection<string> { "All" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedMovements = new ObservableCollection<ItemMovementAuditDto>();

            _dateFrom = DateTime.Today.AddDays(-7);
            _dateTo = DateTime.Today;
            _selectedViewMode = "Latest only";
            _topRows = 1000;
            _pageSize = 10;
            _selectedDirection = "All";
            _selectedSource = "All";
            _selectedCategory = "All";
            _selectedType = "All";

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ViewDetailsCommand = new RelayCommand(() => RequestViewDetails?.Invoke(SelectedRow));
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < GetTotalPages()) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = GetTotalPages(); UpdatePagination(); });
            TotalCardClickCommand = new RelayCommand(() => SelectedDirection = "All");
            InCardClickCommand = new RelayCommand(() => SelectedDirection = "IN");
            OutCardClickCommand = new RelayCommand(() => SelectedDirection = "OUT");
            ToggleAdvancedFiltersCommand = new RelayCommand(() => IsAdvancedFiltersOpen = !IsAdvancedFiltersOpen);
            SetDatePresetCommand = new RelayCommand<int>(days => ApplyDatePreset(days));
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _filterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
            _filterDebounceTimer.Tick += async (s, e) => { _filterDebounceTimer.Stop(); await ApplyFiltersAsync(); };

            _loadDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _loadDebounceTimer.Tick += async (s, e) => { _loadDebounceTimer.Stop(); await LoadAsync(); };

            _stateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _stateSaveTimer.Tick += (s, e) => { _stateSaveTimer.Stop(); SaveUiState(); };

            RestoreUiState();
            if (autoLoad)
                _ = LoadAsync();
        }

        public void MarkDisposed()
        {
            IsDisposed = true;
            var cts = Interlocked.Exchange(ref _applyFiltersCts, null);
            if (cts != null) { try { cts.Cancel(); } catch { } try { cts.Dispose(); } catch { } }
            _filterDebounceTimer.Stop();
            _loadDebounceTimer.Stop();
            _stateSaveTimer.Stop();
        }

        public bool IsDisposed { get; private set; }

        private void TriggerDebouncedFilter()
        {
            _filterDebounceTimer.Stop();
            _filterDebounceTimer.Start();
        }

        private void TriggerDebouncedLoad()
        {
            if (_restoringState) return;
            CurrentPage = 1;
            _loadDebounceTimer.Stop();
            _loadDebounceTimer.Start();
        }

        // ── Filters ───────────────────────────────────────────────────────────

        private DateTime _dateFrom;
        public DateTime DateFrom
        {
            get => _dateFrom;
            set { if (SetField(ref _dateFrom, value)) { TriggerDebouncedLoad(); OnPropertyChanged(nameof(DateRangeText)); } }
        }

        private DateTime _dateTo;
        public DateTime DateTo
        {
            get => _dateTo;
            set { if (SetField(ref _dateTo, value)) { TriggerDebouncedLoad(); OnPropertyChanged(nameof(DateRangeText)); } }
        }

        public string DateRangeText
        {
            get
            {
                if (DateFrom == DateTo) return DateFrom.ToString("MMM dd, yyyy");
                return $"{DateFrom:MMM dd} - {DateTo:MMM dd, yyyy}";
            }
        }

        private void ApplyDatePreset(int daysBack)
        {
            DateTo = DateTime.Today;
            DateFrom = daysBack < 0
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Today.AddDays(-daysBack);
        }

        public RelayCommand ResetFiltersCommand { get; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            SearchText = string.Empty;
            SerialFilter = string.Empty;
            SetCodeFilter = string.Empty;
            UserFilter = string.Empty;
            SelectedCategory = "All";
            SelectedType = "All";
            SelectedSource = "All";
            SelectedDirection = "All";
            SelectedFilterBy = "Default";
            SelectedViewMode = "Latest only";
            TopRows = 1000;
            PageSize = 10;
            ShowArchived = false;

            // Reset to the page's true default sort (newest first) rather than a blank/null sort
            // state, which would otherwise fall back to unsorted DB order and desync from what
            // the Sort By dropdown displays.
            _sortColumnKey = "EventTime";
            _sortDirection = ListSortDirection.Descending;
            _restoreSortKey = "EventTime";
            _restoreSortDirection = ListSortDirection.Descending;
            ApplySortGlyphAndDropdown();

            // Written directly (not via the DateFrom/DateTo setters) and followed by an
            // unconditional reload: ApplyDatePreset()/the property setters only trigger a reload
            // when the value actually differs from the current one, so if the date range was
            // already at its 7-day default this would otherwise silently do nothing — leaving the
            // stale sort/grid state in place even though every filter field "looks" reset.
            _dateTo = DateTime.Today;
            _dateFrom = DateTime.Today.AddDays(-7);
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            OnPropertyChanged(nameof(DateRangeText));

            _ = LoadAsync();
        }

        public ObservableCollection<string> ViewModeOptions { get; }
        private string _selectedViewMode;
        public string SelectedViewMode
        {
            get => _selectedViewMode;
            set { if (SetField(ref _selectedViewMode, value)) _ = LoadAsync(); }
        }

        public ObservableCollection<int> TopRowsOptions { get; }
        private int _topRows;
        public int TopRows
        {
            get => _topRows;
            set { if (SetField(ref _topRows, value)) { TriggerDebouncedLoad(); ScheduleSaveState(); } }
        }

        public ObservableCollection<int> PageSizeOptions { get; }
        private int _pageSize;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (SetField(ref _pageSize, value))
                {
                    CurrentPage = 1;
                    UpdatePagination();
                    ScheduleSaveState();
                }
            }
        }

        public ObservableCollection<string> DirectionOptions { get; }
        private string _selectedDirection;
        public string SelectedDirection
        {
            get => _selectedDirection;
            set { if (SetField(ref _selectedDirection, value)) { UpdateSummaryCardActiveStates(); _ = ApplyFiltersAsync(); ScheduleSaveState(); } }
        }

        public ObservableCollection<string> SourceOptions { get; }
        private string _selectedSource;
        public string SelectedSource
        {
            get => _selectedSource;
            set { if (SetField(ref _selectedSource, value)) { _ = ApplyFiltersAsync(); ScheduleSaveState(); } }
        }

        public ObservableCollection<string> CategoryOptions { get; }
        private string _selectedCategory;
        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetField(ref _selectedCategory, value)) { _ = ApplyFiltersAsync(); ScheduleSaveState(); } }
        }

        public ObservableCollection<string> TypeOptions { get; }
        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set { if (SetField(ref _selectedType, value)) { _ = ApplyFiltersAsync(); ScheduleSaveState(); } }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { TriggerDebouncedFilter(); ScheduleSaveState(); } }
        }

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) { _ = ApplyFiltersAsync(); ScheduleSaveState(); } }
        }

        private string _serialFilter = string.Empty;
        public string SerialFilter
        {
            get => _serialFilter;
            set { if (SetField(ref _serialFilter, value)) { TriggerDebouncedFilter(); ScheduleSaveState(); } }
        }

        private string _setCodeFilter = string.Empty;
        public string SetCodeFilter
        {
            get => _setCodeFilter;
            set { if (SetField(ref _setCodeFilter, value)) { TriggerDebouncedFilter(); ScheduleSaveState(); } }
        }

        private string _userFilter = string.Empty;
        public string UserFilter
        {
            get => _userFilter;
            set { if (SetField(ref _userFilter, value)) { TriggerDebouncedFilter(); ScheduleSaveState(); } }
        }

        // ── Busy / progress state ────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        // ── Summary cards ─────────────────────────────────────────────────────

        private int _totalCount, _inCount, _outCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int InCount { get => _inCount; private set => SetField(ref _inCount, value); }
        public int OutCount { get => _outCount; private set => SetField(ref _outCount, value); }

        private bool _isTotalCardActive, _isInCardActive, _isOutCardActive;
        public bool IsTotalCardActive { get => _isTotalCardActive; private set => SetField(ref _isTotalCardActive, value); }
        public bool IsInCardActive { get => _isInCardActive; private set => SetField(ref _isInCardActive, value); }
        public bool IsOutCardActive { get => _isOutCardActive; private set => SetField(ref _isOutCardActive, value); }

        private void UpdateSummaryCardActiveStates()
        {
            var dir = SelectedDirection ?? "All";
            IsTotalCardActive = dir == "All";
            IsInCardActive = dir == "IN";
            IsOutCardActive = dir == "OUT";
        }

        // ── Selection / commands ──────────────────────────────────────────────

        private ItemMovementAuditDto _selectedRow;
        public ItemMovementAuditDto SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ViewDetailsCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }
        public RelayCommand TotalCardClickCommand { get; }
        public RelayCommand InCardClickCommand { get; }
        public RelayCommand OutCardClickCommand { get; }
        public RelayCommand ToggleAdvancedFiltersCommand { get; }
        public RelayCommand<int> SetDatePresetCommand { get; }

        private bool _isAdvancedFiltersOpen;
        public bool IsAdvancedFiltersOpen
        {
            get => _isAdvancedFiltersOpen;
            set => SetField(ref _isAdvancedFiltersOpen, value);
        }

        public event Action<ItemMovementAuditDto> RequestViewDetails;
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action RebuildSortByOptionsRequested;

        /// <summary>Called by the details dialog's "view timeline" callback and by the host
        /// wrapper's public LoadTimelineForSerialAsync passthrough.</summary>
        public async System.Threading.Tasks.Task LoadTimelineForSerialAsync(string serial)
        {
            var value = serial ?? string.Empty;
            SerialFilter = value;
            SearchText = value;
            CurrentPage = 1;
            await LoadAsync();
        }

        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
        }
    }
}
