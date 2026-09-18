using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Warranty.ViewModels
{
    /// <summary>
    /// Business logic for the Warranty browse page, ported verbatim from the live
    /// BuildUiWithTemplate() path of Pages\Warranty\ViewWarrantyPage.cs (the dead BuildUI(),
    /// GridWarranty_CellPainting, and the never-added "Generate Report" button were confirmed
    /// unreachable and are intentionally not ported).
    /// </summary>
    public sealed class WarrantyPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private List<WarrantyItemDto> _allItems = new List<WarrantyItemDto>();
        private List<WarrantyItemDto> _filteredItems = new List<WarrantyItemDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public WarrantyPageViewModel()
        {
            ItemTypeOptions = new ObservableCollection<string> { "All", "Hardware", "Software/License", "Services" };
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedItems = new ObservableCollection<WarrantyItemDto>();

            _selectedItemType = "All";
            _selectedFilterBy = "Default";

            RefreshCommand = new RelayCommand(async () => await LoadWarrantyItemsAsync());
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() =>
            {
                int totalPages = TotalPages;
                if (CurrentPage < totalPages) { CurrentPage++; UpdatePagination(); }
            });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(WarrantySummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == WarrantySummaryMode.ActiveOnly ? WarrantySummaryMode.All : WarrantySummaryMode.ActiveOnly));
            ExpiringCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == WarrantySummaryMode.ExpiringOnly ? WarrantySummaryMode.All : WarrantySummaryMode.ExpiringOnly));
            ExpiredCardCommand = new RelayCommand(() => SetSummaryMode(
                _summaryMode == WarrantySummaryMode.ExpiredOnly ? WarrantySummaryMode.All : WarrantySummaryMode.ExpiredOnly));
            ResetFiltersCommand = new RelayCommand(ResetFilters);
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedItemType = "All";
            OnPropertyChanged(nameof(SelectedItemType));

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _showExpiredOnly = false;
            OnPropertyChanged(nameof(ShowExpiredOnly));
            _summaryMode = WarrantySummaryMode.All;
            NotifySummaryCardsChanged();

            ApplyFilters();
        }

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        public ObservableCollection<string> ItemTypeOptions { get; }

        private string _selectedItemType;
        public string SelectedItemType
        {
            get => _selectedItemType;
            set { if (SetField(ref _selectedItemType, value)) ApplyFilters(); }
        }

        private bool _showExpiredOnly;
        public bool ShowExpiredOnly
        {
            get => _showExpiredOnly;
            set
            {
                if (!SetField(ref _showExpiredOnly, value)) return;
                _summaryMode = value ? WarrantySummaryMode.ExpiredOnly : WarrantySummaryMode.All;
                NotifySummaryCardsChanged();
                ApplyFilters();
            }
        }

        // ── Clickable summary cards (Total/Active/Expiring/Expired) ──────────
        private enum WarrantySummaryMode { All, ActiveOnly, ExpiringOnly, ExpiredOnly }
        private WarrantySummaryMode _summaryMode = WarrantySummaryMode.All;

        public bool IsTotalCardActive => _summaryMode == WarrantySummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == WarrantySummaryMode.ActiveOnly;
        public bool IsExpiringCardActive => _summaryMode == WarrantySummaryMode.ExpiringOnly;
        public bool IsExpiredCardActive => _summaryMode == WarrantySummaryMode.ExpiredOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand ExpiringCardCommand { get; private set; }
        public RelayCommand ExpiredCardCommand { get; private set; }

        private void SetSummaryMode(WarrantySummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsExpiredOnly = mode == WarrantySummaryMode.ExpiredOnly;
            if (_showExpiredOnly != needsExpiredOnly)
            {
                _showExpiredOnly = needsExpiredOnly;
                OnPropertyChanged(nameof(ShowExpiredOnly));
            }
            ApplyFilters();
        }

        private void NotifySummaryCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsExpiringCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
        }

        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    // Mirrors CmbFilterBy_SelectedIndexChanged: only "Most Recently Added"/
                    // "Oldest Added" force a sort by WarrantyEndDate; "Default" leaves the
                    // current order untouched (the SQL's own ORDER BY, or whatever the last
                    // explicit column/Sort-By sort produced).
                    const string dateColumn = "WarrantyEndDate";
                    if (SelectedFilterBy == "Most Recently Added")
                        SortDataSource(dateColumn, ListSortDirection.Descending);
                    else if (SelectedFilterBy == "Oldest Added")
                        SortDataSource(dateColumn, ListSortDirection.Ascending);

                    UpdatePagination();
                }
            }
        }

        // ── Sort By dropdown (mirrors DefaultListPageTemplate.SetupSortByDropdown) ──

        public ObservableCollection<ListSortOption> SortByOptions { get; }

        private ListSortOption _selectedSortBy;
        public ListSortOption SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                _selectedSortBy = value;
                OnPropertyChanged();

                if (_suppressSortByChange || value == null) return;

                SortDataSource(value.ColumnKey, value.Direction);
                UpdatePagination();
            }
        }

        public ListSortDirection? CurrentSortDirection => _sortDirection;

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                ListSortOption selected = null;
                if (_sortColumnKey != null)
                {
                    selected = options.FirstOrDefault(o =>
                        o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                }
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                {
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);
                }

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler. The View resolves the toggle
        /// direction from the clicked column's own current SortDirection (mirrors the original's
        /// glyph-driven toggle: GridWarranty_ColumnHeaderMouseClick reads
        /// col.HeaderCell.SortGlyphDirection, not a stored "last sorted column" flag).</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            SortDataSource(columnKey, direction);
            UpdatePagination();

            _suppressSortByChange = true;
            try
            {
                var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == _sortDirection);
                if (match != null) SelectedSortBy = match;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        private void SortDataSource(string propertyName, ListSortDirection direction)
        {
            if (_filteredItems == null || string.IsNullOrWhiteSpace(propertyName))
                return;

            try
            {
                var sorted = direction == ListSortDirection.Ascending
                    ? _filteredItems.OrderBy(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null))
                    : _filteredItems.OrderByDescending(x => x.GetType().GetProperty(propertyName)?.GetValue(x, null));

                _filteredItems = sorted.ToList();
                _sortColumnKey = propertyName;
                _sortDirection = direction;
            }
            catch
            {
                // Ignore sorting errors (matches original SortDataSource's silent catch).
            }
        }

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<WarrantyItemDto> PagedItems { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => _filteredItems.Count == 0 ? 0 : (int)Math.Ceiling((double)_filteredItems.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 items)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

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

        private void UpdatePagination()
        {
            if (_filteredItems == null || _filteredItems.Count == 0)
            {
                PagedItems.Clear();
                PageInfoText = "Page 0 of 0 (0 items)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = _filteredItems.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedItems.Clear();
            foreach (var item in paged) PagedItems.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({_filteredItems.Count} items)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Summary cards (always computed from the full _allItems, not the filtered/paged set —
        //    matches the original's UpdateSummaryCards()) ──────────────────────

        private int _totalCount, _activeCount, _expiringCount, _expiredCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int ExpiringCount { get => _expiringCount; private set => SetField(ref _expiringCount, value); }
        public int ExpiredCount { get => _expiredCount; private set => SetField(ref _expiredCount, value); }

        private void UpdateSummaryCards()
        {
            TotalCount = _allItems.Count;
            ActiveCount = _allItems.Count(i => !i.DaysRemaining.HasValue || i.DaysRemaining.Value > 90);
            ExpiringCount = _allItems.Count(i => i.DaysRemaining.HasValue && i.DaysRemaining.Value >= 0 && i.DaysRemaining.Value <= 90);
            ExpiredCount = _allItems.Count(i => i.DaysRemaining.HasValue && i.DaysRemaining.Value < 0);
        }

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string> ErrorOccurred;
        public event Action<int> RequestOpenRenewalDetail;

        public void RaiseOpenRenewalDetail(int itemId) => RequestOpenRenewalDetail?.Invoke(itemId);

        public async Task LoadWarrantyItemsAsync()
        {
            try
            {
                var items = new List<WarrantyItemDto>();
                var cs = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;

                if (string.IsNullOrWhiteSpace(cs))
                {
                    ErrorOccurred?.Invoke("Connection string not found.");
                    return;
                }

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT
                            i.ItemId,
                            i.Name,
                            i.ItemType,
                            i.SerialNumber,
                            i.ModelNumber,
                            -- Priority 1: If item belongs to a Set, use Set dates
                            -- Priority 2: If item NOT part of any Set, use Item-level dates
                            COALESCE(s.StartDate, i.StartDate) AS StartDate,
                            COALESCE(s.EndDate, i.EndDate) AS EndDate,
                            i.WarrantyYears,
                            -- Use Set dates if available, otherwise Item dates
                            COALESCE(s.StartDate, i.WarrantyStartDate, i.DateCreated) AS WarrantyStartDate,
                            COALESCE(s.EndDate, i.WarrantyEndDate) AS WarrantyEndDate,
                            -- Calculate days remaining from the effective end date
                            DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) AS DaysRemaining,
                            s.SetCode
                        FROM dbo.Item i
                        LEFT JOIN dbo.SetItem si ON i.ItemId = si.ItemId
                        LEFT JOIN dbo.[Set] s ON si.SetId = s.SetId
                        WHERE i.Active = 1
                          AND (
                              -- Software/License: Must have either Set EndDate or Item EndDate
                              (i.ItemType = 'Software/License' AND (s.EndDate IS NOT NULL OR i.EndDate IS NOT NULL))
                              -- Hardware: Must have warranty years OR be part of a Set with dates
                              OR (i.ItemType <> 'Software/License' AND (i.WarrantyYears > 0 OR s.EndDate IS NOT NULL))
                          )
                        ORDER BY COALESCE(s.EndDate, i.WarrantyEndDate) ASC";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var itemType = reader.IsDBNull(2) ? "Hardware" : reader.GetString(2);
                            var endDate = reader.IsDBNull(6) ? (DateTime?)null : reader.GetDateTime(6);
                            var daysRemaining = reader.IsDBNull(10) ? (int?)null : reader.GetInt32(10);
                            var setCode = reader.IsDBNull(11) ? "" : reader.GetString(11);

                            // For Software/License items, recalculate DaysRemaining based on EndDate
                            if (itemType == "Software/License" && endDate.HasValue)
                            {
                                daysRemaining = (int)(endDate.Value.Date - DateTime.Today).TotalDays;
                            }

                            // Calculate status based on days remaining
                            string status = "Active";
                            if (daysRemaining.HasValue)
                            {
                                if (daysRemaining.Value < 0)
                                    status = "Expired";
                                else if (daysRemaining.Value <= 30)
                                    status = "Critical (≤30 days)";
                                else if (daysRemaining.Value <= 90)
                                    status = "Warning (≤90 days)";
                            }

                            items.Add(new WarrantyItemDto
                            {
                                ItemId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                ItemType = itemType,
                                SerialNumber = reader.IsDBNull(3) ? null : reader.GetString(3),
                                ModelNumber = reader.IsDBNull(4) ? null : reader.GetString(4),
                                StartDate = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5),
                                EndDate = endDate,
                                WarrantyYears = reader.GetInt32(7),
                                WarrantyStartDate = reader.IsDBNull(8) ? (DateTime?)null : reader.GetDateTime(8),
                                WarrantyEndDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                                DaysRemaining = daysRemaining,
                                WarrantyStatus = status,
                                SetCode = setCode
                            });
                        }
                    }
                }

                _allItems = items;
                ApplyFilters();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(ex.Message);
            }
        }

        private void ApplyFilters()
        {
            if (_allItems == null) return;

            var filtered = _allItems.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var searchText = SearchText.ToLower();
                filtered = filtered.Where(i =>
                    (i.Name != null && i.Name.ToLower().Contains(searchText)) ||
                    (i.SerialNumber != null && i.SerialNumber.ToLower().Contains(searchText)) ||
                    (i.ModelNumber != null && i.ModelNumber.ToLower().Contains(searchText)));
            }

            if (!string.IsNullOrEmpty(SelectedItemType) && SelectedItemType != "All")
            {
                var selectedType = SelectedItemType.Trim();
                filtered = filtered.Where(i =>
                {
                    var itemType = (i.ItemType ?? string.Empty).Trim();

                    // Be resilient to singular/plural mismatch coming from DB values.
                    if (string.Equals(selectedType, "Service", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Equals(itemType, "Service", StringComparison.OrdinalIgnoreCase)
                               || string.Equals(itemType, "Services", StringComparison.OrdinalIgnoreCase);
                    }

                    return string.Equals(itemType, selectedType, StringComparison.OrdinalIgnoreCase);
                });
            }

            switch (_summaryMode)
            {
                case WarrantySummaryMode.ActiveOnly:
                    filtered = filtered.Where(i => !i.DaysRemaining.HasValue || i.DaysRemaining.Value > 90);
                    break;
                case WarrantySummaryMode.ExpiringOnly:
                    filtered = filtered.Where(i => i.DaysRemaining.HasValue && i.DaysRemaining.Value >= 0 && i.DaysRemaining.Value <= 90);
                    break;
                case WarrantySummaryMode.ExpiredOnly:
                    filtered = filtered.Where(i => i.DaysRemaining.HasValue && i.DaysRemaining.Value < 0);
                    break;
                // All: no filter
            }

            _filteredItems = filtered.ToList();
            CurrentPage = 1;
            UpdatePagination();
            UpdateSummaryCards();
        }

        /// <summary>Pre-fills the search box and applies the filter. Called from the home page
        /// global search to land the user on this page with results pre-filtered.</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
        }
    }
}
