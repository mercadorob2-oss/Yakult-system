using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Inventory.ViewModels
{
    /// <summary>
    /// Business logic for the 5-tab Inventory Dashboard, ported verbatim from
    /// Pages\Inventory\ViewInventoryPage.cs (raw inline ADO.NET — no repository layer in the
    /// original, preserved as-is; the dead legacy BuildUi() path was confirmed unreachable and
    /// is not ported). Tabs: 0=Category Stock Summary, 1=Hardware, 2=Software/License,
    /// 3=Services, 4=Inventory (the only tab with edit/archive actions).
    /// </summary>
    public sealed partial class InventoryPageViewModel : ViewModelBase
    {
        private readonly string _connectionString = Yakult.Inventory.App.Core.DatabaseConfig.ConnectionString;
        private bool _suppressSortByChange;

        public InventoryPageViewModel()
        {
            SortByOptions = new ObservableCollection<ListSortOption>();

            FirstPageCommand = new RelayCommand(() => { SetActivePage(1); });
            PrevPageCommand = new RelayCommand(() => { if (ActiveCurrentPage > 1) SetActivePage(ActiveCurrentPage - 1); });
            NextPageCommand = new RelayCommand(() => { if (ActiveCurrentPage < ActiveTotalPages) SetActivePage(ActiveCurrentPage + 1); });
            LastPageCommand = new RelayCommand(() => { SetActivePage(ActiveTotalPages); });
            RefreshCommand = new RelayCommand(OnRefresh);

            // Tab 5 (Inventory) loads eagerly on construction, same as the original constructor's
            // unconditional LoadInventory() call. Tab 0 (Category Stock) also loads eagerly, since
            // BuildUiWithTemplate() calls LoadCategoryStockData() before the first tab is shown.
            LoadCategoryStockData();
            LoadInventory();
        }

        // ── Active tab ────────────────────────────────────────────────────────

        private int _activeTabIndex;
        public int ActiveTabIndex
        {
            get => _activeTabIndex;
            set
            {
                if (SetField(ref _activeTabIndex, value))
                {
                    OnPropertyChanged(nameof(IsInventoryTabActive));
                    OnPropertyChanged(nameof(ShowEditArchiveButtons));

                    // Mirrors: if (_layout.SearchBox != null) _layout.SearchBox.Text = "";
                    // (clearing the shared search box on every tab switch).
                    _searchText = string.Empty;
                    OnPropertyChanged(nameof(SearchText));

                    OnTabActivated();
                }
            }
        }

        public bool IsInventoryTabActive => ActiveTabIndex == 4;

        /// <summary>Edit/Archive buttons are visible only on the Inventory tab, and only when
        /// the user is not read-only — matches TabControl_SelectedIndexChanged's
        /// "isInventoryTab &amp;&amp; !AppSession.IsReadOnly" gate (the only permission check
        /// across all 6 converted pages).</summary>
        public bool ShowEditArchiveButtons => IsInventoryTabActive && !AppSession.IsReadOnly;

        private void OnTabActivated()
        {
            switch (ActiveTabIndex)
            {
                case 0:
                    if (!_categoryStock.Loaded) LoadCategoryStockData();
                    else _categoryStock.UpdatePage();
                    break;
                case 1:
                    if (!_hardware.Loaded) LoadHardwareData();
                    else _hardware.UpdatePage();
                    break;
                case 2:
                    if (!_softwareLicense.Loaded) LoadSoftwareLicenseData();
                    else _softwareLicense.UpdatePage();
                    break;
                case 3:
                    if (!_services.Loaded) LoadServicesData();
                    else _services.UpdatePage();
                    break;
                case 4:
                    UpdatePagination();
                    break;
            }

            RaiseActivePagingChanged();
            RebuildSortByOptionsRequested?.Invoke();
        }

        private void OnRefresh()
        {
            switch (ActiveTabIndex)
            {
                case 0: LoadCategoryStockData(); break;
                case 1: LoadHardwareData(); break;
                case 2: LoadSoftwareLicenseData(); break;
                case 3: LoadServicesData(); break;
                case 4: LoadInventory(); break;
            }
        }

        // ── Shared search box (delegates to whichever tab is active) ───────────

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetField(ref _searchText, value))
                    OnSearchChanged();
            }
        }

        private void OnSearchChanged()
        {
            switch (ActiveTabIndex)
            {
                case 0: ApplyFiltersCategoryStock(); break;
                case 1: ApplyFiltersHardware(); break;
                case 2: ApplyFiltersSoftwareLicense(); break;
                case 3: ApplyFiltersServices(); break;
                case 4: ApplyFilters(); break;
            }
            RaiseActivePagingChanged();
        }

        // ── Shared pagination bar (mirrors the single pagination panel outside the
        //    TabControl in the original, which every tab's Btn*Page_Click routes through) ──

        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }
        public RelayCommand RefreshCommand { get; }

        private int ActiveCurrentPage
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return _categoryStock.CurrentPage;
                    case 1: return _hardware.CurrentPage;
                    case 2: return _softwareLicense.CurrentPage;
                    case 3: return _services.CurrentPage;
                    default: return _currentPage;
                }
            }
        }

        private int ActiveTotalPages
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return _categoryStock.TotalPages;
                    case 1: return _hardware.TotalPages;
                    case 2: return _softwareLicense.TotalPages;
                    case 3: return _services.TotalPages;
                    default: return _filteredInventory == null || _filteredInventory.Count == 0 ? 0
                        : (int)System.Math.Ceiling((double)_filteredInventory.Count / PageSize);
                }
            }
        }

        private void SetActivePage(int page)
        {
            switch (ActiveTabIndex)
            {
                case 0: _categoryStock.CurrentPage = page; _categoryStock.UpdatePage(); break;
                case 1: _hardware.CurrentPage = page; _hardware.UpdatePage(); break;
                case 2: _softwareLicense.CurrentPage = page; _softwareLicense.UpdatePage(); break;
                case 3: _services.CurrentPage = page; _services.UpdatePage(); break;
                case 4: _currentPage = page; UpdatePagination(); break;
            }
            RaiseActivePagingChanged();
        }

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

        private void RaiseActivePagingChanged()
        {
            int current = ActiveCurrentPage;
            int total = ActiveTotalPages;
            int count = ActiveItemCount;

            string noun = ActiveTabIndex == 4 ? "entries" : "items";
            PageInfoText = count == 0 ? $"Page 0 of 0 (0 {noun})" : $"Page {current} of {total} ({count} {noun})";

            CanFirstPage = current > 1;
            CanPrevPage = current > 1;
            CanNextPage = current < total;
            CanLastPage = current < total;
        }

        private int ActiveItemCount
        {
            get
            {
                switch (ActiveTabIndex)
                {
                    case 0: return _categoryStock.Filtered.Count;
                    case 1: return _hardware.Filtered.Count;
                    case 2: return _softwareLicense.Filtered.Count;
                    case 3: return _services.Filtered.Count;
                    default: return _filteredInventory?.Count ?? 0;
                }
            }
        }

        // ── Sort By dropdown (shared control; rebuilt by the View per active tab's grid) ──

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

                switch (ActiveTabIndex)
                {
                    case 0:
                        _categoryStock.SortColumnKey = value.ColumnKey;
                        _categoryStock.SortDirection = value.Direction;
                        ApplyFiltersCategoryStock();
                        break;
                    case 1:
                        _hardware.SortColumnKey = value.ColumnKey;
                        _hardware.SortDirection = value.Direction;
                        ApplyFiltersHardware();
                        break;
                    case 2:
                        _softwareLicense.SortColumnKey = value.ColumnKey;
                        _softwareLicense.SortDirection = value.Direction;
                        ApplyFiltersSoftwareLicense();
                        break;
                    case 3:
                        _services.SortColumnKey = value.ColumnKey;
                        _services.SortDirection = value.Direction;
                        ApplyFiltersServices();
                        break;
                    case 4:
                        _sortColumnKey = value.ColumnKey;
                        _sortDirection = value.Direction;
                        // Mirrors: reset tab-local Filter By to Default when sorting via dropdown.
                        if (_selectedFilterByInventory != "Default")
                        {
                            _selectedFilterByInventory = "Default";
                            OnPropertyChanged(nameof(SelectedFilterByInventory));
                        }
                        ApplyFilters();
                        break;
                }
                RaiseActivePagingChanged();
            }
        }

        /// <summary>Raised when the View should rebuild SortByOptions from the active tab's grid columns.</summary>
        public event System.Action RebuildSortByOptionsRequested;

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                string currentKey = null;
                ListSortDirection? currentDir = null;
                switch (ActiveTabIndex)
                {
                    case 0: currentKey = _categoryStock.SortColumnKey; currentDir = _categoryStock.SortDirection; break;
                    case 1: currentKey = _hardware.SortColumnKey; currentDir = _hardware.SortDirection; break;
                    case 2: currentKey = _softwareLicense.SortColumnKey; currentDir = _softwareLicense.SortDirection; break;
                    case 3: currentKey = _services.SortColumnKey; currentDir = _services.SortDirection; break;
                    case 4: currentKey = _sortColumnKey; currentDir = _sortDirection; break;
                }

                ListSortOption selected = null;
                if (currentKey != null)
                    selected = options.FirstOrDefault(o => o.ColumnKey == currentKey && o.Direction == (currentDir ?? ListSortDirection.Ascending));
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's active grid Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            switch (ActiveTabIndex)
            {
                case 0: _categoryStock.SortColumnKey = columnKey; _categoryStock.SortDirection = direction; ApplyFiltersCategoryStock(); break;
                case 1: _hardware.SortColumnKey = columnKey; _hardware.SortDirection = direction; ApplyFiltersHardware(); break;
                case 2: _softwareLicense.SortColumnKey = columnKey; _softwareLicense.SortDirection = direction; ApplyFiltersSoftwareLicense(); break;
                case 3: _services.SortColumnKey = columnKey; _services.SortDirection = direction; ApplyFiltersServices(); break;
                case 4: _sortColumnKey = columnKey; _sortDirection = direction; ApplyFilters(); break;
            }

            _suppressSortByChange = true;
            try
            {
                var match = SortByOptions.FirstOrDefault(o => o.ColumnKey == columnKey && o.Direction == direction);
                if (match != null) SelectedSortBy = match;
            }
            finally
            {
                _suppressSortByChange = false;
            }

            RaiseActivePagingChanged();
        }

        /// <summary>Pre-fills the search box, switches to the Inventory tab, and applies the
        /// filter. Called from the home page global search.</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;

            if (ActiveTabIndex != 4)
                ActiveTabIndex = 4;
            else if (_allInventory == null)
                LoadInventory();

            SearchText = query;
        }
    }
}
