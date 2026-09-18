using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Update.ViewModels
{
    /// <summary>
    /// Business logic for the Mobile Updates browse page, ported from the live data pipeline of
    /// Pages\Update\ViewUpdatesPage.cs (LoadUpdatesAsync/ApplyFiltersAsync). Date range and Set
    /// Code are server-side filters (passed to SetItemUpdateRepository.GetFilteredAsync); Status
    /// (via the clickable summary cards), search text, and "Problems only" are applied
    /// client-side over the loaded set — matching the original's comment that Status is kept
    /// client-side so the summary counts stay accurate. Row actions (Mark Processed, Delete,
    /// View Details, Audit Timeline, Configure API) are intentionally NOT ported here — they stay
    /// in UpdatesPageView.xaml.cs, copied verbatim from the original page, since they contain
    /// non-trivial transactional business logic (item auto-creation, condition/repair handling,
    /// audit trail entries) that should not be re-derived from memory.
    /// </summary>
    public sealed class UpdatesPageViewModel : ViewModelBase
    {
        private readonly SetItemUpdateRepository _repository = new SetItemUpdateRepository();

        private List<SetItemUpdateDto> _allUpdates = new List<SetItemUpdateDto>();
        private List<SetItemUpdateDto> _filteredUpdates = new List<SetItemUpdateDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;

        private readonly DispatcherTimer _reloadDebounceTimer;
        private readonly DispatcherTimer _stateSaveTimer;
        private bool _restoringState;

        private static DateTime GetDefaultUpdatesFromDate() => DateTime.Today.AddMonths(-1);
        private static DateTime GetDefaultUpdatesToDate() => DateTime.Today;

        public UpdatesPageViewModel()
        {
            PageSizeOptions = new ObservableCollection<int> { 10, 25, 50, 100 };
            PagedUpdates = new ObservableCollection<SetItemUpdateDto>();

            RefreshCommand = new RelayCommand(async () => await LoadDataAsync());
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() =>
            {
                if (CurrentPage < GetTotalPages()) { CurrentPage++; UpdatePagination(); }
            });
            LastPageCommand = new RelayCommand(() => { CurrentPage = GetTotalPages(); UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetStatusMode(UpdateStatusMode.All));
            ProcessedCardCommand = new RelayCommand(() => SetStatusMode(
                _statusMode == UpdateStatusMode.Processed ? UpdateStatusMode.All : UpdateStatusMode.Processed));
            UnprocessedCardCommand = new RelayCommand(() => SetStatusMode(
                _statusMode == UpdateStatusMode.Unprocessed ? UpdateStatusMode.All : UpdateStatusMode.Unprocessed));

            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _reloadDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(350) };
            _reloadDebounceTimer.Tick += async (s, e) => { _reloadDebounceTimer.Stop(); await LoadDataAsync(); };

            _stateSaveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
            _stateSaveTimer.Tick += (s, e) => { _stateSaveTimer.Stop(); SaveUiState(); };

            RestoreUiState();
        }

        // ── Server-side filters (Date range, Set Code) ───────────────────────

        private DateTime _dateFrom = GetDefaultUpdatesFromDate();
        public DateTime DateFrom
        {
            get => _dateFrom;
            set { if (SetField(ref _dateFrom, value)) { OnPropertyChanged(nameof(DateRangeText)); TriggerDebouncedReload(); ScheduleSaveState(); } }
        }

        private DateTime _dateTo = GetDefaultUpdatesToDate();
        public DateTime DateTo
        {
            get => _dateTo;
            set { if (SetField(ref _dateTo, value)) { OnPropertyChanged(nameof(DateRangeText)); TriggerDebouncedReload(); ScheduleSaveState(); } }
        }

        public string DateRangeText => DateFrom.Date == DateTo.Date
            ? DateFrom.ToString("MMM dd, yyyy")
            : $"{DateFrom:MMM dd} - {DateTo:MMM dd, yyyy}";

        private string _setCodeFilter = string.Empty;
        public string SetCodeFilter
        {
            get => _setCodeFilter;
            set { if (SetField(ref _setCodeFilter, value)) { TriggerDebouncedReload(); ScheduleSaveState(); } }
        }

        private void TriggerDebouncedReload()
        {
            if (_restoringState) return;
            _reloadDebounceTimer.Stop();
            _reloadDebounceTimer.Start();
        }

        private void ScheduleSaveState()
        {
            if (_restoringState) return;
            _stateSaveTimer.Stop();
            _stateSaveTimer.Start();
        }

        private void RestoreUiState()
        {
            _restoringState = true;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                var savedFrom = settings.ViewUpdates_DateFrom;
                var savedTo = settings.ViewUpdates_DateTo;
                _dateFrom = savedFrom > DateTime.MinValue ? savedFrom.Date : GetDefaultUpdatesFromDate();
                _dateTo = savedTo > DateTime.MinValue ? savedTo.Date : GetDefaultUpdatesToDate();

                _setCodeFilter = settings.ViewUpdates_SetCode ?? string.Empty;
                _searchText = settings.ViewUpdates_Search ?? string.Empty;
                _problemsOnly = settings.ViewUpdates_ProblemsOnly;

                if (settings.ViewUpdates_PageSize > 0)
                    _pageSize = settings.ViewUpdates_PageSize;

                _statusMode = settings.ViewUpdates_StatusIndex == 1
                    ? UpdateStatusMode.Processed
                    : settings.ViewUpdates_StatusIndex == 2
                        ? UpdateStatusMode.Unprocessed
                        : UpdateStatusMode.All;

                _sortColumnKey = string.IsNullOrEmpty(settings.ViewUpdates_SortKey) ? null : settings.ViewUpdates_SortKey;
                _sortDirection = settings.ViewUpdates_SortOrder == 1
                    ? ListSortDirection.Ascending
                    : settings.ViewUpdates_SortOrder == 2
                        ? (ListSortDirection?)ListSortDirection.Descending
                        : null;
            }
            catch
            {
                _dateFrom = GetDefaultUpdatesFromDate();
                _dateTo = GetDefaultUpdatesToDate();
                _statusMode = UpdateStatusMode.Unprocessed;
            }
            finally
            {
                _restoringState = false;
            }
        }

        private void SaveUiState()
        {
            if (_restoringState) return;
            try
            {
                var settings = Yakult.Inventory.App.Properties.Settings.Default;

                settings.ViewUpdates_DateFrom = DateFrom.Date;
                settings.ViewUpdates_DateTo = DateTo.Date;
                settings.ViewUpdates_StatusIndex = _statusMode == UpdateStatusMode.Processed ? 1
                    : _statusMode == UpdateStatusMode.Unprocessed ? 2
                    : 0;
                settings.ViewUpdates_SetCode = SetCodeFilter ?? string.Empty;
                settings.ViewUpdates_Search = SearchText ?? string.Empty;
                settings.ViewUpdates_ProblemsOnly = ProblemsOnly;
                settings.ViewUpdates_PageSize = PageSize;
                settings.ViewUpdates_SortKey = _sortColumnKey ?? string.Empty;
                settings.ViewUpdates_SortOrder = _sortDirection == ListSortDirection.Ascending ? 1
                    : _sortDirection == ListSortDirection.Descending ? 2
                    : 0;

                settings.Save();
            }
            catch
            {
                // best-effort persistence; never block the UI on a settings-save failure
            }
        }

        // ── Client-side filters (Status via cards, search, problems-only) ───

        private enum UpdateStatusMode { All, Processed, Unprocessed }
        private UpdateStatusMode _statusMode = UpdateStatusMode.Unprocessed;

        public bool IsTotalCardActive => _statusMode == UpdateStatusMode.All;
        public bool IsProcessedCardActive => _statusMode == UpdateStatusMode.Processed;
        public bool IsUnprocessedCardActive => _statusMode == UpdateStatusMode.Unprocessed;

        public RelayCommand TotalCardCommand { get; }
        public RelayCommand ProcessedCardCommand { get; }
        public RelayCommand UnprocessedCardCommand { get; }

        private void SetStatusMode(UpdateStatusMode mode)
        {
            _statusMode = mode;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsProcessedCardActive));
            OnPropertyChanged(nameof(IsUnprocessedCardActive));
            ApplyFilters();
            ScheduleSaveState();
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { ApplyFilters(); ScheduleSaveState(); } }
        }

        private bool _problemsOnly;
        public bool ProblemsOnly
        {
            get => _problemsOnly;
            set { if (SetField(ref _problemsOnly, value)) { ApplyFilters(); ScheduleSaveState(); } }
        }

        public RelayCommand ResetFiltersCommand { get; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            _problemsOnly = false;
            OnPropertyChanged(nameof(ProblemsOnly));
            _setCodeFilter = string.Empty;
            OnPropertyChanged(nameof(SetCodeFilter));

            _statusMode = UpdateStatusMode.All;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsProcessedCardActive));
            OnPropertyChanged(nameof(IsUnprocessedCardActive));

            _dateFrom = GetDefaultUpdatesFromDate();
            OnPropertyChanged(nameof(DateFrom));
            _dateTo = GetDefaultUpdatesToDate();
            OnPropertyChanged(nameof(DateTo));
            OnPropertyChanged(nameof(DateRangeText));

            ScheduleSaveState();
            _ = LoadDataAsync();
        }

        /// <summary>Called by the View's date-preset buttons (Today/7d/30d/Month).</summary>
        public void ApplyDatePreset(int daysBack)
        {
            _dateTo = DateTime.Today;
            _dateFrom = daysBack < 0
                ? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1)
                : DateTime.Today.AddDays(-daysBack);
            OnPropertyChanged(nameof(DateFrom));
            OnPropertyChanged(nameof(DateTo));
            OnPropertyChanged(nameof(DateRangeText));
            ScheduleSaveState();
            _ = LoadDataAsync();
        }

        // ── Sort By dropdown ──────────────────────────────────────────────────

        public ObservableCollection<ListSortOption> SortByOptions { get; } = new ObservableCollection<ListSortOption>();

        private ListSortOption _selectedSortBy;
        private bool _suppressSortByChange;
        public ListSortOption SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                _selectedSortBy = value;
                OnPropertyChanged();
                if (_suppressSortByChange || value == null) return;
                _sortColumnKey = value.ColumnKey;
                _sortDirection = value.Direction;
                ApplyFilters();
                ScheduleSaveState();
            }
        }

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                var selected = _sortColumnKey != null
                    ? options.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending))
                    : null;
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;
            _sortColumnKey = columnKey;
            _sortDirection = direction;
            ApplyFilters();
            ScheduleSaveState();
        }

        // ── Summary cards (always computed from the full loaded set, before the
        //    status-card filter, matching the original's UpdateSummaryCards) ──

        private int _totalCount, _processedCount, _unprocessedCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ProcessedCount { get => _processedCount; private set => SetField(ref _processedCount, value); }
        public int UnprocessedCount { get => _unprocessedCount; private set => SetField(ref _unprocessedCount, value); }

        // ── Busy state ────────────────────────────────────────────────────────

        private bool _isBusy;
        public bool IsBusy { get => _isBusy; private set => SetField(ref _isBusy, value); }

        // ── Selection ─────────────────────────────────────────────────────────

        private SetItemUpdateDto _selectedRow;
        public SetItemUpdateDto SelectedRow
        {
            get => _selectedRow;
            set => SetField(ref _selectedRow, value);
        }

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<SetItemUpdateDto> PagedUpdates { get; }
        public ObservableCollection<int> PageSizeOptions { get; }

        private int _pageSize = 10;
        public int PageSize
        {
            get => _pageSize;
            set { if (SetField(ref _pageSize, value)) { CurrentPage = 1; UpdatePagination(); ScheduleSaveState(); } }
        }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; private set => SetField(ref _currentPage, value); }

        private string _pageInfoText = string.Empty;
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private int GetTotalPages() => _filteredUpdates.Count == 0 ? 1 : (int)Math.Ceiling((double)_filteredUpdates.Count / PageSize);

        private void UpdatePagination()
        {
            var totalPages = GetTotalPages();
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = _filteredUpdates.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedUpdates.Clear();
            foreach (var u in paged) PagedUpdates.Add(u);

            PageInfoText = _filteredUpdates.Count == 0
                ? "No results"
                : $"Page {CurrentPage} of {totalPages} ({_filteredUpdates.Count} updates)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string> ErrorOccurred;

        public async System.Threading.Tasks.Task LoadDataAsync()
        {
            IsBusy = true;
            try
            {
                var setCode = string.IsNullOrWhiteSpace(SetCodeFilter) ? null : SetCodeFilter.Trim();

                // Status is intentionally NOT passed here — filtered client-side in ApplyFilters
                // so the Total/Processed/Unprocessed summary cards stay accurate (matches the
                // original LoadUpdatesAsync's comment).
                var updates = await _repository.GetFilteredAsync(
                    fromDate: DateFrom.Date,
                    toDate: DateTo.Date,
                    includeProcessed: true,
                    includeUnprocessed: true,
                    setCode: setCode);

                _allUpdates = updates?.ToList() ?? new List<SetItemUpdateDto>();
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void ApplyFilters()
        {
            if (_allUpdates == null) return;

            TotalCount = _allUpdates.Count;
            ProcessedCount = _allUpdates.Count(u => u != null && u.Processed);
            UnprocessedCount = TotalCount - ProcessedCount;

            IEnumerable<SetItemUpdateDto> filtered = _allUpdates;

            switch (_statusMode)
            {
                case UpdateStatusMode.Processed:
                    filtered = filtered.Where(u => u != null && u.Processed);
                    break;
                case UpdateStatusMode.Unprocessed:
                    filtered = filtered.Where(u => u != null && !u.Processed);
                    break;
                // All: no filter
            }

            var searchTerm = (SearchText ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                filtered = filtered.Where(u =>
                    u != null &&
                    ((u.SetCode?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.SerialNumber?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.ItemType?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.ModelNumber?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.BranchName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.DepartmentName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.Remark?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0) ||
                     (u.UpdatedByName?.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)));
            }

            if (ProblemsOnly)
                filtered = filtered.Where(u => u != null && (u.IsMissing || u.IsArchived));

            var list = filtered.ToList();

            if (_sortColumnKey != null)
            {
                var prop = typeof(SetItemUpdateDto).GetProperty(_sortColumnKey);
                if (prop != null)
                {
                    list = _sortDirection == ListSortDirection.Descending
                        ? list.OrderByDescending(x => prop.GetValue(x, null)).ToList()
                        : list.OrderBy(x => prop.GetValue(x, null)).ToList();
                }
            }

            _filteredUpdates = list;
            CurrentPage = 1;
            UpdatePagination();
        }
    }
}
