using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.RepairedItems.ViewModels
{
    public sealed partial class RepairedItemsPageViewModel
    {
        private IEnumerable<RepairedItemRow> ApplyBaseFilters(IEnumerable<RepairedItemRow> rows)
        {
            if (rows == null) return Enumerable.Empty<RepairedItemRow>();

            IEnumerable<RepairedItemRow> filtered = rows.Where(x => x != null);

            var activeMode = (SelectedActive ?? "Active only").Trim();
            if (activeMode.Equals("Active only", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(x => x.Active);
            else if (activeMode.Equals("Inactive only", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(x => !x.Active);

            var category = (SelectedCategory ?? "All").Trim();
            if (!string.IsNullOrWhiteSpace(category) && !category.Equals("All", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(x => string.Equals((x.Category ?? string.Empty).Trim(), category, StringComparison.OrdinalIgnoreCase));

            if (SelectedCondition != null && SelectedCondition.Id > 0)
                filtered = filtered.Where(x => x.ConditionId == SelectedCondition.Id);

            var location = (SelectedLocation ?? "All").Trim();
            if (!string.IsNullOrWhiteSpace(location) && !location.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                if (location.Equals("Out", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => x.IsOut);
                else if (location.Equals("In Set", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => x.IsInSet);
                else if (location.Equals("Deployed", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => x.IsDeployed);
                else if (location.Equals("In Stock", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => !x.IsOut);
            }

            var origin = (SelectedOrigin ?? "All").Trim();
            if (!string.IsNullOrWhiteSpace(origin) && !origin.Equals("All", StringComparison.OrdinalIgnoreCase))
                filtered = filtered.Where(x => string.Equals((x.LastRepairOrigin ?? string.Empty).Trim(), origin, StringComparison.OrdinalIgnoreCase));

            return filtered;
        }

        // ── Summary counts ────────────────────────────────────────────────────

        private int _totalCount, _repairedCount, _unrepairedCount, _spareCount, _damagedCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int RepairedCount { get => _repairedCount; private set => SetField(ref _repairedCount, value); }
        public int UnrepairedCount { get => _unrepairedCount; private set => SetField(ref _unrepairedCount, value); }
        public int SpareCount { get => _spareCount; private set => SetField(ref _spareCount, value); }
        public int DamagedCount { get => _damagedCount; private set => SetField(ref _damagedCount, value); }

        private string _pageTitle = "View Repair Items";
        public string PageTitle { get => _pageTitle; private set => SetField(ref _pageTitle, value); }

        private void UpdateSummaryCounts()
        {
            var universe = ApplyBaseFilters(_allRows ?? new List<RepairedItemRow>()).ToList();

            TotalCount = universe.Count;
            RepairedCount = universe.Count(x => x != null && x.IsRepaired);
            // "Unrepaired" (card/filter label now reads "Needs Repair") counts items currently
            // Condition = Damaged — NOT TotalCount - RepairedCount. An item that was simply never
            // damaged belongs in neither bucket; counting it as Unrepaired is the exact bug this
            // page was reported for (Condition = Good items showing up under Unrepaired).
            UnrepairedCount = universe.Count(x => x != null && x.NeedsRepairByCondition);
            SpareCount = universe.Count(x => x != null && x.IsSpare);
            DamagedCount = universe.Count(x => x != null && string.Equals((x.ConditionName ?? string.Empty).Trim(), "Damaged", StringComparison.OrdinalIgnoreCase));

            PageTitle = $"View Repair Items ({RepairedCount} repaired)";
        }

        private bool _isTotalCardActive, _isRepairedCardActive, _isUnrepairedCardActive, _isSpareCardActive, _isDamagedCardActive;
        public bool IsTotalCardActive { get => _isTotalCardActive; private set => SetField(ref _isTotalCardActive, value); }
        public bool IsRepairedCardActive { get => _isRepairedCardActive; private set => SetField(ref _isRepairedCardActive, value); }
        public bool IsUnrepairedCardActive { get => _isUnrepairedCardActive; private set => SetField(ref _isUnrepairedCardActive, value); }
        public bool IsSpareCardActive { get => _isSpareCardActive; private set => SetField(ref _isSpareCardActive, value); }
        public bool IsDamagedCardActive { get => _isDamagedCardActive; private set => SetField(ref _isDamagedCardActive, value); }

        private void UpdateSummaryCardActiveStates()
        {
            IsTotalCardActive = IsRepairedCardActive = IsUnrepairedCardActive = IsSpareCardActive = IsDamagedCardActive = false;

            var conditionIsDamaged = SelectedCondition != null && string.Equals((SelectedCondition.Name ?? string.Empty).Trim(), "Damaged", StringComparison.OrdinalIgnoreCase);

            var spareOnly = (SelectedSpare ?? string.Empty).Trim().Equals("Spare only", StringComparison.OrdinalIgnoreCase);
            if (spareOnly)
            {
                IsSpareCardActive = true;
                if (conditionIsDamaged) IsDamagedCardActive = true;
                return;
            }

            var repair = (SelectedRepairStatus ?? "All").Trim();
            if (repair.Equals("Repaired", StringComparison.OrdinalIgnoreCase)) IsRepairedCardActive = true;
            else if (repair.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase)) IsUnrepairedCardActive = true;
            else IsTotalCardActive = true;

            if (conditionIsDamaged) IsDamagedCardActive = true;
        }

        private bool TrySelectConditionByName(string name)
        {
            if (ConditionOptions == null || ConditionOptions.Count == 0) return false;

            if (SelectedCondition != null && string.Equals((SelectedCondition.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase))
                return false;

            var match = ConditionOptions.FirstOrDefault(c => string.Equals((c.Name ?? string.Empty).Trim(), name, StringComparison.OrdinalIgnoreCase));
            if (match == null) return false;

            SelectedCondition = match;
            return true;
        }

        private void LoadConditions()
        {
            var keep = SelectedCondition;

            ConditionOptions.Clear();
            ConditionOptions.Add(new ConditionChoice { Id = 0, Name = "All" });

            var conditions = (_allRows ?? new List<RepairedItemRow>())
                .Where(x => x != null && x.ConditionId > 0 && !string.IsNullOrWhiteSpace(x.ConditionName))
                .Select(x => new { x.ConditionId, Name = x.ConditionName.Trim() })
                .Distinct()
                .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var c in conditions)
                ConditionOptions.Add(new ConditionChoice { Id = c.ConditionId, Name = c.Name });

            var match = keep != null
                ? ConditionOptions.FirstOrDefault(c => c.Id == keep.Id)
                : null;
            _selectedCondition = match ?? ConditionOptions[0];
            OnPropertyChanged(nameof(SelectedCondition));
        }

        private void LoadCategories()
        {
            var keep = (SelectedCategory ?? "All").Trim();

            CategoryOptions.Clear();
            CategoryOptions.Add("All");

            var categories = (_allRows ?? new List<RepairedItemRow>())
                .Where(x => x != null)
                .Select(x => (x.Category ?? string.Empty).Trim())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var c in categories) CategoryOptions.Add(c);

            var match = CategoryOptions.FirstOrDefault(c => string.Equals(c.Trim(), keep, StringComparison.OrdinalIgnoreCase));
            _selectedCategory = match ?? "All";
            OnPropertyChanged(nameof(SelectedCategory));
        }

        // ── Search / filter / sort pipeline ───────────────────────────────────

        private static bool ContainsIgnoreCase(string haystack, string needle)
        {
            if (string.IsNullOrWhiteSpace(haystack) || string.IsNullOrWhiteSpace(needle)) return false;
            return haystack.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>Every field the quick search box matches against — a row matches if all typed
        /// terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private static IEnumerable<string> RepairedItemSearchFields(RepairedItemRow r) => new[]
        {
            r.Name, r.Category, r.ModelNumber, r.SerialNumber, r.ConditionName, r.LastRepairAction,
            r.LastRepairOrigin, r.LastRepairSourceRaw, r.LastRepairProcessedByName,
            r.LastRepairRemarkOneLine, r.CurrentSetCode, r.LocationLabel
        };

        /// <summary>Identifier-shaped fields only (Model/Serial Number/Set Code) — the subset it's
        /// meaningful to run an edit-distance comparison against for a code-shaped typo.</summary>
        private static IEnumerable<string> RepairedItemCodeFuzzyCandidateFields(RepairedItemRow r) => new[]
        {
            r.ModelNumber, r.SerialNumber, r.CurrentSetCode
        };

        /// <summary>Item name only — used when the mistyped term itself is a descriptive word (e.g. a
        /// brand: "Xiaoomi" vs "Xiaomi") rather than a code.</summary>
        private static IEnumerable<string> RepairedItemDescriptiveFuzzyCandidateFields(RepairedItemRow r) => new[]
        {
            r.Name
        };

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. A term "needs" fuzzing if it didn't find a single exact match anywhere in the
        /// pool — covers both a code-shaped typo (Model/Serial Number/Set Code) and a descriptive/
        /// item-name typo. Every OTHER term still has to match exactly somewhere. Candidates come
        /// from <paramref name="pool"/> — the rows already passing every OTHER active filter.
        /// Discards anything past edit distance 2.
        /// </summary>
        private List<RepairedItemRow> FuzzyMatchRepairedItems(List<RepairedItemRow> pool, string[] searchTerms, int maxResults = 5)
        {
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(r => RepairedItemSearchFields(r).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            if (fuzzTerm == null) return new List<RepairedItemRow>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<RepairedItemRow, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<RepairedItemRow, IEnumerable<string>>)RepairedItemCodeFuzzyCandidateFields
                : RepairedItemDescriptiveFuzzyCandidateFields;

            return pool
                .Where(r => SearchTextHelper.MatchesAllTerms(otherTerms, RepairedItemSearchFields(r)))
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
                .ThenBy(x => x.Row.Name)
                .Take(maxResults)
                .Select(x => x.Row)
                .ToList();
        }

        private void ApplyFilters(bool resetToFirstPage)
        {
            if (resetToFirstPage) CurrentPage = 1;

            IEnumerable<RepairedItemRow> rows = ApplyBaseFilters(_allRows ?? new List<RepairedItemRow>());

            var repairFilter = (SelectedRepairStatus ?? "All").Trim();
            if (repairFilter.Equals("Repaired", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(r => r != null && r.IsRepaired);
            // "Unrepaired" (dropdown value kept as-is; card/label reads "Needs Repair") is driven
            // by NeedsRepairByCondition, matching UpdateSummaryCounts above — not !IsRepaired, which
            // would again sweep up every item that simply never needed a repair in the first place.
            else if (repairFilter.Equals("Unrepaired", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(r => r != null && r.NeedsRepairByCondition);

            var spareFilter = (SelectedSpare ?? "All").Trim();
            if (spareFilter.Equals("Spare only", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(r => r.IsSpare);
            else if (spareFilter.Equals("Not spare", StringComparison.OrdinalIgnoreCase))
                rows = rows.Where(r => !r.IsSpare);

            if (ProblemsOnly)
            {
                // Only real "problem" state left once Repaired/Unrepaired are Condition-driven —
                // an item with no repair history is not a problem, it just never needed fixing.
                rows = rows.Where(r => r != null && r.NeedsRepairByCondition);
            }

            var structuralPool = rows.ToList();
            var searchTerms = SearchTextHelper.SplitTerms(SearchText);

            IEnumerable<RepairedItemRow> filtered = structuralPool;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(r => SearchTextHelper.MatchesAllTerms(searchTerms, RepairedItemSearchFields(r)));

            var afterSearch = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path is completely unaffected. Every other active filter still applies via the
            // same structural pool.
            bool isFuzzy = false;
            if (afterSearch.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchRepairedItems(structuralPool, searchTerms);
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

            _filteredRows = afterSearch;
            TryApplyRestoredSortState();
            ApplySortIfAny();
            BindGridPage();
            UpdateSummaryCardActiveStates();
        }

        private void ApplySortIfAny()
        {
            if (_sortColumnKey == null || _sortDirection == null || _filteredRows == null) return;

            Func<RepairedItemRow, object> selector;
            switch (_sortColumnKey)
            {
                case "ItemId": selector = r => r.ItemId; break;
                case "Name": selector = r => r.Name; break;
                case "Category": selector = r => r.Category; break;
                case "ModelNumber": selector = r => r.ModelNumber; break;
                case "SerialNumber": selector = r => r.SerialNumber; break;
                case "ConditionName": selector = r => r.ConditionName; break;
                case "RepairStatus": selector = r => r.RepairStatus; break;
                case "NeedsRepairLabel": selector = r => r.NeedsRepairLabel; break;
                case "RepairCount": selector = r => r.RepairCount; break;
                case "LastRepairAction": selector = r => r.LastRepairAction; break;
                case "LastRepairAt": selector = r => r.LastRepairAt; break;
                case "SpareLabel": selector = r => r.SpareLabel; break;
                case "StockOnHand": selector = r => r.StockOnHand; break;
                default: selector = r => r.ItemId; break;
            }

            _filteredRows = (_sortDirection == ListSortDirection.Ascending
                    ? _filteredRows.OrderBy(selector, Comparer<object>.Default)
                    : _filteredRows.OrderByDescending(selector, Comparer<object>.Default))
                .ToList();
        }

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<RepairedItemRow> PagedRows { get; }

        /// <summary>Header "select all" checkbox — toggles Selected for the CURRENT PAGE's rows only
        /// (mirrors RequestPageViewModel.SelectAllState).</summary>
        private bool? _selectAllState = false;
        public bool? SelectAllState
        {
            get => _selectAllState;
            set
            {
                _selectAllState = value;
                OnPropertyChanged();
                if (value != true && value != false) return;

                foreach (var row in PagedRows) row.Selected = value.Value;
            }
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

        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private static int GetTotalPages(int totalRows, int pageSize)
        {
            if (pageSize <= 0) pageSize = 1;
            var pages = (int)Math.Ceiling(totalRows / (double)pageSize);
            return pages < 1 ? 1 : pages;
        }

        private int GetTotalPages() => GetTotalPages(_filteredRows?.Count ?? 0, PageSize);

        private void BindGridPage()
        {
            var total = _filteredRows?.Count ?? 0;
            var totalPages = GetTotalPages(total, PageSize);
            if (CurrentPage < 1) CurrentPage = 1;
            if (CurrentPage > totalPages) CurrentPage = totalPages;

            var pageRows = (_filteredRows ?? new List<RepairedItemRow>())
                .Skip((CurrentPage - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            PagedRows.Clear();
            foreach (var r in pageRows) PagedRows.Add(r);

            PageInfoText = total == 0
                ? "No repair items match the current search and filters."
                : $"Page {CurrentPage} / {totalPages} • {total} item(s)";

            CanFirstPage = CanPrevPage = CurrentPage > 1;
            CanNextPage = CanLastPage = CurrentPage < totalPages;

            RebuildSortByOptionsRequested?.Invoke();
        }

        public event Action RebuildSortByOptionsRequested;

        // ── Sort By dropdown ──────────────────────────────────────────────────

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
                ApplySortIfAny();
                BindGridPage();
                ScheduleSaveState();
            }
        }

        public void SetSortByOptionsOnce(List<ListSortOption> options)
        {
            if (SortByOptions.Count > 0) return; // matches _sortByDropdownInitialized guard

            _suppressSortByChange = true;
            try
            {
                foreach (var o in options) SortByOptions.Add(o);

                var selected = _sortColumnKey != null
                    ? options.FirstOrDefault(o => o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending))
                    : null;
                selected = selected ?? options.FirstOrDefault(o => o.ColumnKey == "ItemId" && o.Direction == ListSortDirection.Ascending) ?? options.FirstOrDefault();

                SelectedSortBy = selected;
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's grid Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortDirection = (_sortColumnKey == columnKey && _sortDirection == ListSortDirection.Ascending)
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            _sortColumnKey = columnKey;

            ApplySortIfAny();
            BindGridPage();

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

            ScheduleSaveState();
        }

        public ListSortDirection? CurrentSortDirection => _sortDirection;
        public string CurrentSortColumnKey => _sortColumnKey;
    }
}
