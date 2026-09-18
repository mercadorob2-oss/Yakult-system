using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Set.ViewModels
{
    public sealed partial class SetPageViewModel
    {
        private enum SummaryFilter
        {
            All = 0,
            Expired = 1,
            WithQr = 2,
            Hardware = 3,
            SoftwareLicense = 4,
            Services = 5
        }

        private SummaryFilter _summaryFilter = SummaryFilter.All;

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded set — cheap
        // for one keystroke, but calling it synchronously on EVERY keystroke (no debounce) meant
        // holding a key down (or pasting/spamming characters) blocked the UI thread once per
        // character, which is what made typing/erasing visibly lag. Debouncing it the same way the
        // Set-linked filters below already are fixes that without changing the search itself.
        private readonly DispatcherTimer _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        // ── Search ───────────────────────────────────────────────────────────
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); } }
        }

        // Set when the rows currently on screen came from the "Did you mean...?" fuzzy fallback
        // (see FuzzyMatchSets) rather than an exact search-text match, so the view can show a banner
        // explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        // ── Created date range ───────────────────────────────────────────────
        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetField(ref _fromDate, value)) ApplyFilters(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetField(ref _toDate, value)) ApplyFilters(); }
        }

        // ── Advanced Filters: Set/Invoice + Request fields (server-side, EXISTS/join-backed) ──
        // Unlike Search/Category/Date above, these aren't all columns already loaded onto SetRow
        // (Reference Code and Parent Tag live on the many-per-Set dbo.SetItem rows, and Req ID
        // isn't currently selected at all), so they can't be re-filtered client-side over _allRows.
        // Each is resolved server-side in SetRepository.GetAllSetsAsync via WHERE/EXISTS clauses,
        // so typing triggers a debounced reload (same 300ms DispatcherTimer pattern as the Items
        // page's Set-linked filters) instead of ApplyFilters().
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

        // ── Filter By (date-ordering, applied on top of the current filtered set) ──
        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (!SetField(ref _selectedFilterBy, value)) return;
                ApplyFilterByOrder();
            }
        }

        private void ApplyFilterByOrder()
        {
            if (_filteredRows == null) return;

            if (SelectedFilterBy == "Most Recently Added")
                _filteredRows = _filteredRows.OrderByDescending(r => r.CreatedAt).ToList();
            else if (SelectedFilterBy == "Oldest Added")
                _filteredRows = _filteredRows.OrderBy(r => r.CreatedAt).ToList();
            // "Default" leaves the current (search/summary-filtered) order untouched.

            BindPage();
        }

        // ── Summary cards (clickable filters) ────────────────────────────────
        public int TotalCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int WithQrCount { get; private set; }
        public int HardwareCount { get; private set; }
        public int SoftwareLicenseCount { get; private set; }
        public int ServicesCount { get; private set; }

        public bool IsAllCardActive => _summaryFilter == SummaryFilter.All;
        public bool IsExpiredCardActive => _summaryFilter == SummaryFilter.Expired;
        public bool IsWithQrCardActive => _summaryFilter == SummaryFilter.WithQr;
        public bool IsHardwareCardActive => _summaryFilter == SummaryFilter.Hardware;
        public bool IsSoftwareLicenseCardActive => _summaryFilter == SummaryFilter.SoftwareLicense;
        public bool IsServicesCardActive => _summaryFilter == SummaryFilter.Services;

        private void ToggleCard(SummaryFilter filter)
        {
            _summaryFilter = _summaryFilter == filter ? SummaryFilter.All : filter;
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
            OnPropertyChanged(nameof(IsWithQrCardActive));
            OnPropertyChanged(nameof(IsHardwareCardActive));
            OnPropertyChanged(nameof(IsSoftwareLicenseCardActive));
            OnPropertyChanged(nameof(IsServicesCardActive));
            ApplyFilters();
        }

        // ── Category multi-select filter (item categories present within a set) ──
        // Filters by whether a set CONTAINS an item of the selected category, so a set that mixes
        // Cartridge items with other categories (e.g. Printhead + Cartridge together) still matches
        // a "Cartridge" selection — there is no exclusion of mixed-category sets.
        public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; }

        private string _categoryFilterSummary = "All Categories";
        public string CategoryFilterSummary
        {
            get => _categoryFilterSummary;
            private set => SetField(ref _categoryFilterSummary, value);
        }

        private bool _suppressCategoryFilterChange;

        private void LoadCategoryFilterOptions()
        {
            var previouslyChecked = CategoryFilterOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Name)
                .ToList();

            var allCategories = (_categoriesBySetId?.Values ?? Enumerable.Empty<List<string>>())
                .SelectMany(v => v)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressCategoryFilterChange = true;
            try
            {
                CategoryFilterOptions.Clear();
                foreach (var name in allCategories)
                {
                    var option = new CategoryFilterOption(name)
                    {
                        IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase)
                    };
                    option.CheckedChanged += OnCategoryFilterOptionChanged;
                    CategoryFilterOptions.Add(option);
                }
            }
            finally
            {
                _suppressCategoryFilterChange = false;
            }

            UpdateCategoryFilterSummary();
        }

        private void OnCategoryFilterOptionChanged()
        {
            if (_suppressCategoryFilterChange) return;
            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void ClearCategoryFilter()
        {
            _suppressCategoryFilterChange = true;
            try
            {
                foreach (var option in CategoryFilterOptions) option.IsChecked = false;
            }
            finally
            {
                _suppressCategoryFilterChange = false;
            }

            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void UpdateCategoryFilterSummary()
        {
            var checkedNames = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            CategoryFilterSummary = checkedNames.Count == 0
                ? "All Categories"
                : checkedNames.Count == 1
                    ? checkedNames[0]
                    : $"{checkedNames.Count} categories selected";
        }

        // ── "Show Consumable Items" (Request & Set Management only) ─────────
        // Restricts the grid to sets that contain at least one consumable-category item — same
        // "contains" semantics as CategoryFilterOptions above, not an exclusive match. Visible
        // only when IsConsumableFilterAvailable is true (see SetPageViewModel.cs ctor).
        // Canonical spellings; comparison below is via ConsumableCategories.Canonicalize so
        // drifted item text ("Print Head", "Toner Cartridge", …) still counts.
        private static readonly string[] ConsumableCategoryNames = { "Ink", "Printhead", "Toner", "Cartridge" };

        private bool _showConsumableItemsOnly;
        public bool ShowConsumableItemsOnly
        {
            get => _showConsumableItemsOnly;
            set { if (SetField(ref _showConsumableItemsOnly, value)) ApplyFilters(); }
        }

        private static bool IsSoftwareLicenseType(string setType) =>
            string.Equals(setType, "Software/License", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setType, "Software", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setType, "License", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setType, "SoftwareLicense", StringComparison.OrdinalIgnoreCase);

        private static bool IsServicesType(string setType) =>
            string.Equals(setType, "Services", StringComparison.OrdinalIgnoreCase)
            || string.Equals(setType, "Service", StringComparison.OrdinalIgnoreCase);

        private void UpdateSummaryCards()
        {
            var data = _summaryUniverse ?? _filteredRows ?? new List<SetRow>();
            var today = DateTime.Today;

            TotalCount = data.Count;
            ExpiredCount = data.Count(s => s.EffectiveExpiry.Date < today);
            WithQrCount = data.Count(s => s.HasQrImage);
            HardwareCount = data.Count(s => string.Equals(s.SetType, "Hardware", StringComparison.OrdinalIgnoreCase));
            SoftwareLicenseCount = data.Count(s => IsSoftwareLicenseType(s.SetType));
            ServicesCount = data.Count(s => IsServicesType(s.SetType));

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ExpiredCount));
            OnPropertyChanged(nameof(WithQrCount));
            OnPropertyChanged(nameof(HardwareCount));
            OnPropertyChanged(nameof(SoftwareLicenseCount));
            OnPropertyChanged(nameof(ServicesCount));
        }

        /// <summary>Every field the quick search box matches against — a set matches if all typed
        /// terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private IEnumerable<string> SetSearchFields(SetRow s)
        {
            var fields = new List<string>
            {
                s.SetCode, s.SetType, s.Dto.DocumentNumber, s.Dto.ReferenceNumber, s.Dto.Status,
                s.CreatedByName, s.CurrentEmployeeName, s.CurrentCompanyName, s.CurrentBranchName,
                s.CurrentDepartmentName, s.Remarks, s.Dto.UpgradeReason
            };
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(s.SetId, out var names)) fields.AddRange(names);
            if (_serialNumbersBySetId != null && _serialNumbersBySetId.TryGetValue(s.SetId, out var serials)) fields.AddRange(serials);
            return fields;
        }

        /// <summary>Identifier-shaped fields only (SetCode/Document #/Reference #/serials) — the
        /// subset it's meaningful to run an edit-distance comparison against for a code-shaped typo.</summary>
        private static IEnumerable<string> SetCodeFuzzyCandidateFields(SetRow s) => new[]
        {
            s.SetCode, s.Dto.DocumentNumber, s.Dto.ReferenceNumber
        };

        /// <summary>Company + item names — used when the mistyped term itself is a descriptive word
        /// (e.g. a brand: "Xiaoomi" vs "Xiaomi") rather than a code.</summary>
        private IEnumerable<string> SetDescriptiveFuzzyCandidateFields(SetRow s)
        {
            var fields = new List<string> { s.CurrentCompanyName };
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(s.SetId, out var names)) fields.AddRange(names);
            return fields;
        }

        /// <summary>Category/"Consumables only"/date-range filters — every active filter EXCEPT the
        /// quick search text and the summary-card toggle. Shared between the normal pipeline and the
        /// fuzzy fallback so "closest match" results still respect every other filter the user has set.</summary>
        private IEnumerable<SetRow> ApplyStructuralFilters(IEnumerable<SetRow> source)
        {
            var filtered = source;

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(s =>
                    s.Categories.Any(c => selectedCategories.Contains(c, StringComparer.OrdinalIgnoreCase)));
            }

            if (ShowConsumableItemsOnly)
            {
                filtered = filtered.Where(s =>
                    s.Categories.Any(c => ConsumableCategoryNames.Contains(
                        Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(c),
                        StringComparer.OrdinalIgnoreCase)));
            }

            if (FromDate.HasValue)
            {
                var from = FromDate.Value.Date;
                filtered = filtered.Where(s => s.CreatedAt.Date >= from);
            }
            if (ToDate.HasValue)
            {
                var to = ToDate.Value.Date;
                filtered = filtered.Where(s => s.CreatedAt.Date <= to);
            }

            return filtered;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (SetCode/Document #/Reference #) and a descriptive
        /// typo (Company/item name). Every OTHER term still has to match exactly somewhere. Discards
        /// anything past edit distance 2.
        /// </summary>
        private List<SetRow> FuzzyMatchSets(List<SetRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(s => SetSearchFields(s).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            if (fuzzTerm == null) return new List<SetRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<SetRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<SetRow, IEnumerable<string>>)SetCodeFuzzyCandidateFields
                : SetDescriptiveFuzzyCandidateFields;

            return pool
                .Where(s => SearchTextHelper.MatchesAllTerms(otherTerms, SetSearchFields(s)))
                .Select(s => new
                {
                    Row = s,
                    Distance = fuzzFields(s)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f => SearchTextHelper.MinWordDistance(fuzzTerm, f))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min()
                })
                .Where(x => x.Distance <= 2)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Row.SetCode)
                .Take(maxResults)
                .Select(x => x.Row)
                .ToList();
        }

        // ── Core search + summary-card filter pipeline ───────────────────────
        public void ApplyFilters()
        {
            if (_allRows == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(SearchText);

            var structuralPool = ApplyStructuralFilters(_allRows).ToList();

            IEnumerable<SetRow> filtered = structuralPool;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(s => SearchTextHelper.MatchesAllTerms(searchTerms, SetSearchFields(s)));

            _summaryUniverse = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path is completely unaffected. Relaxes just the search text; every other
            // active filter still applies via the same structural pool.
            bool isFuzzy = false;
            if (_summaryUniverse.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchSets(structuralPool, searchTerms);
                if (fuzzyMatches.Count > 0)
                {
                    isFuzzy = true;
                    _summaryUniverse = fuzzyMatches;
                }
            }
            IsFuzzyResults = isFuzzy;
            FuzzyNoticeText = isFuzzy
                ? $"No exact matches for \"{SearchText}\" — showing the closest results instead."
                : "";

            IEnumerable<SetRow> display = _summaryUniverse;
            var today = DateTime.Today;

            if (isFuzzy)
            {
                // Fuzzy results are already the final display set — the summary-card toggle below
                // shouldn't further slice a handful of "closest match" guesses.
                _filteredRows = _summaryUniverse;
                CurrentPage = 1;
                UpdateSummaryCards();
                BindPage();
                return;
            }

            switch (_summaryFilter)
            {
                case SummaryFilter.Expired:
                    display = display.Where(s => s.EffectiveExpiry.Date < today);
                    break;
                case SummaryFilter.WithQr:
                    display = display.Where(s => s.HasQrImage);
                    break;
                case SummaryFilter.Hardware:
                    display = display.Where(s => string.Equals(s.SetType, "Hardware", StringComparison.OrdinalIgnoreCase));
                    break;
                case SummaryFilter.SoftwareLicense:
                    display = display.Where(s => IsSoftwareLicenseType(s.SetType));
                    break;
                case SummaryFilter.Services:
                    display = display.Where(s => IsServicesType(s.SetType));
                    break;
            }

            _filteredRows = display.ToList();
            CurrentPage = 1;
            UpdateSummaryCards();
            BindPage();
        }

        // ── Sort By dropdown (built by the View from its DataGrid columns) ──
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

                _sortColumnKey = value.ColumnKey;
                _sortDirection = value.Direction;
                SortFilteredInPlace();
                BindPage();
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
                    selected = options.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);
                selected = selected ?? options.FirstOrDefault();

                SelectedSortBy = selected;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortDirection = (_sortColumnKey == columnKey && _sortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            _sortColumnKey = columnKey;

            SortFilteredInPlace();
            BindPage();

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

        private void SortFilteredInPlace()
        {
            if (_filteredRows == null || string.IsNullOrEmpty(_sortColumnKey)) return;

            var prop = typeof(SetRow).GetProperty(_sortColumnKey);
            if (prop == null) return;

            _filteredRows = _sortDirection == ListSortDirection.Descending
                ? _filteredRows.OrderByDescending(x => prop.GetValue(x, null)).ToList()
                : _filteredRows.OrderBy(x => prop.GetValue(x, null)).ToList();
        }

        /// <summary>Pre-fills the search box and applies the filter — called from global search.</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
        }

        /// <summary>Clears every filter control back to its default ("All"/"Default"/empty) state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            IsFuzzyResults = false;
            FuzzyNoticeText = "";

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _suppressCategoryFilterChange = true;
            foreach (var option in CategoryFilterOptions) option.IsChecked = false;
            _suppressCategoryFilterChange = false;
            UpdateCategoryFilterSummary();

            _fromDate = null;
            OnPropertyChanged(nameof(FromDate));
            _toDate = null;
            OnPropertyChanged(nameof(ToDate));

            bool hadSetFilters = !string.IsNullOrEmpty(_setCodeFilter) || !string.IsNullOrEmpty(_documentNumberFilter)
                || !string.IsNullOrEmpty(_companyFilter) || !string.IsNullOrEmpty(_departmentFilter)
                || !string.IsNullOrEmpty(_branchFilter) || !string.IsNullOrEmpty(_employeeFilter)
                || !string.IsNullOrEmpty(_referenceCodeFilter) || !string.IsNullOrEmpty(_parentTagFilter)
                || !string.IsNullOrEmpty(_reqIdFilter);
            _setFilterDebounceTimer.Stop();

            _setCodeFilter = string.Empty;
            OnPropertyChanged(nameof(SetCodeFilter));
            _documentNumberFilter = string.Empty;
            OnPropertyChanged(nameof(DocumentNumberFilter));
            _companyFilter = string.Empty;
            OnPropertyChanged(nameof(CompanyFilter));
            _departmentFilter = string.Empty;
            OnPropertyChanged(nameof(DepartmentFilter));
            _branchFilter = string.Empty;
            OnPropertyChanged(nameof(BranchFilter));
            _employeeFilter = string.Empty;
            OnPropertyChanged(nameof(EmployeeFilter));
            _referenceCodeFilter = string.Empty;
            OnPropertyChanged(nameof(ReferenceCodeFilter));
            _parentTagFilter = string.Empty;
            OnPropertyChanged(nameof(ParentTagFilter));
            _reqIdFilter = string.Empty;
            OnPropertyChanged(nameof(ReqIdFilter));

            _summaryFilter = SummaryFilter.All;
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
            OnPropertyChanged(nameof(IsWithQrCardActive));
            OnPropertyChanged(nameof(IsHardwareCardActive));
            OnPropertyChanged(nameof(IsSoftwareLicenseCardActive));
            OnPropertyChanged(nameof(IsServicesCardActive));

            _showConsumableItemsOnly = _consumableFilterDefault;
            OnPropertyChanged(nameof(ShowConsumableItemsOnly));

            bool needsReload = _showArchived || hadSetFilters;
            _showArchived = false;
            OnPropertyChanged(nameof(ShowArchived));

            if (needsReload) LoadSets();
            else ApplyFilters();
        }
    }
}
