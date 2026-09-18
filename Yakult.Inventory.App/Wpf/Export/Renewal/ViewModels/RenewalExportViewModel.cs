using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Export.Renewal.ViewModels
{
    /// <summary>
    /// Business logic for the Export Renewal page, ported verbatim from
    /// Pages\Export\ExportRenewal.cs. Binds directly to RenewalDto (already has a Selected
    /// property). No Add/Edit/Archive/Delete and no Filter-By/Sort-By (hidden in the original).
    /// Select-All spans EVERY filtered row across all pages (matches the original's
    /// header-click handler, which iterated `_filteredRows` not just `row.Visible` rows) —
    /// unlike ExportInvoice/ExportRenewalsGrouped, whose Select-All is current-page-only.
    /// </summary>
    public sealed class RenewalExportViewModel : ViewModelBase
    {
        private readonly RenewalRepository _repo = new RenewalRepository();

        private List<RenewalDto> _allRows = new List<RenewalDto>();
        private List<RenewalDto> _filteredRows = new List<RenewalDto>();

        private string _sortProperty;
        private ListSortDirection _sortDirection = ListSortDirection.Ascending;

        public RenewalExportViewModel()
        {
            PageSizeOptions = new ObservableCollection<string> { "10", "25", "50", "100" };
            _selectedPageSize = "25";
            PagedRows = new ObservableCollection<RenewalDto>();

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
        public event Action<string, string> RequestError;
        public event Action<List<RenewalDto>, RenewalRepository> RequestExport;

        public ObservableCollection<RenewalDto> PagedRows { get; }
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

        private string _countText = "0 renewal(s)";
        public string CountText { get => _countText; private set => SetField(ref _countText, value); }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; set => SetField(ref _currentPage, value); }

        private string _pageInfoText = "Page 1 of 1 (0 renewal(s))";
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
                _allRows = await Task.Run(() => _repo.GetAllRenewals());
                RebuildFilter();
                CanExport = true;
            }
            catch (Exception ex)
            {
                CountText = $"Error: {ex.Message}";
            }
        }

        private void RebuildFilter()
        {
            string q = SearchText?.Trim().ToLowerInvariant() ?? "";
            _filteredRows = string.IsNullOrEmpty(q)
                ? _allRows.ToList()
                : _allRows.Where(r =>
                    (r.SetCode?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.DocumentNumber?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.CompanyName?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.SiteDisplay?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.SetLevelStatus?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.PartNumber?.ToLowerInvariant().Contains(q) ?? false) ||
                    (r.SetType?.ToLowerInvariant().Contains(q) ?? false)).ToList();

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
            var prop = typeof(RenewalDto).GetProperty(_sortProperty);
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
            PageInfoText = $"Page {CurrentPage} of {pages} ({total} renewal(s))";
            CountText = $"{total} renewal(s)   |   {selAll} selected";

            CanFirstPage = CanPrevPage = CurrentPage > 1;
            CanNextPage = CanLastPage = CurrentPage < pages;
        }

        public void NotifySelectionChanged() => ApplyPage();

        /// <summary>Select-All spans every FILTERED row (all pages) — matches the original.</summary>
        public void SetAllFilteredSelection(bool state)
        {
            foreach (var r in _filteredRows) r.Selected = state;
            ApplyPage();
        }

        private void Export()
        {
            var selected = _allRows.Where(r => r.Selected).ToList();
            if (selected.Count == 0)
            {
                RequestInfo?.Invoke("No Selection", "Please select at least one renewal to export.");
                return;
            }
            RequestExport?.Invoke(selected, _repo);
        }
    }
}
