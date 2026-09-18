using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels
{
    public sealed partial class RenewalGroupPageViewModel
    {
        // ── Loading ──────────────────────────────────────────────────────────
        public async void LoadGroups() => await LoadGroupsAsync();

        public async Task LoadGroupsAsync()
        {
            try
            {
                _allRenewals = await Task.Run(() => _repository.GetAllRenewalsAllChains(
                    setCodeFilter: SetCodeFilter,
                    documentNumberFilter: DocumentNumberFilter,
                    companyFilter: CompanyFilter,
                    departmentFilter: DepartmentFilter,
                    branchFilter: BranchFilter,
                    employeeFilter: EmployeeFilter,
                    referenceCodeFilter: ReferenceCodeFilter,
                    parentTagFilter: ParentTagFilter,
                    reqIdFilter: ReqIdFilter));
                BuildGroups();

                var allSetIds = _groups.SelectMany(g => g.Chain.Select(r => r.SetId)).Distinct().ToList();
                _itemNamesBySetId = await Task.Run(() => _repository.GetItemNamesBySetIds(allSetIds));
                foreach (var g in _groups) g.ItemNamesBySetId = _itemNamesBySetId;

                var subTypesBySetId = await Task.Run(() => _setRepository.GetSubTypesBySetIds(allSetIds));
                foreach (var g in _groups)
                {
                    var subTypes = new List<string>();
                    var referenceCodes = new List<string>();
                    bool hasNonSubType = false;
                    foreach (var setId in g.Chain.Select(r => r.SetId))
                    {
                        if (!subTypesBySetId.TryGetValue(setId, out var entry))
                        {
                            // No dbo.SetItem rows for this set at all — counts as Non-Subtype.
                            hasNonSubType = true;
                            continue;
                        }

                        if (entry.HasNonSubType) hasNonSubType = true;
                        foreach (var st in entry.SubTypes)
                            if (!subTypes.Contains(st, StringComparer.OrdinalIgnoreCase))
                                subTypes.Add(st);
                        foreach (var rc in entry.ReferenceCodes)
                            if (!referenceCodes.Contains(rc, StringComparer.OrdinalIgnoreCase))
                                referenceCodes.Add(rc);
                    }
                    g.SubTypes = subTypes;
                    g.HasNonSubTypeItems = hasNonSubType;
                    g.SubTypeReferenceCodes = referenceCodes;
                }

                LoadDocumentYearOptions();
                ApplyFilters();
                UpdateSummary();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load renewals: {ex.Message}");
            }
        }

        private void BuildGroups()
        {
            if (_allRenewals == null) return;

            var parentOf = _allRenewals.ToDictionary(r => r.SetId, r => r.RenewalOfSetId);

            int FindRoot(int id)
            {
                int cur = id;
                for (int guard = 0; guard < 50; guard++)
                {
                    if (!parentOf.TryGetValue(cur, out var p) || !p.HasValue) return cur;
                    cur = p.Value;
                }
                return cur;
            }

            _groups = _allRenewals
                .GroupBy(r => FindRoot(r.SetId))
                .Select(g =>
                {
                    var chain = g.OrderByDescending(r => r.SetId).ToList();
                    var latest = chain.First();
                    var root = chain.Last();
                    return new RenewalGroupRow
                    {
                        RootSetId = g.Key,
                        RootSetCode = root.SetCode,
                        CompanyName = latest.CompanyName,
                        SetType = latest.SetType,
                        OverallStatus = latest.SetLevelStatus ?? latest.ExpiryStatus,
                        DaysUntilExpiry = latest.DaysUntilExpiry,
                        Active = chain.Any(r => r.Active),
                        Chain = chain,
                        CreatedDate = root.CreatedDate,
                        DocumentDate = root.DocumentDate
                    };
                })
                .OrderBy(g => StatusOrder(g.OverallStatus))
                .ThenBy(g => g.DaysUntilExpiry ?? int.MaxValue)
                .ToList();

            // Subscribed at build time (not per-page) because selection is cross-page,
            // matching CommitGenerateReport's use of _filteredGroups ?? _groups.
            foreach (var group in _groups)
                group.PropertyChanged += Group_PropertyChanged;
        }

        private void Group_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RenewalGroupRow.Selected))
                RecomputeSelectedCount();
        }

        // Called directly by the View's group-checkbox Checked/Unchecked handlers, in addition to
        // the Group_PropertyChanged subscription above — belt-and-suspenders against WPF
        // container recycling occasionally not propagating the bound PropertyChanged reliably,
        // which was undercounting SelectedCount in practice.
        public void RefreshSelectionState() => RecomputeSelectedCount();

        private void RecomputeSelectedCount()
        {
            SelectedCount = (_filteredGroups ?? _groups).Count(g => g.Selected);
            OnPropertyChanged(nameof(HasSelection));
        }

        private static int StatusOrder(string s)
        {
            switch (s)
            {
                case "Expired": return 1;
                case "Expiring Soon": return 2;
                case "Warning": return 3;
                case "Active": return 4;
                default: return 5;
            }
        }

        // ── Pagination ───────────────────────────────────────────────────────
        public int PageSize { get; }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set => SetField(ref _currentPage, value);
        }

        private string _pageInfoText = "Page 0 of 0 (0 groups)";
        public string PageInfoText
        {
            get => _pageInfoText;
            private set => SetField(ref _pageInfoText, value);
        }

        private int GetTotalPages()
        {
            var total = _filteredGroups?.Count ?? 0;
            return total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
        }

        private void RenderPage()
        {
            var src = _filteredGroups ?? new List<RenewalGroupRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 groups)";
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems) Rows.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} groups)";
        }
    }
}
