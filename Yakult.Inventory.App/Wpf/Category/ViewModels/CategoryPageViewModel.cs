using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.Category.ViewModels
{
    /// <summary>
    /// Business logic for the Categories page, ported verbatim from
    /// Pages\Category\ViewCategoryPage.cs. Two selection mechanisms are preserved exactly as
    /// they existed in the original: Edit/ToggleActive/Archive act on the single grid ROW
    /// selection (SelectedCategory, mirrors dgvCategories.SelectedRows[0]), while Delete acts
    /// on the checkbox-column multi-selection (mirrors the "colSelect" checkbox loop, scoped to
    /// the currently-paged rows only). No permission gating exists on this page today (none
    /// added here). "Status" column sorting is a deliberate no-op (see SortMemberPath note on
    /// the View's Status column) — the original DTO never had a real "StatusDisplay" property,
    /// so its reflection-based column sort silently found nothing and did nothing; preserved
    /// by intentionally keying that column's SortMemberPath to a name absent from ItemCategoryDto.
    /// </summary>
    public sealed class CategoryPageViewModel : ViewModelBase
    {
        private const int PageSize = 10;

        private readonly CategoryRepository _repo = new CategoryRepository();
        private readonly string _connectionString = Core.DatabaseConfig.ConnectionString;

        private List<ItemCategoryDto> _allCategories = new List<ItemCategoryDto>();
        private List<ItemCategoryDto> _filteredCategories = new List<ItemCategoryDto>();

        private string _sortColumnKey;
        private ListSortDirection? _sortDirection;
        private bool _suppressSortByChange;

        public CategoryPageViewModel()
        {
            FilterByOptions = new ObservableCollection<string> { "Default", "Most Recently Added", "Oldest Added" };
            SortByOptions = new ObservableCollection<ListSortOption>();
            PagedCategories = new ObservableCollection<ItemCategoryDto>();

            _selectedFilterBy = "Default";

            AddCommand = new RelayCommand(Add);
            EditCommand = new RelayCommand(Edit);
            ToggleActiveCommand = new RelayCommand(ToggleActive);
            ArchiveCommand = new RelayCommand(Archive);
            DeleteCommand = new RelayCommand(Delete);
            RefreshCommand = new RelayCommand(async () => await LoadCategoriesAsync());

            _selection = new SelectionTracker<ItemCategoryDto>(c => c.Selected, (c, s) => c.Selected = s, c => c.CategoryId);
            _selection.Changed += () =>
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            };
            FirstPageCommand = new RelayCommand(() => { CurrentPage = 1; UpdatePagination(); });
            PrevPageCommand = new RelayCommand(() => { if (CurrentPage > 1) { CurrentPage--; UpdatePagination(); } });
            NextPageCommand = new RelayCommand(() => { if (CurrentPage < TotalPages) { CurrentPage++; UpdatePagination(); } });
            LastPageCommand = new RelayCommand(() => { CurrentPage = TotalPages; UpdatePagination(); });

            TotalCardCommand = new RelayCommand(() => SetSummaryMode(CategorySummaryMode.All));
            ActiveCardCommand = new RelayCommand(() => SetSummaryMode(CategorySummaryMode.ActiveOnly));
            InactiveCardCommand = new RelayCommand(() => SetSummaryMode(CategorySummaryMode.InactiveOnly));
            ResetFiltersCommand = new RelayCommand(ResetFilters);
        }

        public RelayCommand ResetFiltersCommand { get; private set; }

        /// <summary>Clears every filter control back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedFilterBy = "Default";
            OnPropertyChanged(nameof(SelectedFilterBy));

            _showInactive = false;
            OnPropertyChanged(nameof(ShowInactive));
            _summaryMode = CategorySummaryMode.ActiveOnly;
            NotifySummaryCardsChanged();

            // Clear the active column sort and ask the View to rebuild/reselect the Sort By
            // dropdown's default entry — mirrors the initial-load selection in SetSortByOptions.
            _sortColumnKey = null;
            _sortDirection = null;
            RebuildSortByOptionsRequested?.Invoke();

            ApplyFilters();
        }

        /// <summary>Raised so the View can recompute and reselect the Sort By dropdown's
        /// default entry (the View owns the DataGrid needed to resolve column keys).</summary>
        public event Action RebuildSortByOptionsRequested;

        // ── Filter/search state ──────────────────────────────────────────────

        private string _searchText;
        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilters(); }
        }

        private bool _showInactive;
        public bool ShowInactive
        {
            get => _showInactive;
            set
            {
                if (!SetField(ref _showInactive, value)) return;
                _summaryMode = value ? CategorySummaryMode.All : CategorySummaryMode.ActiveOnly;
                NotifySummaryCardsChanged();
                ApplyFilters();
            }
        }

        // ── Clickable summary cards (Total/Active/Inactive) ──────────────────
        private enum CategorySummaryMode { All, ActiveOnly, InactiveOnly }
        private CategorySummaryMode _summaryMode = CategorySummaryMode.ActiveOnly;

        public bool IsTotalCardActive => _summaryMode == CategorySummaryMode.All;
        public bool IsActiveCardActive => _summaryMode == CategorySummaryMode.ActiveOnly;
        public bool IsInactiveCardActive => _summaryMode == CategorySummaryMode.InactiveOnly;

        public RelayCommand TotalCardCommand { get; private set; }
        public RelayCommand ActiveCardCommand { get; private set; }
        public RelayCommand InactiveCardCommand { get; private set; }

        private void SetSummaryMode(CategorySummaryMode mode)
        {
            _summaryMode = mode;
            NotifySummaryCardsChanged();

            bool needsShowInactive = mode != CategorySummaryMode.ActiveOnly;
            if (_showInactive != needsShowInactive)
            {
                _showInactive = needsShowInactive;
                OnPropertyChanged(nameof(ShowInactive));
            }
            ApplyFilters();
        }

        private void NotifySummaryCardsChanged()
        {
            OnPropertyChanged(nameof(IsTotalCardActive));
            OnPropertyChanged(nameof(IsActiveCardActive));
            OnPropertyChanged(nameof(IsInactiveCardActive));
        }

        private int _totalCount, _activeCount, _inactiveCount;
        public int TotalCount { get => _totalCount; private set => SetField(ref _totalCount, value); }
        public int ActiveCount { get => _activeCount; private set => SetField(ref _activeCount, value); }
        public int InactiveCount { get => _inactiveCount; private set => SetField(ref _inactiveCount, value); }

        private void UpdateSummaryCards(List<ItemCategoryDto> list)
        {
            TotalCount = list.Count;
            ActiveCount = list.Count(c => c.Active);
            InactiveCount = list.Count(c => !c.Active);
        }

        public ObservableCollection<string> FilterByOptions { get; }

        private string _selectedFilterBy;
        public string SelectedFilterBy
        {
            get => _selectedFilterBy;
            set
            {
                if (SetField(ref _selectedFilterBy, value))
                {
                    // Mirrors cmbFilterBy.SelectedIndexChanged: picking any Filter-By option
                    // (including "Default") cancels an active column sort.
                    _sortColumnKey = null;
                    _sortDirection = null;
                    ApplyFilters();
                }
            }
        }

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

                // Unlike Vendor/Asset, picking a Sort-By option here does NOT reset Filter-By
                // (matches the original SetupSortByDropdown callback, which never touches
                // cmbFilterBy).
                _sortColumnKey = value.ColumnKey;
                _sortDirection = value.Direction;
                ApplyFilters();
            }
        }

        public void SetSortByOptions(List<ListSortOption> options, string defaultColumnKey)
        {
            _suppressSortByChange = true;
            try
            {
                SortByOptions.Clear();
                foreach (var o in options) SortByOptions.Add(o);

                ListSortOption selected = null;
                if (_sortColumnKey != null)
                {
                    selected = options.FirstOrDefault(o =>
                        o.ColumnKey == _sortColumnKey && o.Direction == (_sortDirection ?? ListSortDirection.Ascending));
                }
                if (selected == null && !string.IsNullOrEmpty(defaultColumnKey))
                {
                    selected = options.FirstOrDefault(o => o.ColumnKey == defaultColumnKey && o.Direction == ListSortDirection.Ascending);
                }

                SelectedSortBy = selected ?? options.FirstOrDefault();
            }
            finally
            {
                _suppressSortByChange = false;
            }
        }

        /// <summary>Called by the View's DataGrid.Sorting handler (column header click).</summary>
        public void SortByColumn(string columnKey, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(columnKey)) return;

            _sortColumnKey = columnKey;
            _sortDirection = direction;
            ApplyFilters();

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

        // ── Paging ────────────────────────────────────────────────────────────

        public ObservableCollection<ItemCategoryDto> PagedCategories { get; }

        // ── Selection (tracked against _allCategories, not just the current page) ───────
        private readonly SelectionTracker<ItemCategoryDto> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public List<ItemCategoryDto> GetSelectedCategories() => _selection.GetSelected(_allCategories);
        public void SetCategorySelected(int categoryId, bool selected) => _selection.SetSelected(_allCategories, categoryId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedCategories, _allCategories, selected);
        public void ClearSelection() => _selection.ClearSelection(_allCategories);

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            set => SetField(ref _currentPage, value);
        }

        private int TotalPages => (_filteredCategories?.Count ?? 0) == 0 ? 0 : (int)Math.Ceiling((double)_filteredCategories.Count / PageSize);

        private string _pageInfoText = "Page 0 of 0 (0 categories)";
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

        public RelayCommand AddCommand { get; }
        public RelayCommand EditCommand { get; }
        public RelayCommand ToggleActiveCommand { get; }
        public RelayCommand ArchiveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand FirstPageCommand { get; }
        public RelayCommand PrevPageCommand { get; }
        public RelayCommand NextPageCommand { get; }
        public RelayCommand LastPageCommand { get; }

        private void UpdatePagination()
        {
            var data = _filteredCategories ?? new List<ItemCategoryDto>();
            if (data.Count == 0)
            {
                PagedCategories.Clear();
                PageInfoText = "Page 0 of 0 (0 categories)";
                CanFirstPage = CanPrevPage = CanNextPage = CanLastPage = false;
                return;
            }

            int totalPages = TotalPages;
            if (CurrentPage > totalPages) CurrentPage = totalPages;
            if (CurrentPage < 1) CurrentPage = 1;

            var paged = data.Skip((CurrentPage - 1) * PageSize).Take(PageSize).ToList();

            PagedCategories.Clear();
            foreach (var c in paged) PagedCategories.Add(c);

            PageInfoText = $"Page {CurrentPage} of {totalPages} ({data.Count} categories)";
            CanFirstPage = CurrentPage > 1;
            CanPrevPage = CurrentPage > 1;
            CanNextPage = CurrentPage < totalPages;
            CanLastPage = CurrentPage < totalPages;
        }

        // ── Row selection (Edit/ToggleActive/Archive act on this) ───────────────

        private ItemCategoryDto _selectedCategory;
        public ItemCategoryDto SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetField(ref _selectedCategory, value))
                    OnPropertyChanged(nameof(ToggleActiveButtonText));
            }
        }

        public string ToggleActiveButtonText => (SelectedCategory != null && !SelectedCategory.Active) ? "Activate" : "Deactivate";

        // ── Data load ─────────────────────────────────────────────────────────

        public event Action<string, string> RequestInfo;
        public event Action<string, string> RequestError;
        public event Action<string, string> RequestWarning;
        public event Action RequestAddCategory;
        public event Action<ItemCategoryDto> RequestEditCategory;

        /// <summary>Set by the View to a WinForms Yes/No confirmation prompt.</summary>
        public Func<string, string, bool> ConfirmYesNo { get; set; }

        public async Task LoadCategoriesAsync()
        {
            try
            {
                _allCategories = await _repo.GetAllAsync() ?? new List<ItemCategoryDto>();
                ApplyFilters();
                _selection.Sync(_allCategories);
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to load categories: {ex.Message}");
            }
        }

        private void ApplyFilters()
        {
            if (_allCategories == null) _allCategories = new List<ItemCategoryDto>();

            var filtered = _allCategories.AsEnumerable();

            string searchText = SearchText?.Trim().ToLower();
            if (!string.IsNullOrWhiteSpace(searchText))
                filtered = filtered.Where(c => c.Name != null && c.Name.ToLower().Contains(searchText));

            var forSummary = filtered.ToList();
            UpdateSummaryCards(forSummary);
            filtered = forSummary;

            switch (_summaryMode)
            {
                case CategorySummaryMode.ActiveOnly:
                    filtered = filtered.Where(c => c.Active);
                    break;
                case CategorySummaryMode.InactiveOnly:
                    filtered = filtered.Where(c => !c.Active);
                    break;
                // All: no filter
            }

            _filteredCategories = filtered.ToList();

            if (_sortColumnKey != null)
            {
                var propInfo = typeof(ItemCategoryDto).GetProperty(_sortColumnKey);
                if (propInfo != null)
                {
                    _filteredCategories = _sortDirection == ListSortDirection.Ascending
                        ? _filteredCategories.OrderBy(x => propInfo.GetValue(x, null)).ToList()
                        : _filteredCategories.OrderByDescending(x => propInfo.GetValue(x, null)).ToList();
                }
                // else: propInfo not found (e.g. the "Status" column's key) — original left the
                // order untouched in this case, so we do too.
            }
            else
            {
                // "Filter By" fallback sort — matches ApplyFilters: "Default" behaves the same
                // as "Most Recently Added" here (unlike Vendor/Asset, where Default means
                // "leave order alone").
                if (SelectedFilterBy == "Most Recently Added" || SelectedFilterBy == "Default")
                    _filteredCategories = _filteredCategories.OrderByDescending(x => x.DateCreated).ToList();
                else if (SelectedFilterBy == "Oldest Added")
                    _filteredCategories = _filteredCategories.OrderBy(x => x.DateCreated).ToList();
            }

            CurrentPage = 1;
            UpdatePagination();
        }

        // ── Actions ───────────────────────────────────────────────────────────

        private void Add() => RequestAddCategory?.Invoke();

        private void Edit()
        {
            if (SelectedCategory == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a category to edit.");
                return;
            }
            RequestEditCategory?.Invoke(SelectedCategory);
        }

        private void ToggleActive()
        {
            if (SelectedCategory == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a category.");
                return;
            }

            string action = SelectedCategory.Active ? "deactivate" : "activate";
            string title = "Confirm " + char.ToUpper(action[0]) + action.Substring(1);

            bool confirm = ConfirmYesNo?.Invoke(title, $"Are you sure you want to {action} the category '{SelectedCategory.Name}'?") ?? false;
            if (!confirm) return;

            SelectedCategory.Active = !SelectedCategory.Active;
            _ = UpdateCategoryAsync(SelectedCategory);
        }

        private void Archive()
        {
            if (SelectedCategory == null)
            {
                RequestInfo?.Invoke("No Selection", "Please select a category to archive.");
                return;
            }

            bool confirm = ConfirmYesNo?.Invoke("Confirm Archive",
                $"Archive category '{SelectedCategory.Name}'?\n\nThe category will be moved to the archive.") ?? false;
            if (!confirm) return;

            _ = ArchiveCategoryAsync(SelectedCategory.CategoryId);
        }

        private void Delete() => _ = DeleteSelectedAsync();

        private async Task DeleteSelectedAsync()
        {
            // Reads across all pages, not just PagedCategories — consistent with the
            // "N Selected" badge, which also tracks selection across the full list.
            var checkedItems = _selection.GetSelected(_allCategories);

            if (checkedItems.Count == 0)
            {
                RequestWarning?.Invoke("No Selection", "Please select at least one category to delete.");
                return;
            }

            string itemList = string.Join("\n", checkedItems.Select(c => $"• {c.Name} (Items: {c.ItemCount})"));
            string countText = checkedItems.Count == 1 ? "this category" : $"these {checkedItems.Count} categories";

            bool confirm = ConfirmYesNo?.Invoke("Confirm Permanent Deletion",
                "⚠️ PERMANENT DELETE WARNING ⚠️\n\n" +
                $"This will PERMANENTLY delete {countText}:\n\n" +
                $"{itemList}\n\n" +
                "This action CANNOT be undone!\n\n" +
                "⚠️ Only proceed if this was a DATA ENTRY ERROR.\n" +
                "⚠️ Use 'Archive' button instead for normal operations.\n\n" +
                "Are you absolutely sure you want to permanently delete?") ?? false;

            if (!confirm) return;

            // Matches the original BtnDelete_Click: each delete is independent/fire-and-forget
            // (async void DeleteCategoryAsync per item, not a single awaited batch), so a
            // multi-select delete can show several sequential result dialogs and reloads.
            foreach (var category in checkedItems)
            {
                _ = DeleteCategoryAsync(category.CategoryId);
            }
        }

        private async Task DeleteCategoryAsync(int categoryId)
        {
            try
            {
                var (success, message) = await _repo.DeleteAsync(categoryId);
                if (success)
                {
                    RequestInfo?.Invoke("Success", message);
                    await LoadCategoriesAsync();
                }
                else
                {
                    RequestError?.Invoke("Error", message);
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to delete category: {ex.Message}");
            }
        }

        public async Task CreateCategoryAsync(string name)
        {
            try
            {
                var category = new ItemCategoryDto
                {
                    Name = name,
                    Active = true,
                    DateCreated = DateTime.Now,
                    CreatedBy = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1
                };

                int categoryId = await _repo.CreateAsync(category);
                RequestInfo?.Invoke("Success", $"Category created successfully! ID: {categoryId}");
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to create category: {ex.Message}");
            }
        }

        public async Task UpdateCategoryAsync(ItemCategoryDto category)
        {
            try
            {
                bool success = await _repo.UpdateAsync(category);
                if (success)
                {
                    RequestInfo?.Invoke("Success", "Category updated successfully!");
                    await LoadCategoriesAsync();
                }
                else
                {
                    RequestError?.Invoke("Error", "Category not found or update failed.");
                }
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to update category: {ex.Message}");
            }
        }

        private async Task ArchiveCategoryAsync(int categoryId)
        {
            try
            {
                using (var con = new SqlConnection(_connectionString))
                {
                    await con.OpenAsync();

                    string sql = @"
                        INSERT INTO dbo.ArchiveStatus (EntityType, EntityId, IsArchived, ArchivedAt, ArchivedBy, ArchiveReason)
                        VALUES ('ItemCategory', @CategoryId, 1, GETDATE(), @UserId, 'Archived from View Categories')";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@CategoryId", categoryId);
                        cmd.Parameters.AddWithValue("@UserId", AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }

                ActivityLogger.Log(ActivityLogger.Actions.Delete, "Category", categoryId, $"Category ID {categoryId} archived");
                RequestInfo?.Invoke("Success", "Category archived successfully!");
                await LoadCategoriesAsync();
            }
            catch (Exception ex)
            {
                RequestError?.Invoke("Error", $"Failed to archive category: {ex.Message}");
            }
        }

        /// <summary>Loads the active/inactive item breakdown for the Edit dialog, matching the
        /// original's inline load (before ShowDialog()) including its Warning-level error report.</summary>
        public async Task<(List<CategoryItemLocationDto> Active, List<CategoryItemLocationDto> Inactive)> LoadCategoryItemsAsync(int categoryId)
        {
            try
            {
                var allItems = await _repo.GetItemsByCategoryAsync(categoryId) ?? new List<CategoryItemLocationDto>();
                return (allItems.Where(i => i.Active).ToList(), allItems.Where(i => !i.Active).ToList());
            }
            catch (Exception ex)
            {
                RequestWarning?.Invoke("Error", $"Failed to load items: {ex.Message}");
                return (new List<CategoryItemLocationDto>(), new List<CategoryItemLocationDto>());
            }
        }
    }
}
