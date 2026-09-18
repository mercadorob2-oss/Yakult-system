using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    public sealed partial class InvoicePageViewModel
    {
        private enum ExpiryFilterMode
        {
            All = 0,
            Expired = 1,
            Expiring90 = 2,
            Active = 3,
            NoEndDate = 4
        }

        private ExpiryFilterMode _expiryFilter = ExpiryFilterMode.All;

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded invoice —
        // cheap for one keystroke, but calling it synchronously on EVERY keystroke (no debounce)
        // meant holding a key down (or pasting/spamming characters) blocked the UI thread once per
        // character, which is what made typing/erasing visibly lag. Debouncing it the same way the
        // Set-linked filters below already are fixes that without changing the search itself.
        private readonly DispatcherTimer _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        // ── Search / Filter By ──────────────────────────────────────────────
        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); } }
        }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set { if (SetField(ref _selectedFilterBy, value)) ApplyFilters(resetPage: true); }
        }

        public ObservableCollection<string> FilterByOptions { get; }

        // ── Category multi-select filter (item categories found within each invoice's set) ──
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

            var allCategories = (_allRows ?? new List<InvoiceRow>())
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
            ApplyFilters(resetPage: true);
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
            ApplyFilters(resetPage: true);
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

            var allItemTypes = (_allRows ?? new List<InvoiceRow>())
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
            ApplyFilters(resetPage: true);
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
            ApplyFilters(resetPage: true);
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
        // A flat set of fixed checkboxes rather than a dynamic CategoryFilterOption list —
        // there are exactly four Sub-Types (ItemSubTypeCatalog.ValidSubTypes) plus the fixed
        // "Non-Subtype" bucket, so nothing needs to be discovered/rebuilt from loaded rows the
        // way Category/Item Type are. "Subtype" is itself checkable (matches any invoice with
        // at least one Sub-Type item, unspecified which) — it and the four specific children are
        // mutually exclusive: checking one clears the other side, since picking a specific
        // child is strictly more precise than "any Sub-Type" and the two would otherwise say
        // conflicting things about the same filter.
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

        /// <summary>Sets all four child checkboxes at once without each one individually
        /// trying to clear the parent — used when the parent itself is checked.</summary>
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
            ApplyFilters(resetPage: true);
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
            ApplyFilters(resetPage: true);
        }

        // ── Set-linked filters (SetCode, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) ── mirrors the View Items page's Set-linked
        // filters. Base entity here is already dbo.[Set] itself, but Employee/Reference Code/
        // Parent Tag/Req ID aren't columns on InvoiceRow (or need a per-row EXISTS across many
        // dbo.SetItem rows), so all nine are resolved server-side in GetAllInvoicesAsync and
        // typing triggers a debounced full reload (same 300ms DispatcherTimer pattern as
        // ItemsPageViewModel) instead of the client-side ApplyFilters() used elsewhere on this page.
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

        // ── Document date range + quick range ────────────────────────────────
        private DateTime? _fromDate;
        public DateTime? FromDate
        {
            get => _fromDate;
            set { if (SetField(ref _fromDate, value)) ApplyFilters(resetPage: true); }
        }

        private DateTime? _toDate;
        public DateTime? ToDate
        {
            get => _toDate;
            set { if (SetField(ref _toDate, value)) ApplyFilters(resetPage: true); }
        }

        public ObservableCollection<string> QuickRangeOptions { get; }

        private string _selectedQuickRange;
        public string SelectedQuickRange
        {
            get => _selectedQuickRange;
            set
            {
                if (!SetField(ref _selectedQuickRange, value)) return;
                ApplyQuickRange();
            }
        }

        private void ApplyQuickRange()
        {
            var now = DateTime.Today;
            DateTime? from = null;
            DateTime? to = null;

            switch (SelectedQuickRange)
            {
                case "Last 7 days": from = now.AddDays(-7); to = now; break;
                case "Last 30 days": from = now.AddDays(-30); to = now; break;
                case "Last 90 days": from = now.AddDays(-90); to = now; break;
                case "This month": from = new DateTime(now.Year, now.Month, 1); to = now; break;
                case "This year": from = new DateTime(now.Year, 1, 1); to = now; break;
                default: from = null; to = null; break; // "All"
            }

            _fromDate = from;
            _toDate = to;
            OnPropertyChanged(nameof(FromDate));
            OnPropertyChanged(nameof(ToDate));

            ApplyFilters(resetPage: true);
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
            row.Changed += OnDocumentDateRowChanged;
            row.RemoveCommand = new RelayCommand(() => RemoveDocumentDateRow(row), () => DocumentDateFilterRows.Count > 1);
            DocumentDateFilterRows.Add(row);
        }

        private void RemoveDocumentDateRow(DocumentDateFilterRow row)
        {
            if (DocumentDateFilterRows.Count <= 1) return;
            row.Changed -= OnDocumentDateRowChanged;
            DocumentDateFilterRows.Remove(row);
            ApplyFilters(resetPage: true);
        }

        private void OnDocumentDateRowChanged() => ApplyFilters(resetPage: true);

        private IEnumerable<int> GetDocumentYears()
        {
            return (_allRows ?? new List<InvoiceRow>())
                .Where(r => r.DocumentDate.HasValue)
                .Select(r => r.DocumentDate.Value.Year);
        }

        private void LoadDocumentYearOptions()
        {
            var years = GetDocumentYears().ToList();
            foreach (var row in DocumentDateFilterRows)
                row.SetYearOptions(years);
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
            ApplyFilters(resetPage: true);
        }

        public void ClearDateFilter(string columnName)
        {
            _dateFilters.Remove(columnName);
            OnPropertyChanged(nameof(HasStartDateFilter));
            OnPropertyChanged(nameof(HasEndDateFilter));
            ApplyFilters(resetPage: true);
        }

        // ── Expiry summary cards (clickable filters) ─────────────────────────
        public int TotalCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int ExpiringCount { get; private set; }
        public int ActiveCount { get; private set; }
        public int NoEndDateCount { get; private set; }

        public bool IsAllCardActive => _expiryFilter == ExpiryFilterMode.All;
        public bool IsExpiredCardActive => _expiryFilter == ExpiryFilterMode.Expired;
        public bool IsExpiringCardActive => _expiryFilter == ExpiryFilterMode.Expiring90;
        public bool IsActiveCardActive => _expiryFilter == ExpiryFilterMode.Active;
        public bool IsNoEndDateCardActive => _expiryFilter == ExpiryFilterMode.NoEndDate;

        private void ToggleCard(ExpiryFilterMode mode)
        {
            _expiryFilter = _expiryFilter == mode ? ExpiryFilterMode.All : mode;
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
            OnPropertyChanged(nameof(IsExpiringCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsNoEndDateCardActive));
            ApplyFilters(resetPage: true);
        }

        public RelayCommand TotalCardCommand { get; }
        public RelayCommand ExpiredCardCommand { get; }
        public RelayCommand ExpiringCardCommand { get; }
        public RelayCommand ActiveCardCommand { get; }
        public RelayCommand NoEndDateCardCommand { get; }

        // ── Totals bar + active-filters label ────────────────────────────────
        private string _totalsText = "";
        public string TotalsText
        {
            get => _totalsText;
            private set => SetField(ref _totalsText, value);
        }

        private string _activeFiltersText = "";
        public string ActiveFiltersText
        {
            get => _activeFiltersText;
            private set => SetField(ref _activeFiltersText, value);
        }

        // Set when the rows currently on screen came from the "Did you mean...?" fuzzy fallback
        // (see FuzzyMatchInvoices) rather than an exact search-text match, so the view can show a
        // banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        private void UpdateTotalsBar(List<InvoiceRow> list)
        {
            if (list == null || list.Count == 0) { TotalsText = ""; return; }

            decimal subtotal = list.Sum(x => x.Subtotal);
            decimal vat = list.Sum(x => x.VatAmount);
            decimal wht = list.Sum(x => x.WhtAmount);
            decimal discount = list.Sum(x => x.DiscountAmount);
            decimal due = list.Sum(x => x.TotalAmountDue);

            TotalsText = $"Subtotal: {subtotal:N2}    VAT: {vat:N2}    WHT: {wht:N2}    Discount: {discount:N2}    Total Due: {due:N2}";
        }

        private void UpdateActiveFiltersLabel()
        {
            var parts = new List<string>();
            if (_expiryFilter != ExpiryFilterMode.All) parts.Add($"Status: {_expiryFilter}");
            if (FromDate.HasValue) parts.Add($"From: {FromDate.Value:MM/dd/yyyy}");
            if (ToDate.HasValue) parts.Add($"To: {ToDate.Value:MM/dd/yyyy}");
            var constrainedRows = DocumentDateFilterRows.Where(r => r.HasAnyConstraint).ToList();
            if (constrainedRows.Count > 0)
            {
                var rowTexts = constrainedRows.Select(row =>
                {
                    var docParts = new List<string>();
                    if (row.SelectedMonth != "Any") docParts.Add(row.SelectedMonth);
                    if (row.SelectedDay != "Any") docParts.Add(row.SelectedDay);
                    if (row.SelectedYear != "Any") docParts.Add(row.SelectedYear);
                    return string.Join(" ", docParts);
                });
                parts.Add($"Doc Date: {string.Join(" OR ", rowTexts)}");
            }
            if (!string.Equals(CategoryFilterSummary, "All Categories", StringComparison.OrdinalIgnoreCase)) parts.Add($"Category: {CategoryFilterSummary}");
            if (!string.Equals(ItemTypeFilterSummary, "All Item Types", StringComparison.OrdinalIgnoreCase)) parts.Add($"Item Type: {ItemTypeFilterSummary}");
            if (!string.Equals(SubTypeFilterSummary, "All Sub-Types", StringComparison.OrdinalIgnoreCase)) parts.Add($"Sub-Type: {SubTypeFilterSummary}");
            if (!string.IsNullOrWhiteSpace(SetCodeFilter)) parts.Add($"Set Code: {SetCodeFilter}");
            if (!string.IsNullOrWhiteSpace(DocumentNumberFilter)) parts.Add($"Document #: {DocumentNumberFilter}");
            if (!string.IsNullOrWhiteSpace(CompanyFilter)) parts.Add($"Company: {CompanyFilter}");
            if (!string.IsNullOrWhiteSpace(DepartmentFilter)) parts.Add($"Department: {DepartmentFilter}");
            if (!string.IsNullOrWhiteSpace(BranchFilter)) parts.Add($"Branch: {BranchFilter}");
            if (!string.IsNullOrWhiteSpace(EmployeeFilter)) parts.Add($"Employee: {EmployeeFilter}");
            if (!string.IsNullOrWhiteSpace(ReferenceCodeFilter)) parts.Add($"Reference Code: {ReferenceCodeFilter}");
            if (!string.IsNullOrWhiteSpace(ParentTagFilter)) parts.Add($"Parent Tag: {ParentTagFilter}");
            if (!string.IsNullOrWhiteSpace(ReqIdFilter)) parts.Add($"Req ID: {ReqIdFilter}");

            ActiveFiltersText = parts.Count == 0 ? "" : "Filters: " + string.Join(" • ", parts);
        }

        private void UpdateSummary(List<InvoiceRow> list)
        {
            TotalCount = list.Count;
            NoEndDateCount = list.Count(i => !i.EndDate.HasValue || !i.DaysLeft.HasValue);
            ExpiredCount = list.Count(i => i.DaysLeft.HasValue && i.DaysLeft.Value < 0);
            ExpiringCount = list.Count(i => i.DaysLeft.HasValue && i.DaysLeft.Value >= 0 && i.DaysLeft.Value <= 90);
            ActiveCount = list.Count(i => i.DaysLeft.HasValue && i.DaysLeft.Value > 90);

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(NoEndDateCount));
            OnPropertyChanged(nameof(ExpiredCount));
            OnPropertyChanged(nameof(ExpiringCount));
            OnPropertyChanged(nameof(ActiveCount));
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

            var prop = typeof(InvoiceRow).GetProperty(_sortColumnKey);
            if (prop == null) return;

            _filteredRows = _sortDirection == ListSortDirection.Descending
                ? _filteredRows.OrderByDescending(x => prop.GetValue(x, null)).ToList()
                : _filteredRows.OrderBy(x => prop.GetValue(x, null)).ToList();
        }

        /// <summary>Every field the quick search box matches against — an invoice matches if all
        /// typed terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private IEnumerable<string> InvoiceSearchFields(InvoiceRow i)
        {
            var fields = new List<string> { i.SetCode, i.DocumentNumber, i.ReferenceNumber, i.Company, i.Status, i.CreatedByName, i.Remarks };
            fields.AddRange(i.SubTypeReferenceCodes);
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(i.SetId, out var names)) fields.AddRange(names);
            if (_serialNumbersBySetId != null && _serialNumbersBySetId.TryGetValue(i.SetId, out var serials)) fields.AddRange(serials);
            // Parent Tag (e.g. "Samsung Services") isn't a column on dbo.[Set] — it lives on the
            // Set's line items — so without this an invoice is only findable by its tag through
            // Advanced Filters, never the quick search box, even though the tag is effectively part
            // of the invoice's identity.
            if (_parentTagLabelsBySetId != null && _parentTagLabelsBySetId.TryGetValue(i.SetId, out var tags)) fields.AddRange(tags);
            // dbo.vw_InvoiceItems (per line item) — VendorName is descriptive context, same role as
            // Company; ModelNumber/PartNumber/AssetSerialNumber are identifier-shaped and also feed
            // InvoiceFuzzyCandidateFields below.
            if (_invoiceItemSearchFieldsBySetId != null && _invoiceItemSearchFieldsBySetId.TryGetValue(i.SetId, out var itemFields))
            {
                fields.AddRange(itemFields.VendorNames);
                fields.AddRange(itemFields.ModelNumbers);
                fields.AddRange(itemFields.PartNumbers);
                fields.AddRange(itemFields.AssetSerialNumbers);
            }
            return fields;
        }

        /// <summary>Identifier-shaped fields only (SetCode/Document #/Reference #/Sub-Type ref codes/
        /// serials/vw_InvoiceItems Model/Part/Asset-Serial numbers) — the subset it's meaningful to run
        /// an edit-distance comparison against. Free-text fields like Company/Status/Remarks/VendorName
        /// are excluded: comparing a short mistyped code against a long sentence would never land
        /// within the strict distance-2 threshold anyway.</summary>
        private IEnumerable<string> InvoiceFuzzyCandidateFields(InvoiceRow i)
        {
            var fields = new List<string> { i.SetCode, i.DocumentNumber, i.ReferenceNumber };
            fields.AddRange(i.SubTypeReferenceCodes);
            if (_serialNumbersBySetId != null && _serialNumbersBySetId.TryGetValue(i.SetId, out var serials)) fields.AddRange(serials);
            if (_invoiceItemSearchFieldsBySetId != null && _invoiceItemSearchFieldsBySetId.TryGetValue(i.SetId, out var itemFuzzFields))
            {
                fields.AddRange(itemFuzzFields.ModelNumbers);
                fields.AddRange(itemFuzzFields.PartNumbers);
                fields.AddRange(itemFuzzFields.AssetSerialNumbers);
            }
            return fields;
        }

        /// <summary>Brand/name-shaped fields — used when the mistyped term itself is a descriptive word
        /// (e.g. a company or vendor name) rather than a code. Deliberately narrower than the full
        /// descriptive field set (no Status/Remarks/CreatedByName): those are too short or too generic
        /// to fuzz against without risking coincidental matches on unrelated invoices.</summary>
        private IEnumerable<string> InvoiceDescriptiveFuzzyCandidateFields(InvoiceRow i)
        {
            var fields = new List<string> { i.Company };
            if (_itemNamesBySetId != null && _itemNamesBySetId.TryGetValue(i.SetId, out var names)) fields.AddRange(names);
            if (_parentTagLabelsBySetId != null && _parentTagLabelsBySetId.TryGetValue(i.SetId, out var tags)) fields.AddRange(tags);
            if (_invoiceItemSearchFieldsBySetId != null && _invoiceItemSearchFieldsBySetId.TryGetValue(i.SetId, out var itemFields)) fields.AddRange(itemFields.VendorNames);
            return fields;
        }

        /// <summary>Category/Item Type/Sub-Type/date-range/document-date/column-date filters — every
        /// active filter EXCEPT the quick search text and the expiry-card filter. Shared between the
        /// normal pipeline and the fuzzy fallback so "closest match" results still respect every other
        /// filter the user has set, not just the search box.</summary>
        private IEnumerable<InvoiceRow> ApplyStructuralFilters(IEnumerable<InvoiceRow> source)
        {
            var filtered = source;

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(i =>
                    i.Categories.Any(c => selectedCategories.Contains(c, StringComparer.OrdinalIgnoreCase)));
            }

            var selectedItemTypes = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedItemTypes.Count > 0)
            {
                filtered = filtered.Where(i =>
                    i.ItemTypes.Any(t => selectedItemTypes.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            var selectedSubTypes = new List<string>();
            if (SubTypeFilterContract) selectedSubTypes.Add("Contract");
            if (SubTypeFilterSubscription) selectedSubTypes.Add("Subscription");
            if (SubTypeFilterLicense) selectedSubTypes.Add("License");
            if (SubTypeFilterServices) selectedSubTypes.Add("Services");

            if (SubTypeFilterNonSubType || SubTypeFilterAnySubtype || selectedSubTypes.Count > 0)
            {
                filtered = filtered.Where(i =>
                    (SubTypeFilterNonSubType && i.HasNonSubTypeItems) ||
                    (SubTypeFilterAnySubtype && i.SubTypes.Count > 0) ||
                    (selectedSubTypes.Count > 0 && i.SubTypes.Any(t => selectedSubTypes.Contains(t, StringComparer.OrdinalIgnoreCase))));
            }

            if (FromDate.HasValue)
            {
                var from = FromDate.Value.Date;
                filtered = filtered.Where(i => i.DocumentDate.HasValue && i.DocumentDate.Value.Date >= from);
            }
            if (ToDate.HasValue)
            {
                var to = ToDate.Value.Date;
                filtered = filtered.Where(i => i.DocumentDate.HasValue && i.DocumentDate.Value.Date <= to);
            }

            var constrainedDateRows = DocumentDateFilterRows.Where(row => row.HasAnyConstraint).ToList();
            if (constrainedDateRows.Count > 0)
                filtered = filtered.Where(i => constrainedDateRows.Any(row => row.Matches(i.DocumentDate)));

            foreach (var kvp in _dateFilters)
            {
                var from = kvp.Value.Item1;
                var to = kvp.Value.Item2;
                if (kvp.Key == "StartDate")
                {
                    if (from.HasValue) filtered = filtered.Where(i => i.StartDate.HasValue && i.StartDate.Value.Date >= from.Value.Date);
                    if (to.HasValue) filtered = filtered.Where(i => i.StartDate.HasValue && i.StartDate.Value.Date <= to.Value.Date);
                }
                else if (kvp.Key == "EndDate")
                {
                    if (from.HasValue) filtered = filtered.Where(i => i.EndDate.HasValue && i.EndDate.Value.Date >= from.Value.Date);
                    if (to.HasValue) filtered = filtered.Where(i => i.EndDate.HasValue && i.EndDate.Value.Date <= to.Value.Date);
                }
            }

            return filtered;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match above
        /// found nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (SetCode/Document #/Reference #) and a descriptive
        /// typo (Company/Vendor/item name/Parent Tag, e.g. "Xiaoomi" vs "Xiaomi"); its shape decides
        /// which fields it's scored against. Every OTHER term still has to match exactly somewhere,
        /// which is what keeps this targeted instead of surfacing unrelated invoices. Candidates come
        /// from <paramref name="pool"/> — the rows already passing every OTHER active filter — so a
        /// "closest match" never ignores filters the user deliberately set. Discards anything past
        /// edit distance 2.
        /// </summary>
        private List<InvoiceRow> FuzzyMatchInvoices(List<InvoiceRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(i => InvoiceSearchFields(i).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            // Every term matched something individually — the zero-result came from their AND
            // combination, not a typo.
            if (fuzzTerm == null) return new List<InvoiceRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<InvoiceRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<InvoiceRow, IEnumerable<string>>)InvoiceFuzzyCandidateFields
                : InvoiceDescriptiveFuzzyCandidateFields;

            return pool
                .Where(i => SearchTextHelper.MatchesAllTerms(otherTerms, InvoiceSearchFields(i)))
                .Select(i => new
                {
                    Row = i,
                    Distance = fuzzFields(i)
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
        public void ApplyFilters(bool resetPage)
        {
            if (_allRows == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(SearchText);

            IEnumerable<InvoiceRow> filtered = _allRows;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(i => SearchTextHelper.MatchesAllTerms(searchTerms, InvoiceSearchFields(i)));

            filtered = ApplyStructuralFilters(filtered);

            // Snapshot before the expiry-card filter so the summary cards always reflect counts
            // across every OTHER active filter, and clicking one card doesn't zero out the rest.
            var forSummary = filtered.ToList();

            switch (_expiryFilter)
            {
                case ExpiryFilterMode.Expired:
                    filtered = filtered.Where(i => i.DaysLeft.HasValue && i.DaysLeft.Value < 0);
                    break;
                case ExpiryFilterMode.Expiring90:
                    filtered = filtered.Where(i => i.DaysLeft.HasValue && i.DaysLeft.Value >= 0 && i.DaysLeft.Value <= 90);
                    break;
                case ExpiryFilterMode.Active:
                    filtered = filtered.Where(i => i.DaysLeft.HasValue && i.DaysLeft.Value > 90);
                    break;
                case ExpiryFilterMode.NoEndDate:
                    filtered = filtered.Where(i => !i.EndDate.HasValue || !i.DaysLeft.HasValue);
                    break;
            }

            _filteredRows = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path above is completely unaffected. Relaxes just the search text; every other
            // active filter (category, dates, etc.) still applies via the same structural pool.
            bool isFuzzy = false;
            if (_filteredRows.Count == 0 && searchTerms.Length > 0)
            {
                var structuralPool = ApplyStructuralFilters(_allRows).ToList();
                var fuzzyMatches = FuzzyMatchInvoices(structuralPool, searchTerms);
                if (fuzzyMatches.Count > 0)
                {
                    isFuzzy = true;
                    _filteredRows = fuzzyMatches;
                    forSummary = fuzzyMatches;
                }
            }
            IsFuzzyResults = isFuzzy;
            FuzzyNoticeText = isFuzzy
                ? $"No exact matches for \"{SearchText}\" — showing the closest results instead."
                : "";

            if (SelectedFilterBy == "Most Recently Added")
                _filteredRows = _filteredRows.OrderByDescending(x => x.CreatedAt).ToList();
            else if (SelectedFilterBy == "Oldest Added")
                _filteredRows = _filteredRows.OrderBy(x => x.CreatedAt).ToList();

            if (resetPage) CurrentPage = 1;

            BindPage();
            UpdateSummary(forSummary);
            UpdateTotalsBar(_filteredRows);
            UpdateActiveFiltersLabel();
        }

        public void ApplyFilters() => ApplyFilters(resetPage: true);

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

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _suppressCategoryFilterChange = true;
            foreach (var option in CategoryFilterOptions) option.IsChecked = false;
            _suppressCategoryFilterChange = false;
            UpdateCategoryFilterSummary();

            _suppressItemTypeFilterChange = true;
            foreach (var option in ItemTypeFilterOptions) option.IsChecked = false;
            _suppressItemTypeFilterChange = false;
            UpdateItemTypeFilterSummary();

            _subTypeFilterNonSubType = false;
            _subTypeFilterContract = false;
            _subTypeFilterSubscription = false;
            _subTypeFilterLicense = false;
            _subTypeFilterServices = false;
            OnPropertyChanged(nameof(SubTypeFilterNonSubType));
            OnPropertyChanged(nameof(SubTypeFilterContract));
            OnPropertyChanged(nameof(SubTypeFilterSubscription));
            OnPropertyChanged(nameof(SubTypeFilterLicense));
            OnPropertyChanged(nameof(SubTypeFilterServices));
            UpdateSubTypeFilterSummary();

            _fromDate = null;
            OnPropertyChanged(nameof(FromDate));
            _toDate = null;
            OnPropertyChanged(nameof(ToDate));
            _selectedQuickRange = "All";
            OnPropertyChanged(nameof(SelectedQuickRange));

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

            while (DocumentDateFilterRows.Count > 1)
            {
                var last = DocumentDateFilterRows[DocumentDateFilterRows.Count - 1];
                last.Changed -= OnDocumentDateRowChanged;
                DocumentDateFilterRows.RemoveAt(DocumentDateFilterRows.Count - 1);
            }
            DocumentDateFilterRows[0].Reset();

            _dateFilters.Clear();
            OnPropertyChanged(nameof(HasStartDateFilter));
            OnPropertyChanged(nameof(HasEndDateFilter));

            _expiryFilter = ExpiryFilterMode.All;
            OnPropertyChanged(nameof(IsAllCardActive));
            OnPropertyChanged(nameof(IsExpiredCardActive));
            OnPropertyChanged(nameof(IsExpiringCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsNoEndDateCardActive));

            // The Set-linked filters above are resolved server-side, so a plain client-side
            // ApplyFilters() isn't enough to un-apply them — reload from the DB with all filters
            // now cleared (LoadInvoicesAsync re-runs ApplyFilters(resetPage: true) once loaded).
            LoadInvoices();
        }
    }
}
