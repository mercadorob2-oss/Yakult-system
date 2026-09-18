using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Renewal.ViewRenewals.ViewModels
{
    public sealed partial class RenewalPageViewModel
    {
        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded renewal —
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
        // (see FuzzyMatchRenewals) rather than an exact search-text match, so the view can show a
        // banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        // ── Status / Type / Show Archived ─────────────────────────────────────
        public ObservableCollection<string> StatusOptions { get; }

        private string _selectedStatus;
        public string SelectedStatus
        {
            get => _selectedStatus;
            set
            {
                if (!SetField(ref _selectedStatus, value)) return;
                OnPropertyChanged(nameof(IsAllCardActive));
                OnPropertyChanged(nameof(IsExpiredCardActive));
                OnPropertyChanged(nameof(IsExpiringCardActive));
                OnPropertyChanged(nameof(IsWarningCardActive));
                OnPropertyChanged(nameof(IsActiveCardActive));
                ApplyFilters();
            }
        }

        // ── Summary cards (clickable — toggle SelectedStatus, reusing the existing
        // Status filter pipeline rather than introducing a second filter dimension) ──
        public bool IsAllCardActive => string.IsNullOrEmpty(SelectedStatus) || SelectedStatus == "All";
        public bool IsExpiredCardActive => SelectedStatus == "Expired";
        public bool IsExpiringCardActive => SelectedStatus == "Expiring Soon";
        public bool IsWarningCardActive => SelectedStatus == "Warning";
        public bool IsActiveCardActive => SelectedStatus == "Active";

        private void ToggleStatusCard(string status)
        {
            SelectedStatus = SelectedStatus == status ? "All" : status;
        }

        public ObservableCollection<string> TypeOptions { get; }

        private string _selectedType;
        public string SelectedType
        {
            get => _selectedType;
            set { if (SetField(ref _selectedType, value)) ApplyFilters(); }
        }

        private bool _showArchived;
        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) ApplyFilters(); }
        }

        // ── Category multi-select filter (item categories found within each renewal's set) ──
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

            var allCategories = (_allRows ?? new List<RenewalRow>())
                .SelectMany(r => r.Categories)
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

        // ── Item Type multi-select filter (Hardware / Software License / Services) ──
        public ObservableCollection<CategoryFilterOption> ItemTypeFilterOptions { get; }

        private string _itemTypeFilterSummary = "All Item Types";
        public string ItemTypeFilterSummary
        {
            get => _itemTypeFilterSummary;
            private set => SetField(ref _itemTypeFilterSummary, value);
        }

        private bool _suppressItemTypeFilterChange;

        private void LoadItemTypeFilterOptions()
        {
            var previouslyChecked = ItemTypeFilterOptions
                .Where(o => o.IsChecked)
                .Select(o => o.Name)
                .ToList();

            var allItemTypes = (_allRows ?? new List<RenewalRow>())
                .SelectMany(r => r.ItemTypes)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressItemTypeFilterChange = true;
            try
            {
                ItemTypeFilterOptions.Clear();
                foreach (var name in allItemTypes)
                {
                    var option = new CategoryFilterOption(name)
                    {
                        IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase)
                    };
                    option.CheckedChanged += OnItemTypeFilterOptionChanged;
                    ItemTypeFilterOptions.Add(option);
                }
            }
            finally
            {
                _suppressItemTypeFilterChange = false;
            }

            UpdateItemTypeFilterSummary();
        }

        private void OnItemTypeFilterOptionChanged()
        {
            if (_suppressItemTypeFilterChange) return;
            UpdateItemTypeFilterSummary();
            ApplyFilters();
        }

        private void ClearItemTypeFilter()
        {
            _suppressItemTypeFilterChange = true;
            try
            {
                foreach (var option in ItemTypeFilterOptions) option.IsChecked = false;
            }
            finally
            {
                _suppressItemTypeFilterChange = false;
            }

            UpdateItemTypeFilterSummary();
            ApplyFilters();
        }

        private void UpdateItemTypeFilterSummary()
        {
            var checkedNames = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            ItemTypeFilterSummary = checkedNames.Count == 0
                ? "All Item Types"
                : checkedNames.Count == 1
                    ? checkedNames[0]
                    : $"{checkedNames.Count} item types selected";
        }

        // ── Sub-Type filter (Non-Subtype, or Subtype -> Contract/Subscription/License/Services) ──
        // Same fixed-checkbox shape as Invoice Reports' Sub-Type filter — see
        // InvoicePageViewModel.Filters.cs for the rationale. "Subtype" (any) and the four
        // specific children are mutually exclusive: checking one clears the other side.
        private bool _suppressSubTypeFilterChange;

        private bool _subTypeFilterNonSubType;
        public bool SubTypeFilterNonSubType
        {
            get => _subTypeFilterNonSubType;
            set { if (SetField(ref _subTypeFilterNonSubType, value)) OnSubTypeFilterChanged(); }
        }

        private bool _subTypeFilterAnySubtype;
        public bool SubTypeFilterAnySubtype
        {
            get => _subTypeFilterAnySubtype;
            set
            {
                if (!SetField(ref _subTypeFilterAnySubtype, value)) return;
                if (_suppressSubTypeFilterChange) return;

                if (value) SetSubTypeChildren(false);
                OnSubTypeFilterChanged();
            }
        }

        private bool _subTypeFilterContract;
        public bool SubTypeFilterContract
        {
            get => _subTypeFilterContract;
            set
            {
                if (!SetField(ref _subTypeFilterContract, value)) return;
                if (_suppressSubTypeFilterChange) return;

                if (value) SubTypeFilterAnySubtype = false;
                OnSubTypeFilterChanged();
            }
        }

        private bool _subTypeFilterSubscription;
        public bool SubTypeFilterSubscription
        {
            get => _subTypeFilterSubscription;
            set
            {
                if (!SetField(ref _subTypeFilterSubscription, value)) return;
                if (_suppressSubTypeFilterChange) return;

                if (value) SubTypeFilterAnySubtype = false;
                OnSubTypeFilterChanged();
            }
        }

        private bool _subTypeFilterLicense;
        public bool SubTypeFilterLicense
        {
            get => _subTypeFilterLicense;
            set
            {
                if (!SetField(ref _subTypeFilterLicense, value)) return;
                if (_suppressSubTypeFilterChange) return;

                if (value) SubTypeFilterAnySubtype = false;
                OnSubTypeFilterChanged();
            }
        }

        private bool _subTypeFilterServices;
        public bool SubTypeFilterServices
        {
            get => _subTypeFilterServices;
            set
            {
                if (!SetField(ref _subTypeFilterServices, value)) return;
                if (_suppressSubTypeFilterChange) return;

                if (value) SubTypeFilterAnySubtype = false;
                OnSubTypeFilterChanged();
            }
        }

        private void SetSubTypeChildren(bool value)
        {
            _suppressSubTypeFilterChange = true;
            try
            {
                SubTypeFilterContract = value;
                SubTypeFilterSubscription = value;
                SubTypeFilterLicense = value;
                SubTypeFilterServices = value;
            }
            finally
            {
                _suppressSubTypeFilterChange = false;
            }
        }

        private string _subTypeFilterSummary = "All Sub-Types";
        public string SubTypeFilterSummary
        {
            get => _subTypeFilterSummary;
            private set => SetField(ref _subTypeFilterSummary, value);
        }

        private void OnSubTypeFilterChanged()
        {
            UpdateSubTypeFilterSummary();
            ApplyFilters();
        }

        private void UpdateSubTypeFilterSummary()
        {
            var names = new List<string>();
            if (SubTypeFilterNonSubType) names.Add("Non-Subtype");
            if (SubTypeFilterAnySubtype) names.Add("Subtype (Any)");
            if (SubTypeFilterContract) names.Add("Contract");
            if (SubTypeFilterSubscription) names.Add("Subscription");
            if (SubTypeFilterLicense) names.Add("License");
            if (SubTypeFilterServices) names.Add("Services");

            SubTypeFilterSummary = names.Count == 0
                ? "All Sub-Types"
                : names.Count == 1
                    ? names[0]
                    : $"{names.Count} sub-types selected";
        }

        private void ClearSubTypeFilter()
        {
            _subTypeFilterNonSubType = false;
            _subTypeFilterAnySubtype = false;
            _subTypeFilterContract = false;
            _subTypeFilterSubscription = false;
            _subTypeFilterLicense = false;
            _subTypeFilterServices = false;
            OnPropertyChanged(nameof(SubTypeFilterNonSubType));
            OnPropertyChanged(nameof(SubTypeFilterAnySubtype));
            OnPropertyChanged(nameof(SubTypeFilterContract));
            OnPropertyChanged(nameof(SubTypeFilterSubscription));
            OnPropertyChanged(nameof(SubTypeFilterLicense));
            OnPropertyChanged(nameof(SubTypeFilterServices));

            UpdateSubTypeFilterSummary();
            ApplyFilters();
        }

        // ── Column popup date-range filters (Start Date / End Date headers) ──
        public bool HasStartDateFilter => _dateFilters.ContainsKey("StartDate");
        public bool HasEndDateFilter => _dateFilters.ContainsKey("EndDate");

        public Tuple<DateTime?, DateTime?> GetDateFilter(string columnName)
        {
            _dateFilters.TryGetValue(columnName, out var v);
            return v;
        }

        public void SetDateFilter(string columnName, DateTime? from, DateTime? to)
        {
            if (from.HasValue || to.HasValue)
                _dateFilters[columnName] = Tuple.Create(from, to);
            else
                _dateFilters.Remove(columnName);

            OnPropertyChanged(nameof(HasStartDateFilter));
            OnPropertyChanged(nameof(HasEndDateFilter));
            ApplyFilters();
        }

        public void ClearDateFilter(string columnName)
        {
            _dateFilters.Remove(columnName);
            OnPropertyChanged(nameof(HasStartDateFilter));
            OnPropertyChanged(nameof(HasEndDateFilter));
            ApplyFilters();
        }

        // ── Document Date filter (toolbar) — flexible: match by exact day, by
        // month+year, by year alone, or any combination, via three independent
        // "Any"-able dropdowns. Multiple rows can be added; a document date matches
        // if it satisfies ANY constrained row (OR'd together). ──
        public ObservableCollection<DocumentDateFilterRow> DocumentDateFilterRows { get; } = new ObservableCollection<DocumentDateFilterRow>();

        // Only the first 3 rows render inline in the Advanced Filters popup — beyond that it
        // starts overlapping the rest of the page. A "More" link opens DocumentDateOverflowWindow,
        // which binds directly to DocumentDateFilterRows itself (not a snapshot), so edits there
        // stay in sync with the popup and the applied filter automatically.
        public IEnumerable<DocumentDateFilterRow> VisibleDocumentDateFilterRows => DocumentDateFilterRows.Take(3);
        public bool HasMoreDocumentDateFilterRows => DocumentDateFilterRows.Count > 3;
        public int MoreDocumentDateFilterRowsCount => Math.Max(0, DocumentDateFilterRows.Count - 3);

        public RelayCommand AddDocumentDateRowCommand { get; private set; }

        private void InitDocumentDateFilterRows()
        {
            DocumentDateFilterRows.CollectionChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(VisibleDocumentDateFilterRows));
                OnPropertyChanged(nameof(HasMoreDocumentDateFilterRows));
                OnPropertyChanged(nameof(MoreDocumentDateFilterRowsCount));
            };
            AddDocumentDateRowCommand = new RelayCommand(AddDocumentDateRow);
            AddDocumentDateRow();
        }

        private void AddDocumentDateRow()
        {
            var row = new DocumentDateFilterRow();
            row.SetYearOptions(GetDocumentYears());
            row.Changed += ApplyFilters;
            row.RemoveCommand = new RelayCommand(() => RemoveDocumentDateRow(row), () => DocumentDateFilterRows.Count > 1);
            DocumentDateFilterRows.Add(row);
        }

        private void RemoveDocumentDateRow(DocumentDateFilterRow row)
        {
            if (DocumentDateFilterRows.Count <= 1) return;
            row.Changed -= ApplyFilters;
            DocumentDateFilterRows.Remove(row);
            ApplyFilters();
        }

        private IEnumerable<int> GetDocumentYears()
        {
            return (_allRows ?? new List<RenewalRow>())
                .Where(r => r.DocumentDate.HasValue)
                .Select(r => r.DocumentDate.Value.Year);
        }

        private void LoadDocumentYearOptions()
        {
            var years = GetDocumentYears().ToList();
            foreach (var row in DocumentDateFilterRows)
                row.SetYearOptions(years);
        }

        // ── Set-linked filters (Set Code, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) — mirrors ItemsPageViewModel's Set-linked filters.
        // Set Code/Document #/Company/Department/Branch come straight off vw_RenewalStatus;
        // Employee/Reference Code/Parent Tag/Req ID require joins the repository builds only
        // when the corresponding field is non-empty (see RenewalRepository.GetAllRenewals).
        // All nine are resolved server-side via RenewalRepository.GetAllRenewals, so typing
        // triggers a debounced reload (300ms DispatcherTimer, see RenewalPageViewModel.cs)
        // instead of the client-side ApplyFilters() used by the other filters above.
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

        /// <summary>Every field the quick search box matches against — a renewal matches if all
        /// typed terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private IEnumerable<string> RenewalSearchFields(RenewalRow r)
        {
            var fields = new List<string>
            {
                r.SetCode, r.DocumentNumber, r.Dto.ReferenceNumber, r.CompanyName, r.Dto.VendorName,
                r.Dto.SiteName, r.Dto.CurrentBranchName, r.Dto.CurrentDepartmentName, r.Dto.Remarks,
                r.Dto.RenewalNotes, r.SetType, r.TotalAmountDue.ToString(),
                r.DaysUntilExpiry.HasValue ? r.DaysUntilExpiry.Value.ToString() : null
            };
            fields.AddRange(r.SubTypeReferenceCodes);
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(r.SetId, out var names)) fields.AddRange(names);
            return fields;
        }

        /// <summary>Identifier-shaped fields only (SetCode/Document #/Reference #/Sub-Type ref codes)
        /// — the subset it's meaningful to run an edit-distance comparison against for a code-shaped typo.</summary>
        private static IEnumerable<string> RenewalCodeFuzzyCandidateFields(RenewalRow r)
        {
            var fields = new List<string> { r.SetCode, r.DocumentNumber, r.Dto.ReferenceNumber };
            fields.AddRange(r.SubTypeReferenceCodes);
            return fields;
        }

        /// <summary>Company/Vendor/item names — used when the mistyped term itself is a descriptive
        /// word (e.g. a brand: "Xiaoomi" vs "Xiaomi") rather than a code.</summary>
        private IEnumerable<string> RenewalDescriptiveFuzzyCandidateFields(RenewalRow r)
        {
            var fields = new List<string> { r.CompanyName, r.Dto.VendorName };
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(r.SetId, out var names)) fields.AddRange(names);
            return fields;
        }

        /// <summary>Type/Show-Archived/Category/Item-Type/Sub-Type/date filters — every active filter
        /// EXCEPT the quick search text and the status-card toggle. Shared between the normal
        /// pipeline and the fuzzy fallback so "closest match" results still respect every other
        /// filter the user has set.</summary>
        private IEnumerable<RenewalRow> ApplyStructuralFilters(IEnumerable<RenewalRow> source)
        {
            var filtered = source;

            if (!string.IsNullOrEmpty(SelectedType) && SelectedType != "All")
                filtered = filtered.Where(r => r.SetType == SelectedType);

            if (!ShowArchived)
                filtered = filtered.Where(r => r.Active);

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(r =>
                    r.Categories.Any(c => selectedCategories.Contains(c, StringComparer.OrdinalIgnoreCase)));
            }

            var selectedItemTypes = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedItemTypes.Count > 0)
            {
                filtered = filtered.Where(r =>
                    r.ItemTypes.Any(t => selectedItemTypes.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            var selectedSubTypes = new List<string>();
            if (SubTypeFilterContract) selectedSubTypes.Add("Contract");
            if (SubTypeFilterSubscription) selectedSubTypes.Add("Subscription");
            if (SubTypeFilterLicense) selectedSubTypes.Add("License");
            if (SubTypeFilterServices) selectedSubTypes.Add("Services");

            if (SubTypeFilterNonSubType || SubTypeFilterAnySubtype || selectedSubTypes.Count > 0)
            {
                filtered = filtered.Where(r =>
                    (SubTypeFilterNonSubType && r.HasNonSubTypeItems) ||
                    (SubTypeFilterAnySubtype && r.SubTypes.Count > 0) ||
                    (selectedSubTypes.Count > 0 && r.SubTypes.Any(t => selectedSubTypes.Contains(t, StringComparer.OrdinalIgnoreCase))));
            }

            foreach (var kvp in _dateFilters)
            {
                var from = kvp.Value.Item1;
                var to = kvp.Value.Item2;
                if (kvp.Key == "StartDate")
                {
                    if (from.HasValue) filtered = filtered.Where(r => r.StartDate.HasValue && r.StartDate.Value.Date >= from.Value.Date);
                    if (to.HasValue) filtered = filtered.Where(r => r.StartDate.HasValue && r.StartDate.Value.Date <= to.Value.Date);
                }
                else if (kvp.Key == "EndDate")
                {
                    if (from.HasValue) filtered = filtered.Where(r => r.EndDate.HasValue && r.EndDate.Value.Date >= from.Value.Date);
                    if (to.HasValue) filtered = filtered.Where(r => r.EndDate.HasValue && r.EndDate.Value.Date <= to.Value.Date);
                }
            }

            var constrainedDateRows = DocumentDateFilterRows.Where(row => row.HasAnyConstraint).ToList();
            if (constrainedDateRows.Count > 0)
                filtered = filtered.Where(r => constrainedDateRows.Any(row => row.Matches(r.DocumentDate)));

            return filtered;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (SetCode/Document #/Reference #) and a descriptive
        /// typo (Company/Vendor/item name). Every OTHER term still has to match exactly somewhere.
        /// Discards anything past edit distance 2.
        /// </summary>
        private List<RenewalRow> FuzzyMatchRenewals(List<RenewalRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(r => RenewalSearchFields(r).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            if (fuzzTerm == null) return new List<RenewalRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<RenewalRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<RenewalRow, IEnumerable<string>>)RenewalCodeFuzzyCandidateFields
                : RenewalDescriptiveFuzzyCandidateFields;

            return pool
                .Where(r => SearchTextHelper.MatchesAllTerms(otherTerms, RenewalSearchFields(r)))
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
                .ThenBy(x => x.Row.SetCode)
                .Take(maxResults)
                .Select(x => x.Row)
                .ToList();
        }

        // ── Core filter pipeline ─────────────────────────────────────────────
        public void ApplyFilters()
        {
            if (_allRows == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(SearchText);

            var structuralPool = ApplyStructuralFilters(_allRows).ToList();

            IEnumerable<RenewalRow> filtered = structuralPool;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(r => SearchTextHelper.MatchesAllTerms(searchTerms, RenewalSearchFields(r)));

            var afterSearch = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path is completely unaffected. Relaxes just the search text; every other
            // active filter still applies via the same structural pool.
            bool isFuzzy = false;
            if (afterSearch.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchRenewals(structuralPool, searchTerms);
                if (fuzzyMatches.Count > 0)
                {
                    isFuzzy = true;
                    afterSearch = fuzzyMatches;
                }
            }
            IsFuzzyResults = isFuzzy;
            FuzzyNoticeText = isFuzzy
                ? $"No exact matches for \"{SearchText}\" — showing the closest results instead."
                : "";

            // Fuzzy results are already the final display set — the status-card filter below
            // shouldn't further slice a handful of "closest match" guesses.
            if (!isFuzzy && !string.IsNullOrEmpty(SelectedStatus) && SelectedStatus != "All")
                afterSearch = afterSearch.Where(r => r.ExpiryStatus == SelectedStatus).ToList();

            _filteredRows = afterSearch;
            CurrentPage = 1;
            BindPage();
            RecomputeSelectedCount();
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

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click) — toggles direction.</summary>
        public void SortByColumn(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            var direction = (_sortColumnKey == columnKey && _sortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            SortByColumn(columnKey, direction);
        }

        /// <summary>Called by the date-range popup's explicit Sort Ascending/Descending actions.</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortColumnKey = columnKey;
            _sortDirection = direction;

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

            var prop = typeof(RenewalRow).GetProperty(_sortColumnKey);
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

        /// <summary>Clears every filter control back to its default ("All"/"Any"/empty) state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));
            IsFuzzyResults = false;
            FuzzyNoticeText = "";

            _selectedStatus = "All";
            OnPropertyChanged(nameof(SelectedStatus));
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
            OnPropertyChanged(nameof(IsExpiringCardActive));
            OnPropertyChanged(nameof(IsWarningCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));

            _selectedType = "All";
            OnPropertyChanged(nameof(SelectedType));

            _showArchived = false;
            OnPropertyChanged(nameof(ShowArchived));

            _suppressCategoryFilterChange = true;
            foreach (var option in CategoryFilterOptions) option.IsChecked = false;
            _suppressCategoryFilterChange = false;
            UpdateCategoryFilterSummary();

            _suppressItemTypeFilterChange = true;
            foreach (var option in ItemTypeFilterOptions) option.IsChecked = false;
            _suppressItemTypeFilterChange = false;
            UpdateItemTypeFilterSummary();

            _subTypeFilterNonSubType = false;
            _subTypeFilterAnySubtype = false;
            _subTypeFilterContract = false;
            _subTypeFilterSubscription = false;
            _subTypeFilterLicense = false;
            _subTypeFilterServices = false;
            OnPropertyChanged(nameof(SubTypeFilterNonSubType));
            OnPropertyChanged(nameof(SubTypeFilterAnySubtype));
            OnPropertyChanged(nameof(SubTypeFilterContract));
            OnPropertyChanged(nameof(SubTypeFilterSubscription));
            OnPropertyChanged(nameof(SubTypeFilterLicense));
            OnPropertyChanged(nameof(SubTypeFilterServices));
            UpdateSubTypeFilterSummary();

            _dateFilters.Clear();
            OnPropertyChanged(nameof(HasStartDateFilter));
            OnPropertyChanged(nameof(HasEndDateFilter));

            while (DocumentDateFilterRows.Count > 1)
            {
                var last = DocumentDateFilterRows[DocumentDateFilterRows.Count - 1];
                last.Changed -= ApplyFilters;
                DocumentDateFilterRows.RemoveAt(DocumentDateFilterRows.Count - 1);
            }
            DocumentDateFilterRows[0].Reset();

            // Set-linked filters require a SQL reload (see QueueSetFilterReload above), so stop
            // any pending debounce and go straight to LoadRenewalsAsync() — which also re-runs
            // ApplyFilters()/UpdateSummary() once the reload finishes — rather than calling
            // ApplyFilters() twice.
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

            _ = LoadRenewalsAsync();
        }
    }
}
