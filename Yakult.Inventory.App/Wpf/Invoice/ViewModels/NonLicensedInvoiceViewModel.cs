using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Business logic for the "Non-Licensed Invoices" page — a read-only browse/search/report
    /// view of every Hardware invoice line already confirmed as NonLicensed via the Invoice
    /// License Review page. Loads from the read-only dbo.vw_ConfirmedNonLicensedInvoiceItems
    /// view; nothing here writes to the database.
    /// </summary>
    public sealed class NonLicensedInvoiceViewModel : ViewModelBase
    {
        private readonly NonLicensedInvoiceRepository _repository = new NonLicensedInvoiceRepository();
        private readonly SetRepository _setRepository = new SetRepository();

        private List<NonLicensedInvoiceRow> _allRows = new List<NonLicensedInvoiceRow>();
        private List<NonLicensedInvoiceRow> _filteredRows = new List<NonLicensedInvoiceRow>();

        public NonLicensedInvoiceViewModel()
        {
            RefreshCommand = new RelayCommand(async () => await LoadAsync());
            ClearCategoryFilterCommand = new RelayCommand(ClearCategoryFilter);
            ClearItemTypeFilterCommand = new RelayCommand(ClearItemTypeFilter);

            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; BindPage(); });
            PrevPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, CurrentPage - 1); BindPage(); });
            NextPageCommand = new RelayCommand(() => { CurrentPage = CurrentPage + 1; BindPage(); });
            LastPageCommand = new RelayCommand(() => { CurrentPage = Math.Max(1, GetTotalPages()); BindPage(); });
        }

        // ── Events (WinForms interop seam — wired by the View's code-behind) ────
        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestWarning;
        public event Action<string, string> RequestError;

        /// <summary>Raised when the user wants to look at the actual invoice for one row.</summary>
        public event Action<NonLicensedInvoiceRow> RequestPreviewInvoice;

        // ── Commands ─────────────────────────────────────────────────────────
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ClearCategoryFilterCommand { get; }
        public RelayCommand ClearItemTypeFilterCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        // ── Grid data ────────────────────────────────────────────────────────
        public ObservableCollection<NonLicensedInvoiceRow> Rows { get; } = new ObservableCollection<NonLicensedInvoiceRow>();

        private bool _isLoading;
        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }

        private int _totalCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }

        // ── Search ───────────────────────────────────────────────────────────
        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        // ── Category / Item Type multi-select filters (same pattern as Invoice License Review) ──
        public ObservableCollection<CategoryFilterOption> CategoryFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();
        public ObservableCollection<CategoryFilterOption> ItemTypeFilterOptions { get; } = new ObservableCollection<CategoryFilterOption>();

        private string _categoryFilterSummary = "All Categories";
        public string CategoryFilterSummary { get => _categoryFilterSummary; private set => SetField(ref _categoryFilterSummary, value); }

        private string _itemTypeFilterSummary = "All Item Types";
        public string ItemTypeFilterSummary { get => _itemTypeFilterSummary; private set => SetField(ref _itemTypeFilterSummary, value); }

        private bool _suppressCategoryFilterChange;
        private bool _suppressItemTypeFilterChange;

        private const string UncategorizedLabel = "(Uncategorized)";

        // ── Pagination ───────────────────────────────────────────────────────
        public ObservableCollection<int> PageSizeOptions { get; } = new ObservableCollection<int> { 25, 50, 100, 200 };

        private int _pageSize = 50;
        public int PageSize
        {
            get => _pageSize;
            set
            {
                if (!SetField(ref _pageSize, value)) return;
                CurrentPage = 1;
                BindPage();
            }
        }

        private int _currentPage = 1;
        public int CurrentPage { get => _currentPage; private set => SetField(ref _currentPage, value); }

        private string _pageInfoText = "Page 0 of 0 (0 items)";
        public string PageInfoText { get => _pageInfoText; private set => SetField(ref _pageInfoText, value); }

        private int GetTotalPages()
        {
            var total = _filteredRows?.Count ?? 0;
            return total <= 0 ? 0 : (int)Math.Ceiling(total / (double)PageSize);
        }

        private void BindPage()
        {
            var src = _filteredRows ?? new List<NonLicensedInvoiceRow>();
            var totalPages = GetTotalPages();

            if (src.Count == 0)
            {
                Rows.Clear();
                PageInfoText = "Page 0 of 0 (0 items)";
                return;
            }

            CurrentPage = totalPages > 0 ? Math.Max(1, Math.Min(CurrentPage, totalPages)) : 1;

            var pageItems = src.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            Rows.Clear();
            foreach (var item in pageItems) Rows.Add(item);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({src.Count} items)";
        }

        // ── Loading ──────────────────────────────────────────────────────────
        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var dtos = await _repository.GetConfirmedNonLicensedItemsAsync();
                var rows = dtos.Select(MapRow).ToList();

                var setIds = rows.Select(r => r.SetId).Distinct().ToList();
                var itemTypesBySetId = await Task.Run(() => _setRepository.GetItemTypesBySetIds(setIds));
                var categoriesBySetId = await Task.Run(() => _setRepository.GetCategoriesBySetIds(setIds));
                foreach (var row in rows)
                {
                    row.ItemTypesOnInvoice = itemTypesBySetId.TryGetValue(row.SetId, out var types) ? types : Array.Empty<string>();
                    row.CategoriesOnInvoice = categoriesBySetId.TryGetValue(row.SetId, out var cats) ? cats : Array.Empty<string>();
                }

                _allRows = rows;

                LoadCategoryFilterOptions();
                LoadItemTypeFilterOptions();
                ApplyFilters();
            }
            catch (SqlException ex)
            {
                RequestError?.Invoke(
                    "Database Error",
                    $"Lost connection to the database, or a database error occurred while loading non-licensed invoices:\n\n{ex.Message}");
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load non-licensed invoice items:\n\n{ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private static NonLicensedInvoiceRow MapRow(NonLicensedInvoiceRepository.ConfirmedNonLicensedItemDto dto) => new NonLicensedInvoiceRow
        {
            ItemId = dto.ItemId,
            SetId = dto.SetId,
            ReqId = dto.ReqId,
            InvoiceNumber = dto.InvoiceNumber,
            PONumber = dto.PONumber,
            InvoiceDate = dto.InvoiceDate,
            Status = dto.Status,
            Site = dto.Site,
            CompanyName = dto.CompanyName,
            InvoiceTotalAmount = dto.InvoiceTotalAmount,
            Department = dto.Department,
            Employee = dto.Employee,
            ItemCode = dto.ItemCode,
            ItemName = dto.ItemName,
            ItemDescription = dto.ItemDescription,
            ItemType = dto.ItemType,
            ModelNumber = dto.ModelNumber,
            SerialNumber = dto.SerialNumber,
            CategoryName = dto.CategoryName,
            LegacyCategoryText = dto.LegacyCategoryText,
            Quantity = dto.Quantity,
            Unit = dto.Unit,
            UnitPrice = dto.UnitPrice,
            LineTotal = dto.LineTotal,
            VendorName = dto.VendorName,
            ConditionName = dto.ConditionName,
            ReviewedByName = dto.ReviewedByName,
            ReviewedAt = dto.ReviewedAt,
            CreatedAt = dto.CreatedAt,
        };

        // ── Filtering ────────────────────────────────────────────────────────
        private void ApplyFilters()
        {
            IEnumerable<NonLicensedInvoiceRow> filtered = _allRows;

            var terms = SearchTextHelper.SplitTerms(SearchText);
            if (terms.Length > 0)
            {
                filtered = filtered.Where(r => SearchTextHelper.MatchesAllTerms(terms, new[]
                {
                    r.InvoiceNumber, r.PONumber, r.ItemName, r.ItemDescription, r.ModelNumber,
                    r.SerialNumber, r.DisplayCategory, r.CompanyName, r.Department, r.Employee,
                    r.VendorName, r.ReviewedByName
                }));
            }

            var selectedCategories = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedCategories.Count > 0)
            {
                filtered = filtered.Where(r =>
                    selectedCategories.Contains(string.IsNullOrWhiteSpace(r.DisplayCategory) ? UncategorizedLabel : r.DisplayCategory, StringComparer.OrdinalIgnoreCase));
            }

            var selectedItemTypes = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            if (selectedItemTypes.Count > 0)
            {
                filtered = filtered.Where(r =>
                    r.ItemTypesOnInvoice != null &&
                    r.ItemTypesOnInvoice.Any(t => selectedItemTypes.Contains(t, StringComparer.OrdinalIgnoreCase)));
            }

            _filteredRows = filtered.ToList();
            TotalCount = _filteredRows.Count;

            CurrentPage = 1;
            BindPage();
        }

        private void LoadCategoryFilterOptions()
        {
            var previouslyChecked = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();

            var allCategories = _allRows
                .Select(r => string.IsNullOrWhiteSpace(r.DisplayCategory) ? UncategorizedLabel : r.DisplayCategory)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressCategoryFilterChange = true;
            try
            {
                CategoryFilterOptions.Clear();
                foreach (var name in allCategories)
                {
                    var option = new CategoryFilterOption(name) { IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase) };
                    option.CheckedChanged += OnCategoryFilterOptionChanged;
                    CategoryFilterOptions.Add(option);
                }
            }
            finally { _suppressCategoryFilterChange = false; }

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
            try { foreach (var o in CategoryFilterOptions) o.IsChecked = false; }
            finally { _suppressCategoryFilterChange = false; }

            UpdateCategoryFilterSummary();
            ApplyFilters();
        }

        private void UpdateCategoryFilterSummary()
        {
            var names = CategoryFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            CategoryFilterSummary = names.Count == 0 ? "All Categories" : names.Count == 1 ? names[0] : $"{names.Count} categories selected";
        }

        private void LoadItemTypeFilterOptions()
        {
            var previouslyChecked = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();

            var allTypes = _allRows
                .SelectMany(r => r.ItemTypesOnInvoice ?? Array.Empty<string>())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
                .ToList();

            _suppressItemTypeFilterChange = true;
            try
            {
                ItemTypeFilterOptions.Clear();
                foreach (var name in allTypes)
                {
                    var option = new CategoryFilterOption(name) { IsChecked = previouslyChecked.Contains(name, StringComparer.OrdinalIgnoreCase) };
                    option.CheckedChanged += OnItemTypeFilterOptionChanged;
                    ItemTypeFilterOptions.Add(option);
                }
            }
            finally { _suppressItemTypeFilterChange = false; }

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
            try { foreach (var o in ItemTypeFilterOptions) o.IsChecked = false; }
            finally { _suppressItemTypeFilterChange = false; }

            UpdateItemTypeFilterSummary();
            ApplyFilters();
        }

        private void UpdateItemTypeFilterSummary()
        {
            var names = ItemTypeFilterOptions.Where(o => o.IsChecked).Select(o => o.Name).ToList();
            ItemTypeFilterSummary = names.Count == 0 ? "All Item Types" : names.Count == 1 ? names[0] : $"{names.Count} item types selected";
        }

        // ── Preview ──────────────────────────────────────────────────────────
        public void PreviewInvoice(NonLicensedInvoiceRow row)
        {
            if (row == null) return;
            RequestPreviewInvoice?.Invoke(row);
        }
    }
}
