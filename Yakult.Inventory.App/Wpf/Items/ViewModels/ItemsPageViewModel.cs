using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Items.ViewModels
{
    public partial class ItemsPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;
        private const int CategoryPageSize = 5;

        private List<ItemDto> _allItems = new List<ItemDto>();
        private List<ItemDto> _filteredItems = new List<ItemDto>();
        private readonly ItemSelectionManager _selectionManager = new ItemSelectionManager();

        private List<CategorySummaryDto> _allCategorySummaries = new List<CategorySummaryDto>();

        // Excel-style per-column value filters, keyed by ItemDto property name.
        private readonly Dictionary<string, HashSet<string>> _columnFilters =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public static readonly HashSet<string> FilterableColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Name", "Category", "ItemType", "ModelNumber", "SerialNumber", "VendorName"
        };

        private string _sortProperty;   // null => no explicit column sort; falls back to SelectedSortBy
        private bool _sortAscending;

        private int _currentPage = 1;
        private int _categoryCurrentPage = 1;

        public ObservableCollection<ItemDto> PagedItems { get; } = new ObservableCollection<ItemDto>();
        public ObservableCollection<CategorySummaryDto> PagedCategorySummaries { get; } = new ObservableCollection<CategorySummaryDto>();
        public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();
        public List<string> SortByOptions { get; } = new List<string> { "Default", "Most Recently Added", "Oldest Added", "Date Purchased (Newest)", "Date Purchased (Oldest)" };

        /// <summary>Checkbox item backing the multi-select Category filter (mirrors the Sub-Type filter's shape).</summary>
        public sealed class CategoryFilterOption : ViewModelBase
        {
            private readonly Func<bool> _isSuppressed;
            private readonly Action _onChanged;

            public string Name { get; }

            private bool _isSelected;
            public bool IsSelected
            {
                get => _isSelected;
                set
                {
                    if (!SetField(ref _isSelected, value)) return;
                    if (_isSuppressed()) return;
                    _onChanged();
                }
            }

            public CategoryFilterOption(string name, Func<bool> isSuppressed, Action onChanged)
            {
                Name = name;
                _isSuppressed = isSuppressed;
                _onChanged = onChanged;
            }
        }

        // ── Loading ───────────────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        // ── Filters ───────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) { _searchDebounceTimer.Stop(); _searchDebounceTimer.Start(); } }
        }

        // ── Category filter (multi-select checkboxes, same shape as Sub-Type below) ──────────
        private bool _suppressCategoryFilterChange;

        private void OnCategoryFilterChanged()
        {
            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private string _categoryFilterSummary = "All Categories";
        public string CategoryFilterSummary
        {
            get => _categoryFilterSummary;
            private set => SetField(ref _categoryFilterSummary, value);
        }

        private void UpdateCategoryFilterSummary()
        {
            var names = CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            CategoryFilterSummary = names.Count == 0
                ? "All Categories"
                : names.Count == 1
                    ? names[0]
                    : $"{names.Count} categories selected";
        }

        private void ClearCategoryFilter()
        {
            _suppressCategoryFilterChange = true;
            try
            {
                foreach (var opt in CategoryFilterOptions)
                    opt.IsSelected = false;
            }
            finally
            {
                _suppressCategoryFilterChange = false;
            }

            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private string _serialFilter = string.Empty;
        public string SerialFilter
        {
            get => _serialFilter;
            set { if (SetField(ref _serialFilter, value)) ApplyFilters(); }
        }

        // ── Set-linked filters (SetCode, Document #, Company, Department, Branch, Employee,
        // Reference Code, Parent Tag, Req ID) ──────────────────────────────────────────────
        // Unlike Serial/Category/Sub-Type these aren't columns on ItemDto — an Item can belong to
        // many dbo.SetItem rows across many Sets — so they can't be re-filtered client-side over
        // _allItems. Each is resolved server-side via an EXISTS join in LoadItemsAsync, so typing
        // triggers a debounced reload (300ms DispatcherTimer) instead of ApplyFilters().
        private readonly DispatcherTimer _setFilterDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };

        private void QueueSetFilterReload()
        {
            _setFilterDebounceTimer.Stop();
            _setFilterDebounceTimer.Start();
        }

        // Quick search runs the full tokenized+fuzzy filter pipeline over every loaded item — cheap
        // for one keystroke, but calling it synchronously on EVERY keystroke (no debounce) meant
        // holding a key down (or pasting/spamming characters) blocked the UI thread once per
        // character, which is what made typing/erasing visibly lag. Debouncing it the same way the
        // Set-linked filters above already are fixes that without changing the search itself.
        private readonly DispatcherTimer _searchDebounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

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

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (!SetField(ref _showInactive, value)) return;

                // Manually toggling this checkbox is equivalent to picking the Total/Active
                // summary card, so the two controls stay in sync rather than fighting.
                _itemsSummaryMode = value ? ItemsSummaryMode.All : ItemsSummaryMode.ActiveOnly;
                OnPropertyChanged(nameof(IsTotalCardActive));
                OnPropertyChanged(nameof(IsActiveCardActive));
                OnPropertyChanged(nameof(IsInactiveCardActive));

                _ = ReloadAsync();
            }
        }

        private string _selectedSortBy = "Default";
        public string SelectedSortBy
        {
            get => _selectedSortBy;
            set
            {
                if (SetField(ref _selectedSortBy, value))
                {
                    _sortProperty = null;
                    ApplyFilters();
                }
            }
        }

        // ── Sub-Type filter (Non-Subtype, or Subtype -> Contract/Subscription/License/Services) ──
        // Same fixed-checkbox shape as Invoice Reports'/Sets'/Renewals' Sub-Type filter. Unlike
        // those pages (which aggregate Sub-Types across a Set's several dbo.SetItem rows), an
        // Item carries its own SubType directly (ItemDto.SubType, dbo.Item.SubType) — one value
        // per row, no per-SetId lookup needed. "Subtype" (any) and the four specific children
        // are mutually exclusive: checking one clears the other side.
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

        // ── Stat cards (clickable — filter to Total/Active/Inactive) ─────────────
        private int _totalItemsCount;
        public int TotalItemsCount { get => _totalItemsCount; private set => SetField(ref _totalItemsCount, value); }

        private int _activeItemsCount;
        public int ActiveItemsCount { get => _activeItemsCount; private set => SetField(ref _activeItemsCount, value); }

        private int _inactiveItemsCount;
        public int InactiveItemsCount { get => _inactiveItemsCount; private set => SetField(ref _inactiveItemsCount, value); }

        private enum ItemsSummaryMode { All, ActiveOnly, InactiveOnly }
        private ItemsSummaryMode _itemsSummaryMode = ItemsSummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _itemsSummaryMode == ItemsSummaryMode.All;
        public bool IsActiveCardActive => _itemsSummaryMode == ItemsSummaryMode.ActiveOnly;
        public bool IsInactiveCardActive => _itemsSummaryMode == ItemsSummaryMode.InactiveOnly;

        public ICommand TotalCardCommand { get; }
        public ICommand ActiveCardCommand { get; }
        public ICommand InactiveCardCommand { get; }

        private void SetItemsSummaryMode(ItemsSummaryMode mode)
        {
            _itemsSummaryMode = mode;
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsInactiveCardActive));

            // Total/Inactive need inactive rows fetched from the DB at all; Active-only
            // matches the page's original default (excludes inactive at the DB level).
            bool needsShowInactive = mode != ItemsSummaryMode.ActiveOnly;
            if (_showInactive != needsShowInactive)
            {
                _showInactive = needsShowInactive;
                OnPropertyChanged(nameof(ShowInactive));
                _ = ReloadAsync();
            }
            else
            {
                ApplyFilters();
            }
        }

        // ── Selection ─────────────────────────────────────────────────────────
        private int _selectedCount;
        public int SelectedCount { get => _selectedCount; private set => SetField(ref _selectedCount, value); }
        public bool HasSelection => SelectedCount > 0;

        private bool? _selectAllState = false;
        public bool? SelectAllState { get => _selectAllState; private set => SetField(ref _selectAllState, value); }

        // Category Stock Summary is read-only — item actions (everything except
        // Update Cellphone Details, matching the original's behavior) are disabled
        // while that tab is active, since "selected items" has no meaning there.
        private bool _isItemsTabActive = true;
        public bool IsItemsTabActive { get => _isItemsTabActive; set => SetField(ref _isItemsTabActive, value); }

        // Set when the rows currently on screen came from the "Did you mean...?" fuzzy fallback
        // (see FuzzyMatchItems) rather than an exact search-text match, so the view can show a
        // banner explaining why instead of presenting typo-corrected guesses as literal hits.
        private bool _isFuzzyResults;
        public bool IsFuzzyResults { get => _isFuzzyResults; private set => SetField(ref _isFuzzyResults, value); }

        private string _fuzzyNoticeText = "";
        public string FuzzyNoticeText { get => _fuzzyNoticeText; private set => SetField(ref _fuzzyNoticeText, value); }

        // ── Items pagination ──────────────────────────────────────────────────
        private string _pageInfo = "Page 0 of 0 (0 items)";
        public string PageInfo { get => _pageInfo; private set => SetField(ref _pageInfo, value); }

        private bool _canGoFirst, _canGoPrev, _canGoNext, _canGoLast;
        public bool CanGoFirst { get => _canGoFirst; private set => SetField(ref _canGoFirst, value); }
        public bool CanGoPrev { get => _canGoPrev; private set => SetField(ref _canGoPrev, value); }
        public bool CanGoNext { get => _canGoNext; private set => SetField(ref _canGoNext, value); }
        public bool CanGoLast { get => _canGoLast; private set => SetField(ref _canGoLast, value); }

        // ── Category summary pagination ──────────────────────────────────────
        private string _categoryPageInfo = "Page 0 of 0 (0 items)";
        public string CategoryPageInfo { get => _categoryPageInfo; private set => SetField(ref _categoryPageInfo, value); }

        private bool _categoryCanGoFirst, _categoryCanGoPrev, _categoryCanGoNext, _categoryCanGoLast;
        public bool CategoryCanGoFirst { get => _categoryCanGoFirst; private set => SetField(ref _categoryCanGoFirst, value); }
        public bool CategoryCanGoPrev { get => _categoryCanGoPrev; private set => SetField(ref _categoryCanGoPrev, value); }
        public bool CategoryCanGoNext { get => _categoryCanGoNext; private set => SetField(ref _categoryCanGoNext, value); }
        public bool CategoryCanGoLast { get => _categoryCanGoLast; private set => SetField(ref _categoryCanGoLast, value); }

        // ── Commands ──────────────────────────────────────────────────────────
        public ICommand RefreshCommand { get; }
        public ICommand ResetFiltersCommand { get; }
        public ICommand ClearSubTypeFilterCommand { get; }
        public ICommand ClearCategoryFilterCommand { get; }
        public ICommand AddItemsCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand ArchiveCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand GetFromMobileCommand { get; }
        public ICommand ImportCommand { get; }
        public ICommand DownloadTemplateCommand { get; }
        public ICommand ExportSelectedPdfCommand { get; }
        public ICommand BulkAddRequestCommand { get; }
        public ICommand AddToContractGroupCommand { get; }
        public ICommand AddToSubscriptionGroupCommand { get; }
        public ICommand AddToLicenseGroupCommand { get; }
        public ICommand AddToServiceGroupCommand { get; }
        public ICommand BulkEditItemsCommand { get; }
        public ICommand UpdateCellphoneDetailsCommand { get; }

        public ICommand FirstPageCommand { get; }
        public ICommand PrevPageCommand { get; }
        public ICommand NextPageCommand { get; }
        public ICommand LastPageCommand { get; }

        public ICommand CategoryFirstPageCommand { get; }
        public ICommand CategoryPrevPageCommand { get; }
        public ICommand CategoryNextPageCommand { get; }
        public ICommand CategoryLastPageCommand { get; }

        public ICommand ToggleSelectAllCommand { get; }
        public ICommand SortByColumnCommand { get; }

        // ── Events (handled by code-behind: dialogs, file pickers, owner windows) ──
        public event Action RequestAddItems;
        public event Action<List<ItemDto>> RequestEditItem;
        public event Action<List<ItemDto>> RequestArchiveItems;
        public event Action<List<ItemDto>> RequestDeleteItems;
        public event Action RequestGetFromMobile;
        public event Action RequestImport;
        public event Action RequestDownloadTemplate;
        public event Action<List<ItemDto>> RequestExportPdf;
        public event Action<List<int>> RequestBulkAddRequest;
        public event Action<List<int>, string> RequestAddToGroup;
        public event Action<List<int>> RequestBulkEditItems;
        public event Action RequestUpdateCellphoneDetails;

        public ItemsPageViewModel()
        {
            RefreshCommand = new RelayCommand(() => _ = RefreshAndResetFiltersAsync());
            ResetFiltersCommand = new RelayCommand(() => _ = ResetFiltersAsync());
            ClearSubTypeFilterCommand = new RelayCommand(ClearSubTypeFilter);
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            TotalCardCommand = new RelayCommand(() => SetItemsSummaryMode(ItemsSummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetItemsSummaryMode(ItemsSummaryMode.ActiveOnly));
            InactiveCardCommand = new RelayCommand(() => SetItemsSummaryMode(ItemsSummaryMode.InactiveOnly));
            AddItemsCommand = new RelayCommand(() => RequestAddItems?.Invoke());
            EditCommand = new RelayCommand(() => RequestEditItem?.Invoke(GetSelectedItems()));
            ArchiveCommand = new RelayCommand(() => RequestArchiveItems?.Invoke(GetSelectedItems()));
            DeleteCommand = new RelayCommand(() => RequestDeleteItems?.Invoke(GetSelectedItems()));
            GetFromMobileCommand = new RelayCommand(() => RequestGetFromMobile?.Invoke());
            ImportCommand = new RelayCommand(() => RequestImport?.Invoke());
            DownloadTemplateCommand = new RelayCommand(() => RequestDownloadTemplate?.Invoke());
            ExportSelectedPdfCommand = new RelayCommand(() => RequestExportPdf?.Invoke(GetSelectedItems()), () => HasSelection);
            BulkAddRequestCommand = new RelayCommand(() => RequestBulkAddRequest?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList()), () => HasSelection);
            AddToContractGroupCommand = new RelayCommand(() => RequestAddToGroup?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList(), "Contract"), () => HasSelection);
            AddToSubscriptionGroupCommand = new RelayCommand(() => RequestAddToGroup?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList(), "Subscription"), () => HasSelection);
            AddToLicenseGroupCommand = new RelayCommand(() => RequestAddToGroup?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList(), "License"), () => HasSelection);
            AddToServiceGroupCommand = new RelayCommand(() => RequestAddToGroup?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList(), "Services"), () => HasSelection);
            BulkEditItemsCommand = new RelayCommand(
                () => RequestBulkEditItems?.Invoke(GetSelectedItems().Select(i => i.ItemId).ToList()),
                () => HasSelection);
            UpdateCellphoneDetailsCommand = new RelayCommand(() => RequestUpdateCellphoneDetails?.Invoke());

            FirstPageCommand = new RelayCommand(() => { _currentPage = 1; RebuildItemsPage(); }, () => CanGoFirst);
            PrevPageCommand = new RelayCommand(() => { _currentPage--; RebuildItemsPage(); }, () => CanGoPrev);
            NextPageCommand = new RelayCommand(() => { _currentPage++; RebuildItemsPage(); }, () => CanGoNext);
            LastPageCommand = new RelayCommand(() => { _currentPage = TotalPages(); RebuildItemsPage(); }, () => CanGoLast);

            CategoryFirstPageCommand = new RelayCommand(() => { _categoryCurrentPage = 1; RebuildCategoryPage(); }, () => CategoryCanGoFirst);
            CategoryPrevPageCommand = new RelayCommand(() => { _categoryCurrentPage--; RebuildCategoryPage(); }, () => CategoryCanGoPrev);
            CategoryNextPageCommand = new RelayCommand(() => { _categoryCurrentPage++; RebuildCategoryPage(); }, () => CategoryCanGoNext);
            CategoryLastPageCommand = new RelayCommand(() => { _categoryCurrentPage = CategoryTotalPages(); RebuildCategoryPage(); }, () => CategoryCanGoLast);

            ToggleSelectAllCommand = new RelayCommand(() => SetAllSelected(SelectAllState != true));
            SortByColumnCommand = new RelayCommand<string>(ToggleColumnSort);

            _setFilterDebounceTimer.Tick += async (s, e) => { _setFilterDebounceTimer.Stop(); await ReloadAsync(); };
            _searchDebounceTimer.Tick += (s, e) => { _searchDebounceTimer.Stop(); ApplyFilters(); };
        }

        public List<ItemDto> GetSelectedItems() =>
            (_allItems ?? new List<ItemDto>()).Where(i => i != null && i.Selected).ToList();

        public int TotalPages() => Math.Max(1, (int)Math.Ceiling((_filteredItems?.Count ?? 0) / (double)PageSize));
        public int CategoryTotalPages() => Math.Max(1, (int)Math.Ceiling((_allCategorySummaries?.Count ?? 0) / (double)CategoryPageSize));

        public bool IsColumnFiltered(string propertyName) => _columnFilters.ContainsKey(propertyName);

        public List<string> GetDistinctColumnValues(string propertyName)
        {
            Func<ItemDto, string> getter;
            switch (propertyName)
            {
                case "Name": getter = r => r.Name ?? ""; break;
                case "Category": getter = r => r.Category ?? ""; break;
                case "ItemType": getter = r => r.ItemType ?? ""; break;
                case "ModelNumber": getter = r => r.ModelNumber ?? ""; break;
                case "SerialNumber": getter = r => r.SerialNumber ?? ""; break;
                case "VendorName": getter = r => r.VendorName ?? ""; break;
                default: return new List<string>();
            }

            return (_allItems ?? new List<ItemDto>())
                .Select(getter)
                .Where(v => !string.IsNullOrEmpty(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public HashSet<string> GetColumnFilter(string propertyName)
        {
            _columnFilters.TryGetValue(propertyName, out var set);
            return set;
        }

        public void SetColumnFilter(string propertyName, HashSet<string> selectedValues, int distinctValueCount)
        {
            if (selectedValues == null || selectedValues.Count == 0 || selectedValues.Count >= distinctValueCount)
                _columnFilters.Remove(propertyName);
            else
                _columnFilters[propertyName] = selectedValues;

            ApplyFilters();
        }

        public void ClearColumnFilter(string propertyName)
        {
            _columnFilters.Remove(propertyName);
            ApplyFilters();
        }

        public void SetColumnSort(string propertyName, bool ascending)
        {
            _sortProperty = propertyName;
            _sortAscending = ascending;
            ApplyFilters();
        }

        private void ToggleColumnSort(string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName)) return;

            if (_sortProperty == propertyName)
                _sortAscending = !_sortAscending;
            else
            {
                _sortProperty = propertyName;
                _sortAscending = true;
            }

            ApplyFilters();
        }

        public string SortProperty => _sortProperty;
        public bool SortAscending => _sortAscending;

        public void SetItemSelected(int itemId, bool selected)
        {
            var item = (_allItems ?? new List<ItemDto>()).FirstOrDefault(i => i.ItemId == itemId);
            if (item == null) return;

            item.Selected = selected;
            _selectionManager.SetSelected(itemId, selected);
            SyncSelectionState();
        }

        public void SetAllSelected(bool selected)
        {
            var list = _filteredItems ?? _allItems ?? new List<ItemDto>();
            foreach (var item in list)
            {
                item.Selected = selected;
                _selectionManager.SetSelected(item.ItemId, selected);
            }

            SyncSelectionState();
            RefreshCurrentPageDisplay();
        }

        public void ClearSelection()
        {
            _selectionManager.Clear();
            foreach (var item in _allItems ?? new List<ItemDto>())
                item.Selected = false;

            SyncSelectionState();
            RefreshCurrentPageDisplay();
        }

        // Counts across ALL loaded items (every page/filter), matching the original
        // GetCheckedItemsCount() semantics — a selection made under one filter must
        // still count after the user changes the filter or page.
        private void SyncSelectionState()
        {
            SelectedCount = (_allItems ?? new List<ItemDto>()).Count(i => i != null && i.Selected);

            var scoped = _filteredItems ?? _allItems ?? new List<ItemDto>();
            if (scoped.Count == 0)
                SelectAllState = false;
            else
            {
                int selectedInScope = scoped.Count(i => i.Selected);
                SelectAllState = selectedInScope == 0 ? (bool?)false
                    : selectedInScope == scoped.Count ? (bool?)true
                    : null;
            }

            OnPropertyChanged(nameof(HasSelection));
        }

        // Re-adds the currently visible page's items so the DataGrid re-pulls cell
        // values for rows whose Selected flag changed programmatically (ItemDto has
        // no INotifyPropertyChanged, so the grid otherwise won't notice the mutation).
        private void RefreshCurrentPageDisplay()
        {
            var current = PagedItems.ToList();
            PagedItems.Clear();
            foreach (var item in current)
                PagedItems.Add(item);
        }

        private void ApplyFiltersFromTop()
        {
            _currentPage = 1;
            ApplyFilters();
        }

        /// <summary>Every field the quick search box matches against — an item matches if all typed
        /// terms each show up somewhere in here (AND across terms, OR across fields).</summary>
        private static IEnumerable<string> ItemSearchFields(ItemDto item) => new[]
        {
            item.Name, item.Description, item.Category, item.ModelNumber, item.SerialNumber,
            item.UnitOfMeasure, item.ConditionName, item.ItemType, item.VendorName,
            item.LatestStatus, item.Remarks, item.LatestRemark, item.LastRepairAction
        };

        /// <summary>Identifier-shaped fields only (Model/Serial Number) — the subset it's meaningful
        /// to run an edit-distance comparison against for a code-shaped typo. Free-text fields like
        /// Name/Description/Vendor are excluded here: comparing a short mistyped code against a long
        /// sentence would never land within the strict distance-2 threshold anyway.</summary>
        private static IEnumerable<string> ItemFuzzyCandidateFields(ItemDto item) => new[]
        {
            item.ModelNumber, item.SerialNumber
        };

        /// <summary>Name/Description only — used when the mistyped term itself is a descriptive word
        /// (e.g. a brand: "Xiaoomi" vs "Xiaomi") rather than a code. Deliberately narrower than the
        /// full descriptive field set (no Vendor/Category/Remarks/etc.): those are short enough or
        /// generic enough that fuzzing against them risks matching unrelated items by coincidence.</summary>
        private static IEnumerable<string> ItemDescriptiveFuzzyCandidateFields(ItemDto item) => new[]
        {
            item.Name, item.Description
        };

        /// <summary>Category/Serial/Sub-Type/Active-Inactive/column filters — every active filter
        /// EXCEPT the quick search text. Shared between the normal pipeline and the fuzzy fallback so
        /// "closest match" results still respect every other filter the user has set.</summary>
        private IEnumerable<ItemDto> ApplyStructuralFilters(IEnumerable<ItemDto> source)
        {
            var filtered = source;

            var selectedCategories = CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name).ToList();
            if (selectedCategories.Count > 0)
                filtered = filtered.Where(item => item.Category != null && selectedCategories.Contains(item.Category, StringComparer.OrdinalIgnoreCase));

            var serialFilter = (_serialFilter ?? string.Empty).Trim().ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(serialFilter))
                filtered = filtered.Where(item => item.SerialNumber != null && item.SerialNumber.ToLowerInvariant().Contains(serialFilter));

            var selectedSubTypes = new List<string>();
            if (SubTypeFilterContract) selectedSubTypes.Add("Contract");
            if (SubTypeFilterSubscription) selectedSubTypes.Add("Subscription");
            if (SubTypeFilterLicense) selectedSubTypes.Add("License");
            if (SubTypeFilterServices) selectedSubTypes.Add("Services");

            if (SubTypeFilterNonSubType || SubTypeFilterAnySubtype || selectedSubTypes.Count > 0)
            {
                filtered = filtered.Where(item =>
                    (SubTypeFilterNonSubType && string.IsNullOrWhiteSpace(item.SubType)) ||
                    (SubTypeFilterAnySubtype && !string.IsNullOrWhiteSpace(item.SubType)) ||
                    (selectedSubTypes.Count > 0 && item.SubType != null && selectedSubTypes.Contains(item.SubType, StringComparer.OrdinalIgnoreCase)));
            }

            switch (_itemsSummaryMode)
            {
                case ItemsSummaryMode.ActiveOnly:
                    filtered = filtered.Where(item => item.Active);
                    break;
                case ItemsSummaryMode.InactiveOnly:
                    filtered = filtered.Where(item => !item.Active);
                    break;
                // All: no additional filter.
            }

            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                switch (kvp.Key)
                {
                    case "Name": filtered = filtered.Where(i => selected.Contains(i.Name ?? "", StringComparer.OrdinalIgnoreCase)); break;
                    case "Category": filtered = filtered.Where(i => selected.Contains(i.Category ?? "", StringComparer.OrdinalIgnoreCase)); break;
                    case "ItemType": filtered = filtered.Where(i => selected.Contains(i.ItemType ?? "", StringComparer.OrdinalIgnoreCase)); break;
                    case "ModelNumber": filtered = filtered.Where(i => selected.Contains(i.ModelNumber ?? "", StringComparer.OrdinalIgnoreCase)); break;
                    case "SerialNumber": filtered = filtered.Where(i => selected.Contains(i.SerialNumber ?? "", StringComparer.OrdinalIgnoreCase)); break;
                    case "VendorName": filtered = filtered.Where(i => selected.Contains(i.VendorName ?? "", StringComparer.OrdinalIgnoreCase)); break;
                }
            }

            return filtered;
        }

        /// <summary>
        /// Strict "Did you mean...?" fallback — only called when the exact search-text match found
        /// nothing. Only terms that look like an identifier (Model/Serial Number: has a digit or a
        /// "-") get relaxed into an edit-distance comparison; purely-descriptive terms (e.g. an item
        /// name) still have to match exactly somewhere, which is what keeps this targeted instead of
        /// surfacing unrelated items. Candidates come from <paramref name="pool"/> — the items already
        /// passing every OTHER active filter — so a "closest match" never ignores filters the user
        /// deliberately set. Discards anything past edit distance 2.
        /// </summary>
        private List<ItemDto> FuzzyMatchItems(List<ItemDto> pool, string[] searchTerms, int maxResults = 5)
        {
            // A term "needs" fuzzing if it didn't find a single exact match anywhere in the pool —
            // covers both a code-shaped typo (Serial/Model Number) and a descriptive/brand-name typo
            // (Name/Description, e.g. "Xiaoomi" vs "Xiaomi"). Every OTHER term still has to match
            // exactly — that's what keeps this targeted instead of surfacing unrelated items.
            // Everything here runs against `pool`, which is already the full item table held in
            // memory (see ItemsPageViewModel.Queries.cs — no TOP/paging on the load query), so this
            // is a plain in-memory scan: no SQL round trip, no debounce needed, sub-millisecond even
            // over a couple thousand rows.
            string fuzzTerm = null;
            foreach (var term in searchTerms)
            {
                bool anyExactMatch = pool.Any(i => ItemSearchFields(i).Any(f =>
                    f != null && f.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0));
                if (!anyExactMatch) { fuzzTerm = term; break; }
            }
            // Every term matched something individually — the zero-result came from their AND
            // combination (e.g. two real words that just never co-occur on one item), not a typo.
            if (fuzzTerm == null) return new List<ItemDto>();

            var otherTerms = searchTerms.Where(t => t != fuzzTerm).ToArray();
            Func<ItemDto, IEnumerable<string>> fuzzFields = SearchTextHelper.LooksLikeCode(fuzzTerm)
                ? (Func<ItemDto, IEnumerable<string>>)ItemFuzzyCandidateFields
                : ItemDescriptiveFuzzyCandidateFields;

            return pool
                .Where(i => SearchTextHelper.MatchesAllTerms(otherTerms, ItemSearchFields(i)))
                .Select(i => new
                {
                    Item = i,
                    Distance = fuzzFields(i)
                        .Where(f => !string.IsNullOrWhiteSpace(f))
                        .Select(f => SearchTextHelper.MinWordDistance(fuzzTerm, f))
                        .DefaultIfEmpty(int.MaxValue)
                        .Min()
                })
                .Where(x => x.Distance <= 2)
                .OrderBy(x => x.Distance)
                .ThenBy(x => x.Item.Name)
                .Take(maxResults)
                .Select(x => x.Item)
                .ToList();
        }

        private void ApplyFilters()
        {
            if (_allItems == null) return;

            var searchTerms = SearchTextHelper.SplitTerms(_searchText);

            var structuralPool = ApplyStructuralFilters(_allItems);

            IEnumerable<ItemDto> filtered = structuralPool;
            if (searchTerms.Length > 0)
                filtered = filtered.Where(item => SearchTextHelper.MatchesAllTerms(searchTerms, ItemSearchFields(item)));

            var list = filtered.ToList();

            // Only ever runs when the exact search-text match above found nothing at all — the
            // normal path above is completely unaffected. Relaxes just the search text; every other
            // active filter still applies via the same structural pool.
            bool isFuzzy = false;
            if (list.Count == 0 && searchTerms.Length > 0)
            {
                var fuzzyMatches = FuzzyMatchItems(structuralPool.ToList(), searchTerms);
                if (fuzzyMatches.Count > 0)
                {
                    isFuzzy = true;
                    list = fuzzyMatches;
                }
            }
            IsFuzzyResults = isFuzzy;
            FuzzyNoticeText = isFuzzy
                ? $"No exact matches for \"{_searchText}\" — showing the closest results instead."
                : "";

            if (_sortProperty != null)
            {
                var propInfo = typeof(ItemDto).GetProperty(_sortProperty);
                if (propInfo != null)
                {
                    list = _sortAscending
                        ? list.OrderBy(x => propInfo.GetValue(x, null)).ToList()
                        : list.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
            }
            else if (_selectedSortBy == "Most Recently Added")
                list = list.OrderByDescending(x => x.DateCreated).ToList();
            else if (_selectedSortBy == "Oldest Added")
                list = list.OrderBy(x => x.DateCreated).ToList();
            else if (_selectedSortBy == "Date Purchased (Newest)")
                list = list.OrderByDescending(x => x.DatePurchased).ToList();
            else if (_selectedSortBy == "Date Purchased (Oldest)")
                list = list.OrderBy(x => x.DatePurchased).ToList();
            else
                list = list.OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.ItemId).ToList();

            // When "Show Inactive" is on, surface inactive rows at the top (stable sort keeps
            // the ordering chosen above within each Active/Inactive group).
            if (_showInactive)
                list = list.OrderByDescending(x => !x.Active).ToList();

            _filteredItems = list;
            _currentPage = 1;
            RebuildItemsPage();
        }

        private void RebuildItemsPage()
        {
            int totalPages = TotalPages();
            if (_currentPage > totalPages) _currentPage = totalPages;
            if (_currentPage < 1) _currentPage = 1;

            var count = _filteredItems?.Count ?? 0;

            PagedItems.Clear();
            if (count > 0)
            {
                foreach (var item in _filteredItems.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                    PagedItems.Add(item);
            }

            PageInfo = count == 0
                ? "Page 0 of 0 (0 items)"
                : $"Page {_currentPage} of {totalPages} ({count} items)";

            CanGoFirst = _currentPage > 1;
            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < totalPages;
            CanGoLast = _currentPage < totalPages;

            SyncSelectionState();
        }

        private void RebuildCategoryPage()
        {
            int totalPages = CategoryTotalPages();
            if (_categoryCurrentPage > totalPages) _categoryCurrentPage = totalPages;
            if (_categoryCurrentPage < 1) _categoryCurrentPage = 1;

            var count = _allCategorySummaries?.Count ?? 0;

            PagedCategorySummaries.Clear();
            if (count > 0)
            {
                foreach (var row in _allCategorySummaries.Skip((_categoryCurrentPage - 1) * CategoryPageSize).Take(CategoryPageSize))
                    PagedCategorySummaries.Add(row);
            }

            CategoryPageInfo = count == 0
                ? "Page 0 of 0 (0 items)"
                : $"Page {_categoryCurrentPage} of {totalPages} ({count} items)";

            CategoryCanGoFirst = _categoryCurrentPage > 1;
            CategoryCanGoPrev = _categoryCurrentPage > 1;
            CategoryCanGoNext = _categoryCurrentPage < totalPages;
            CategoryCanGoLast = _categoryCurrentPage < totalPages;
        }

        private void RebuildCategoryOptions()
        {
            var previouslySelected = new HashSet<string>(
                CategoryFilterOptions.Where(c => c.IsSelected).Select(c => c.Name),
                StringComparer.OrdinalIgnoreCase);

            var categories = (_allItems ?? new List<ItemDto>())
                .Select(i => i.Category)
                .Where(c => !string.IsNullOrEmpty(c))
                .Distinct()
                .OrderBy(c => c)
                .ToList();

            _suppressCategoryFilterChange = true;
            try
            {
                CategoryFilterOptions.Clear();
                foreach (var cat in categories)
                {
                    CategoryFilterOptions.Add(new CategoryFilterOption(cat, () => _suppressCategoryFilterChange, OnCategoryFilterChanged)
                    {
                        IsSelected = previouslySelected.Contains(cat)
                    });
                }
            }
            finally
            {
                _suppressCategoryFilterChange = false;
            }

            UpdateCategoryFilterSummary();
        }

        /// <summary>Pre-fills the search box and applies the filter (called from the home-page search).</summary>
        public void ApplyInitialSearch(string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return;
            SearchText = query;
        }
    }
}
