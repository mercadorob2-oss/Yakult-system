using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages.Export;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Export.RenewalsGrouped.ViewModels
{
    /// <summary>
    /// Business logic for the Export Renewals (Grouped) page, ported verbatim from
    /// Pages\Export\ExportRenewalsGrouped.cs, including LoadGroupedRenewals()/StatusOrder()
    /// (mirrors ViewRenewalGroupPage.BuildGroups()/StatusOrder() exactly — same grouping-by-root,
    /// same sort order, same "exclude archived chains" default filter). No Add/Edit/Archive/
    /// Delete and no Filter-By/Sort-By (hidden in the original). Select-All is current-page-only
    /// (matches the original's header-click handler, which iterated `row.Visible` rows) — unlike
    /// ExportRenewal/ExportSets, whose Select-All spans every filtered row across all pages.
    /// </summary>
    public sealed class RenewalsGroupedExportViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repo = new RenewalRepository();

        private List<RenewalGroupViewModel> _allRows = new List<RenewalGroupViewModel>();
        private List<RenewalGroupViewModel> _filteredRows = new List<RenewalGroupViewModel>();

        private string _sortProperty;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public RenewalsGroupedExportViewModel()
        {
            PageSizeOptions = new ObservableCollection<string> { "10", "25", "50", "100" };
            _selectedPageSize = "25";
            PagedRows = new ObservableCollection<RenewalGroupViewModel>();

            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ExportCommand = new RelayCommand(Export);
            BackCommand = new RelayCommand(() => RequestBack?.Invoke());
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; ApplyPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); ApplyPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage++; ApplyPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages(); ApplyPage(); });
        }

        public event Action RequestBack;
        public event Action<string, string> RequestInfo;
        public event Action<List<RenewalGroupViewModel>, RenewalRepository> RequestExport;

        public ObservableCollection<RenewalGroupViewModel> PagedRows { get; }
        public ObservableCollection<string> PageSizeOptions { get; }

        private string _selectedPageSize;
        public string SelectedPageSize
        {
            get => _selectedPageSize;
            set
            {
                if (SetField(ref _selectedPageSize, value) && int.TryParse(value, out var size) && size > 0)
                {
                    _pageSize = size;
                    CurrentPage = 1;
                    ApplyPage();
                }
            }
        }
        private int _pageSize = 25;

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) RebuildFilter(); }
        }

        private bool _canExport;
        public bool CanExport { get => _canExport; private set => SetField(ref _canExport, value); }

        private string _countText = "0 group(s)";
        public string CountText { get => _countText; private set => SetField(ref _countText, value); }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; set => SetField(ref _currentPage, value); }

        private string _pageInfoText = "Page 1 of 1 (0 group(s))";
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private bool _canFirstPage, _canPrevPage, _canNextPage, _canLastPage;
        public bool CanFirstPage { get => _canFirstPage; private set => SetField(ref _canFirstPage, value); }
        public bool CanPrevPage { get => _canPrevPage; private set => SetField(ref _canPrevPage, value); }
        public bool CanNextPage { get => _canNextPage; private set => SetField(ref _canNextPage, value); }
        public bool CanLastPage { get => _canLastPage; private set => SetField(ref _canLastPage, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ExportCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        public async Task LoadAsync()
        {
            CanExport = false;
            CountText = "Loading…";
            try
            {
                _allRows = await Task.Run(() => LoadGroupedRenewals());
                RebuildFilter();
                CanExport = true;
            }
            catch (Exception ex)
            {
                CountText = $"Error: {ex.Message}";
            }
        }

        // Mirrors ViewRenewalGroupPage.BuildGroups() exactly
        private List<RenewalGroupViewModel> LoadGroupedRenewals()
        {
            var all = _repo.GetAllRenewalsAllChains();

            var parentOf = all.ToDictionary(r => r.SetId, r => r.RenewalOfSetId);

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

            var groups = all
                .GroupBy(r => FindRoot(r.SetId))
                .Select(g =>
                {
                    var chain = g.OrderByDescending(r => r.SetId).ToList();
                    var latest = chain.First();
                    var root = chain.Last();
                    return new RenewalGroupViewModel
                    {
                        RootSetId = g.Key,
                        RootSetCode = root.SetCode,
                        CompanyName = latest.CompanyName,
                        SetType = latest.SetType,
                        OverallStatus = latest.SetLevelStatus ?? latest.ExpiryStatus,
                        DaysUntilExpiry = latest.DaysUntilExpiry,
                        RootEndDate = root.EndDate,
                        Active = chain.Any(r => r.Active),
                        Chain = chain
                    };
                })
                // Same sort order as ViewRenewalGroupPage:
                // Expired first, then Expiring Soon, Warning, Active, then others
                .OrderBy(g => StatusOrder(g.OverallStatus))
                .ThenBy(g => g.DaysUntilExpiry ?? int.MaxValue)
                // Same default filter: exclude archived (Active = false) — mirrors chkShowArchived=false
                .Where(g => g.Active)
                .ToList();

            return groups;
        }

        // Mirrors ViewRenewalGroupPage.StatusOrder() exactly
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

        private void RebuildFilter()
        {
            string q = SearchText?.Trim().ToLowerInvariant() ?? "";
            _filteredRows = string.IsNullOrEmpty(q)
                ? _allRows.ToList()
                : _allRows.Where(r =>
                    (r.RootSetCode?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.CompanyName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.SetType?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.OverallStatus?.ToLowerInvariant().Contains(q) ?? false)).ToList();

            if (_sortProperty != null) ApplySort();

            CurrentPage = 1;
            ApplyPage();
        }

        public ListSortDirection SortByColumn(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return _sortDirection;
            _sortDirection = _sortProperty == propertyName && _sortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;
            _sortProperty = propertyName;
            ApplySort();
            ApplyPage();
            return _sortDirection;
        }

        private void ApplySort()
        {
            var prop = typeof(RenewalGroupViewModel).GetProperty(_sortProperty);
            if (prop == null) return;
            _filteredRows = _sortDirection == ListSortDirection.Ascending
                ? _filteredRows.OrderBy(x => prop.GetValue(x, null)).ToList()
                : _filteredRows.OrderByDescending(x => prop.GetValue(x, null)).ToList();
        }

        private int TotalPages()
        {
            int total = _filteredRows.Count;
            return total == 0 ? 1 : (int)Math.Ceiling(total / (double)_pageSize);
        }

        private void ApplyPage()
        {
            int total = _filteredRows.Count;
            int pages = TotalPages();
            CurrentPage = Math.Max(1, Math.Min(CurrentPage, pages));

            var paged = _filteredRows.Skip((CurrentPage - 1) * _pageSize).Take(_pageSize).ToList();
            PagedRows.Clear();
            foreach (var r in paged) PagedRows.Add(r);

            int selAll = _allRows.Count(r => r.Selected);
            PageInfoText = $"Page {CurrentPage} of {pages} ({total} group(s))";
            CountText = $"{total} group(s)   |   {selAll} selected";

            CanFirstPage = CanPrevPage = CurrentPage > 1;
            CanNextPage = CanLastPage = CurrentPage < pages;
        }

        /// <summary>Select-All is current-page-only — matches the original.</summary>
        public void NotifySelectionChanged() => ApplyPage();

        public void SetPageSelection(bool state)
        {
            foreach (var r in PagedRows) r.Selected = state;
            ApplyPage();
        }

        private void Export()
        {
            var selected = _allRows.Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one renewal group to export.");
                return;
            }
            RequestExport?.Invoke(selected, _repo);
        }
    }
}
