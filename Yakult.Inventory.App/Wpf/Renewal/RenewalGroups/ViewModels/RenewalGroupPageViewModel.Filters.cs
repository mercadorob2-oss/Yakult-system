using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels
{
    public sealed partial class RenewalGroupPageViewModel
    {
        private readonly Dictionary<string, HashSet<string>> _columnFilters = new Dictionary<string, HashSet<string>>();
        private string _sortColumnKey;
        private bool _sortAscending = true;

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded group — cheap
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
        // (see FuzzyMatchRenewalGroups) rather than an exact search-text match, so the view can show
        // a banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        // ── Set-linked filters (SetCode, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) ──────────────────────────────────────────────
        // Unlike Search/Status/Type these aren't columns on the already-loaded grouped rows —
        // they require a server round trip through GetAllRenewalsAllChains, so typing queues a
        // debounced reload instead of calling ApplyFilters() over the in-memory list.
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

        // ── Sub-Type filter (Non-Subtype, or Subtype -> Contract/Subscription/License/Services) ──
        // Same fixed-checkbox shape as Invoice Reports' Sub-Type filter — see
        // InvoicePageViewModel.Filters.cs for the rationale. "Subtype" (any) and the four
        // specific children are mutually exclusive: checking one clears the other side.
        // Each RenewalGroupRow aggregates SubTypes/HasNonSubTypeItems across every set in its
        // chain (see RenewalGroupPageViewModel.Actions.cs), since a group can span several SetIds.
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
            return (_groups ?? new List<RenewalGroupRow>())
                .Where(g => g.DocumentDate.HasValue)
                .Select(g => g.DocumentDate.Value.Year);
        }

        private void LoadDocumentYearOptions()
        {
            var years = GetDocumentYears().ToList();
            foreach (var row in DocumentDateFilterRows)
                row.SetYearOptions(years);
        }

        // ── Summary cards (computed from all non-archived groups, independent of filters) ──
        public int TotalCount { get; private set; }
        public int ExpiredCount { get; private set; }
        public int ExpiringCount { get; private set; }
        public int WarningCount { get; private set; }
        public int ActiveCount { get; private set; }

        private void UpdateSummary()
        {
            var active = (_groups ?? new List<RenewalGroupRow>()).Where(g => g.Active).ToList();

            TotalCount = active.Count;
            ExpiredCount = active.Count(g => g.OverallStatus == "Expired");
            ExpiringCount = active.Count(g => g.OverallStatus == "Expiring Soon");
            WarningCount = active.Count(g => g.OverallStatus == "Warning");
            ActiveCount = active.Count(g => g.OverallStatus == "Active");

            OnPropertyChanged(nameof(TotalCount));
            OnPropertyChanged(nameof(ExpiredCount));
            OnPropertyChanged(nameof(ExpiringCount));
            OnPropertyChanged(nameof(WarningCount));
            OnPropertyChanged(nameof(ActiveCount));
        }

        // ── Column filter popup (data supplied to the View; ColumnFilterPopup stays WinForms) ──
        private static string DaysLeftDisplay(RenewalGroupRow g)
        {
            if (!g.DaysUntilExpiry.HasValue) return "";
            return g.DaysUntilExpiry.Value < 0
                ? $"Expired {Math.Abs(g.DaysUntilExpiry.Value)}d ago"
                : $"{g.DaysUntilExpiry.Value} days left";
        }

        private static Func<RenewalGroupRow, string> ColumnGetter(string columnKey)
        {
            switch (columnKey)
            {
                case "SetCode": return g => g.RootSetCode ?? "";
                case "Company": return g => g.CompanyName ?? "";
                case "Type": return g => g.SetType ?? "";
                case "Status": return g => g.OverallStatus ?? "";
                case "Sets": return g => g.Chain.Count.ToString();
                case "DaysLeft": return g => DaysLeftDisplay(g);
                case "CreatedAt": return g => g.CreatedDate?.ToString("MM/dd/yyyy") ?? "";
                default: return g => "";
            }
        }

        public bool HasColumnFilter(string columnKey) => _columnFilters.ContainsKey(columnKey);

        public List<string> GetDistinctColumnValues(string columnKey)
        {
            var getter = ColumnGetter(columnKey);
            return (_groups ?? new List<RenewalGroupRow>())
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public HashSet<string> GetCurrentColumnFilter(string columnKey)
        {
            _columnFilters.TryGetValue(columnKey, out var v);
            return v;
        }

        public void ApplyColumnSort(string columnKey, bool ascending)
        {
            _sortColumnKey = columnKey;
            _sortAscending = ascending;
            ApplyFilters();
        }

        public void ApplyColumnFilter(string columnKey, HashSet<string> selectedValues, int distinctValueCount)
        {
            if (selectedValues == null || selectedValues.Count == 0 || selectedValues.Count >= distinctValueCount)
                _columnFilters.Remove(columnKey);
            else
                _columnFilters[columnKey] = selectedValues;

            ApplyFilters();
        }

        public void ClearColumnFilter(string columnKey)
        {
            _columnFilters.Remove(columnKey);
            ApplyFilters();
        }

        /// <summary>Every field the quick search box matches against — a group matches if all typed
        /// terms each show up somewhere across any chain entry or the group's aggregate fields (AND
        /// across terms, OR across fields/chain entries).</summary>
        private static IEnumerable<string> RenewalGroupSearchFields(RenewalGroupRow g)
        {
            var fields = new List<string>();
            foreach (var r in g.Chain)
            {
                fields.Add(r.SetCode);
                fields.Add(r.DocumentNumber);
                fields.Add(r.CompanyName);
                fields.Add(r.SetType);
                fields.Add(r.Remarks);
                fields.Add(r.RenewalNotes);
            }
            fields.AddRange(g.SubTypeReferenceCodes);
            if (g.ItemNamesBySetId != null)
                fields.AddRange(g.ItemNamesBySetId.Values.SelectMany(names => names));
            return fields;
        }

        /// <summary>Identifier-shaped fields only (SetCode/Document #/Sub-Type ref codes) — the
        /// subset it's meaningful to run an edit-distance comparison against for a code-shaped typo.</summary>
        private static IEnumerable<string> RenewalGroupCodeFuzzyCandidateFields(RenewalGroupRow g)
        {
            var fields = new List<string>();
            foreach (var r in g.Chain)
            {
                fields.Add(r.SetCode);
                fields.Add(r.DocumentNumber);
            }
            fields.AddRange(g.SubTypeReferenceCodes);
            return fields;
        }

        /// <summary>Company + item names — used when the mistyped term itself is a descriptive word
        /// (e.g. a brand: "Xiaoomi" vs "Xiaomi") rather than a code.</summary>
        private static IEnumerable<string> RenewalGroupDescriptiveFuzzyCandidateFields(RenewalGroupRow g)
        {
            var fields = g.Chain.Select(r => r.CompanyName).ToList();
            if (g.ItemNamesBySetId != null)
                fields.AddRange(g.ItemNamesBySetId.Values.SelectMany(names => names));
            return fields;
        }

        /// <summary>Type/Show-Archived/Sub-Type/document-date/column filters — every active filter
        /// EXCEPT the quick search text and the status-card toggle. Shared between the normal
        /// pipeline and the fuzzy fallback so "closest match" results still respect every other
        /// filter the user has set.</summary>
        private IEnumerable<RenewalGroupRow> ApplyStructuralFilters(IEnumerable<RenewalGroupRow> source)
        {
            var f = source;

            if (!string.IsNullOrEmpty(SelectedType) && SelectedType != "All")
                f = f.Where(g => g.SetType == SelectedType);

            if (!ShowArchived)
                f = f.Where(g => g.Active);

            var selectedSubTypes = new List<string>();
            if (SubTypeFilterContract) selectedSubTypes.Add("Contract");
            if (SubTypeFilterSubscription) selectedSubTypes.Add("Subscription");
            if (SubTypeFilterLicense) selectedSubTypes.Add("License");
            if (SubTypeFilterServices) selectedSubTypes.Add("Services");

            if (SubTypeFilterNonSubType || SubTypeFilterAnySubtype || selectedSubTypes.Count > 0)
            {
                f = f.Where(g =>
                    (SubTypeFilterNonSubType && g.HasNonSubTypeItems) ||
                    (SubTypeFilterAnySubtype && g.SubTypes.Count > 0) ||
                    (selectedSubTypes.Count > 0 && g.SubTypes.Any(t => selectedSubTypes.Contains(t, StringComparer.OrdinalIgnoreCase))));
            }

            var constrainedDateRows = DocumentDateFilterRows.Where(row => row.HasAnyConstraint).ToList();
            if (constrainedDateRows.Count > 0)
                f = f.Where(g => constrainedDateRows.Any(row => row.Matches(g.DocumentDate)));

            foreach (var kv in _columnFilters)
            {
                var allowed = kv.Value;
                if (allowed == null || allowed.Count == 0) continue;
                var getter = ColumnGetter(kv.Key);
                f = f.Where(g => allowed.Contains(getter(g), StringComparer.OrdinalIgnoreCase));
            }

            return f;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (SetCode/Document #) and a descriptive typo
        /// (Company/item name). Every OTHER term still has to match exactly somewhere. Discards
        /// anything past edit distance 2.
        /// </summary>
        private List<RenewalGroupRow> FuzzyMatchRenewalGroups(List<RenewalGroupRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(g => RenewalGroupSearchFields(g).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            if (fuzzTerm == null) return new List<RenewalGroupRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<RenewalGroupRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<RenewalGroupRow, IEnumerable<string>>)RenewalGroupCodeFuzzyCandidateFields
                : RenewalGroupDescriptiveFuzzyCandidateFields;

            return pool
                .Where(g => SearchTextHelper.MatchesAllTerms(otherTerms, RenewalGroupSearchFields(g)))
                .Select(g => new
                {
                    Row = g,
                    Distance = fuzzFields(g)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f => SearchTextHelper.MinWordDistance(fuzzTerm, f))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min()
                })
                .Where(x => x.Distance <= 2)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Row.RootSetCode)
                .Take(maxResults)
                .Select(x => x.Row)
                .ToList();
        }

        // ── Core filter + sort pipeline ──────────────────────────────────────
        public void ApplyFilters()
        {
            if (_groups == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(SearchText);
            var structuralPool = ApplyStructuralFilters(_groups).ToList();

            var f = structuralPool.AsEnumerable();
            if (searchTerms.Length > 0)
                f = f.Where(g => SearchTextHelper.MatchesAllTerms(searchTerms, RenewalGroupSearchFields(g)));

            var afterSearch = f.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path is completely unaffected. Relaxes just the search text; every other
            // active filter still applies via the same structural pool.
            bool isFuzzy = false;
            if (afterSearch.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchRenewalGroups(structuralPool, searchTerms);
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
                afterSearch = afterSearch.Where(g => g.OverallStatus == SelectedStatus || g.Chain.First().ExpiryStatus == SelectedStatus).ToList();

            _filteredGroups = afterSearch;

            if (_sortColumnKey != null)
            {
                switch (_sortColumnKey)
                {
                    case "SetCode":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.RootSetCode ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                            : _filteredGroups.OrderByDescending(g => g.RootSetCode ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case "Company":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.CompanyName ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                            : _filteredGroups.OrderByDescending(g => g.CompanyName ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case "Type":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.SetType ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                            : _filteredGroups.OrderByDescending(g => g.SetType ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case "Status":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.OverallStatus ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                            : _filteredGroups.OrderByDescending(g => g.OverallStatus ?? "", StringComparer.OrdinalIgnoreCase).ToList();
                        break;
                    case "Sets":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.Chain.Count).ToList()
                            : _filteredGroups.OrderByDescending(g => g.Chain.Count).ToList();
                        break;
                    case "DaysLeft":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.DaysUntilExpiry ?? int.MaxValue).ToList()
                            : _filteredGroups.OrderByDescending(g => g.DaysUntilExpiry ?? int.MaxValue).ToList();
                        break;
                    case "CreatedAt":
                        _filteredGroups = _sortAscending
                            ? _filteredGroups.OrderBy(g => g.CreatedDate ?? DateTime.MaxValue).ToList()
                            : _filteredGroups.OrderByDescending(g => g.CreatedDate ?? DateTime.MaxValue).ToList();
                        break;
                }
            }

            CurrentPage = 1;
            RenderPage();
            RecomputeSelectedCount();
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

            while (DocumentDateFilterRows.Count > 1)
            {
                var last = DocumentDateFilterRows[DocumentDateFilterRows.Count - 1];
                last.Changed -= ApplyFilters;
                DocumentDateFilterRows.RemoveAt(DocumentDateFilterRows.Count - 1);
            }
            DocumentDateFilterRows[0].Reset();

            _columnFilters.Clear();
            _sortColumnKey = null;

            bool hadSetFilters = !string.IsNullOrWhiteSpace(_setCodeFilter) || !string.IsNullOrWhiteSpace(_documentNumberFilter)
                || !string.IsNullOrWhiteSpace(_companyFilter) || !string.IsNullOrWhiteSpace(_departmentFilter)
                || !string.IsNullOrWhiteSpace(_branchFilter) || !string.IsNullOrWhiteSpace(_employeeFilter)
                || !string.IsNullOrWhiteSpace(_referenceCodeFilter) || !string.IsNullOrWhiteSpace(_parentTagFilter)
                || !string.IsNullOrWhiteSpace(_reqIdFilter);

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

            ApplyFilters();

            // Set-linked filters are resolved server-side, so clearing them needs an actual reload.
            if (hadSetFilters) _ = LoadGroupsAsync();
        }
    }
}
