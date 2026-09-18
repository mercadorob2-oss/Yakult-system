using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Request.ViewModels
{
    public sealed partial class RequestPageViewModel
    {
        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded request —
        // cheap for one keystroke, but calling it synchronously on EVERY keystroke (no debounce)
        // meant holding a key down (or pasting/spamming characters) blocked the UI thread once per
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
        // (see FuzzyMatchRequests) rather than an exact search-text match, so the view can show a
        // banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        // ── Set/Invoice + Request-linked filters (SetCode, Document #, Company, Department,
        // Branch, Employee, Reference Code, Parent Tag, Req ID) ──────────────────────────────
        // Unlike Search/Status/Category/Date, these are resolved server-side in
        // RequestRepository.GetAllRequests() (see that method's WHERE-clause construction) rather
        // than re-filtered over the already-loaded _allRows, mirroring ItemsPageViewModel's
        // "Set-linked filters" pattern. Typing triggers a debounced reload (300ms) instead of an
        // immediate ApplyFilter() call.
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

        // Matches the requester (dbo.Request.EmpId -> dbo.Employee.Name, already joined as `e`),
        // not "Received By" (`rcv`) — the requester is the primary actor on a Request row and is
        // what the grid's own "Employee" column already displays (EmployeeName).
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

        // ── Date requested range ─────────────────────────────────────────────
        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetField(ref _fromDate, value)) ApplyFilter(); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetField(ref _toDate, value)) ApplyFilter(); }
        }

        // ── Status filter ────────────────────────────────────────────────────
        public ObservableCollection<string> StatusOptions { get; }

        private string _selectedStatus;
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (!SetField(ref _selectedStatus, value)) return;
                NotifyStatusCardsChanged();
                ApplyFilter();
            }
        }

        // ── Clickable summary cards (drive SelectedStatus; clicking the active card resets to "All") ──
        public bool IsAllCardActive => string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == "All";
        public bool IsUnderReviewCardActive => SelectedStatus == "Under Review";
        public bool IsOnHoldCardActive => SelectedStatus == "On Hold";
        public bool IsSubmittedCardActive => SelectedStatus == "Submitted";
        public bool IsCompletedCardActive => SelectedStatus == "Completed";

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand UnderReviewCardCommand { get; private set; }
        public RelayCommand OnHoldCardCommand { get; private set; }
        public RelayCommand SubmittedCardCommand { get; private set; }
        public RelayCommand CompletedCardCommand { get; private set; }

        private void InitStatusCardCommands()
        {
            TotalCardCommand = new RelayCommand(() => SelectedStatus = "All");
            UnderReviewCardCommand = new RelayCommand(() => ToggleStatusCard("Under Review"));
            OnHoldCardCommand = new RelayCommand(() => ToggleStatusCard("On Hold"));
            SubmittedCardCommand = new RelayCommand(() => ToggleStatusCard("Submitted"));
            CompletedCardCommand = new RelayCommand(() => ToggleStatusCard("Completed"));
        }

        private void ToggleStatusCard(string status)
        {
            SelectedStatus = SelectedStatus == status ? "All" : status;
        }

        private void NotifyStatusCardsChanged()
        {
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsUnderReviewCardActive));
            OnPropertyChanged(nameof(IsOnHoldCardActive));
            OnPropertyChanged(nameof(IsSubmittedCardActive));
            OnPropertyChanged(nameof(IsCompletedCardActive));
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
                _filteredRows = _filteredRows.OrderByDescending(r => r.DateRequested).ToList();
            else if (SelectedFilterBy == "Oldest Added")
                _filteredRows = _filteredRows.OrderBy(r => r.DateRequested).ToList();
            // "Default" leaves the current (search/status-filtered) order untouched.

            BindPage();
        }

        /// <summary>Every field the quick search box matches against — a request matches if all
        /// typed terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private static IEnumerable<string> RequestSearchFields(RequestRow r) => new[]
        {
            r.EmployeeName, r.ItemName, r.ModelNumber, r.SerialNumber, r.Category,
            r.Dto.Description, r.Dto.Remarks, r.Dto.EntryType, r.CreatedByName, r.ModifiedByName
        };

        /// <summary>Identifier-shaped fields only (Model/Serial Number) — the subset it's meaningful
        /// to run an edit-distance comparison against for a code-shaped typo.</summary>
        private static IEnumerable<string> RequestCodeFuzzyCandidateFields(RequestRow r) => new[]
        {
            r.ModelNumber, r.SerialNumber
        };

        /// <summary>Item name only — used when the mistyped term itself is a descriptive word (e.g. a
        /// brand: "Xiaoomi" vs "Xiaomi") rather than a code. Deliberately narrower than the full
        /// descriptive field set: Employee/Remarks/etc. are short or generic enough that fuzzing
        /// against them risks matching unrelated requests by coincidence.</summary>
        private static IEnumerable<string> RequestDescriptiveFuzzyCandidateFields(RequestRow r) => new[]
        {
            r.ItemName
        };

        /// <summary>Date-range/Category filters — every active filter EXCEPT the quick search text and
        /// the status-card toggle. Shared between the normal pipeline and the fuzzy fallback so
        /// "closest match" results still respect every other filter the user has set.</summary>
        private IEnumerable<RequestRow> ApplyStructuralFilters(IEnumerable<RequestRow> source)
        {
            var filtered = source;

            if (FromDate.HasValue)
            {
                var from = FromDate.Value.Date;
                filtered = filtered.Where(r => r.DateRequested.HasValue && r.DateRequested.Value.Date >= from);
            }
            if (ToDate.HasValue)
            {
                var to = ToDate.Value.Date;
                filtered = filtered.Where(r => r.DateRequested.HasValue && r.DateRequested.Value.Date <= to);
            }

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(r => r.Category != null &&
                    selectedCategories.Contains(r.Category, StringComparer.OrdinalIgnoreCase));
            }

            return filtered;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (Model/Serial Number) and a descriptive/item-name
        /// typo. Every OTHER term still has to match exactly somewhere. Candidates come from
        /// <paramref name="pool"/> — the rows already passing every OTHER active filter. Discards
        /// anything past edit distance 2.
        /// </summary>
        private List<RequestRow> FuzzyMatchRequests(List<RequestRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(r => RequestSearchFields(r).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            if (fuzzTerm == null) return new List<RequestRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<RequestRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<RequestRow, IEnumerable<string>>)RequestCodeFuzzyCandidateFields
                : RequestDescriptiveFuzzyCandidateFields;

            return pool
                .Where(r => SearchTextHelper.MatchesAllTerms(otherTerms, RequestSearchFields(r)))
                .Select(r => new
                {
                    Row = r,
                    Distance = fuzzFields(r)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f => SearchTextHelper.MinWordDistance(fuzzTerm, f))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min()
                })
                .Where(x => x.Distance <= 2)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Row.ItemName)
                .Take(maxResults)
                .Select(x => x.Row)
                .ToList();
        }

        // ── Core search + status filter pipeline ─────────────────────────────
        public void ApplyFilter()
        {
            if (_allRows == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(SearchText);

            var structuralPool = ApplyStructuralFilters(_allRows).ToList();

            IEnumerable<RequestRow> filtered = structuralPool;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(r => SearchTextHelper.MatchesAllTerms(searchTerms, RequestSearchFields(r)));

            // Snapshot for the summary cards is taken here — after search/date/category filters
            // but BEFORE the status-card filter below — so clicking a card doesn't zero out the
            // other cards' counts (same bug class fixed on Invoice Reports).
            var forSummary = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path is completely unaffected. Relaxes just the search text; every other
            // active filter still applies via the same structural pool.
            bool isFuzzy = false;
            if (forSummary.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchRequests(structuralPool, searchTerms);
                if (fuzzyMatches.Count > 0)
                {
                    isFuzzy = true;
                    forSummary = fuzzyMatches;
                }
            }
            IsFuzzyResults = isFuzzy;
            FuzzyNoticeText = isFuzzy
                ? $"No exact matches for \"{SearchText}\" — showing the closest results instead."
                : "";

            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            if (!isFuzzy && !string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "All")
            {
                filtered = filtered.Where(r => r.Status == SelectedStatus);
            }

            _filteredRows = filtered.ToList();
            CurrentPage = 1;
            BindPage();
        }

        private void UpdateSummaryCards(List<RequestRow> list)
        {
            TotalCount = list.Count;
            UnderReviewCount = list.Count(r => string.Equals((r.Status ?? "").Trim(), "Under Review", StringComparison.OrdinalIgnoreCase));
            OnHoldCount = list.Count(r => string.Equals((r.Status ?? "").Trim(), "On Hold", StringComparison.OrdinalIgnoreCase));
            SubmittedCount = list.Count(r => string.Equals((r.Status ?? "").Trim(), "Submitted", StringComparison.OrdinalIgnoreCase));
            CompletedCount = list.Count(r => string.Equals((r.Status ?? "").Trim(), "Completed", StringComparison.OrdinalIgnoreCase));

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(UnderReviewCount));
            OnPropertyChanged(nameof(OnHoldCount));
            OnPropertyChanged(nameof(SubmittedCount));
            OnPropertyChanged(nameof(CompletedCount));
        }

        // ── Category multi-select filter (values driven by the currently loaded request rows) ──
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

            var allCategories = (_allRows ?? new List<RequestRow>())
                .Select(r => r.Category)
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
            ApplyFilter();
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
            ApplyFilter();
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

        public int TotalCount { get; private set; }
        public int UnderReviewCount { get; private set; }
        public int OnHoldCount { get; private set; }
        public int SubmittedCount { get; private set; }
        public int CompletedCount { get; private set; }

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

            var prop = typeof(RequestRow).GetProperty(_sortColumnKey);
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

            _fromDate = null;
            OnPropertyChanged(nameof(FromDate));
            _toDate = null;
            OnPropertyChanged(nameof(ToDate));

            _selectedStatus = "All";
            OnPropertyChanged(nameof(SelectedStatus));
            NotifyStatusCardsChanged();

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _suppressCategoryFilterChange = true;
            foreach (var option in CategoryFilterOptions) option.IsChecked = false;
            _suppressCategoryFilterChange = false;
            UpdateCategoryFilterSummary();

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

            LoadRequests();
        }
    }
}
