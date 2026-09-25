using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;
using Yakult.Inventory.App.WPF.Shared.Helpers;

namespace Yakult.Inventory.App.WPF.ConsumableManagement.ViewModels
{
    public class ConsumableModelRowVm : ViewModelBase
    {
        public ConsumableModelDto Model { get; }
        public CartridgeModelDto CartridgeModel { get; }

        public bool IsCartridge => CartridgeModel != null;

        public int ConsumableModelId => IsCartridge ? CartridgeModel.CartridgeModelId : Model.ConsumableModelId;
        public string ModelNumber => IsCartridge ? CartridgeModel.ModelNumber : Model.ModelNumber;
        public string Category => IsCartridge ? "Cartridge" : Model.Category;
        public int AvailableStock => IsCartridge ? CartridgeModel.AvailableStock : Model.AvailableStock;
        public bool IsRequestable => IsCartridge ? CartridgeModel.IsRequestable : Model.IsRequestable;
        public DateTime CreatedAt => IsCartridge ? CartridgeModel.CreatedAt : Model.CreatedAt;
        public string CreatedByName => IsCartridge ? CartridgeModel.CreatedByName : Model.CreatedByName;

        private bool _isSelected;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public ConsumableModelRowVm(ConsumableModelDto model) { Model = model; }
        public ConsumableModelRowVm(CartridgeModelDto cartridgeModel) { CartridgeModel = cartridgeModel; }
    }

    public class ViewConsumableModelsViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private readonly ConsumableModelRepository _repository;
        private readonly CartridgeModelRepository _cartridgeRepository;

        private List<ConsumableModelRowVm> _all = new List<ConsumableModelRowVm>();
        private List<ConsumableModelRowVm> _filtered = new List<ConsumableModelRowVm>();
        private int _currentPage = 1;
        private int _totalPages = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>();
        private string _sortColumn = null;
        private bool _sortAscending = true;

        private bool _isLoading;
        private string _searchText = string.Empty;
        private int _totalModels;
        private int _totalStock;
        private string _pageInfo = "Page 1 of 1  (0 records)";
        private bool _canGoPrev;
        private bool _canGoNext;

        public ObservableCollection<ConsumableModelRowVm> PagedRows { get; }
            = new ObservableCollection<ConsumableModelRowVm>();

        public ObservableCollection<string> CategoryOptions { get; }
            = new ObservableCollection<string> { "All Categories", "Print Head", "Ink", "Toner", "Cartridge" };

        private string _selectedCategory = "All Categories";

        public bool IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
        public int TotalModels { get => _totalModels; set => SetField(ref _totalModels, value); }
        public int TotalStock { get => _totalStock; set => SetField(ref _totalStock, value); }
        public string PageInfo { get => _pageInfo; set => SetField(ref _pageInfo, value); }
        public bool CanGoPrev { get => _canGoPrev; set => SetField(ref _canGoPrev, value); }
        public bool CanGoNext { get => _canGoNext; set => SetField(ref _canGoNext, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterFromTop(); }
        }

        public string SelectedCategory
        {
            get => _selectedCategory;
            set { if (SetField(ref _selectedCategory, value)) ApplyFilterFromTop(); }
        }

        // ── Selection (tracked against _filtered, matching this page's existing
        //    GetSelectedRows()/SelectAll() convention — export-scope, not just the page) ──
        private readonly SelectionTracker<ConsumableModelRowVm> _selection;
        public int SelectedCount => _selection.SelectedCount;
        public bool HasSelection => _selection.HasSelection;

        public void SetRowSelected(int consumableModelId, bool selected) => _selection.SetSelected(_filtered, consumableModelId, selected);
        public void SetPageSelected(bool selected) => _selection.SetManySelected(PagedRows, _filtered, selected);
        public void ClearSelection() => _selection.ClearSelection(_filtered);

        public ViewConsumableModelsViewModel()
        {
            _repository = new ConsumableModelRepository();
            _cartridgeRepository = new CartridgeModelRepository();
            ResetFiltersCommand = new RelayCommand(ResetFilters);

            _selection = new SelectionTracker<ConsumableModelRowVm>(r => r.IsSelected, (r, s) => r.IsSelected = s, r => r.ConsumableModelId);
            _selection.Changed += () =>
            {
                OnPropertyChanged(nameof(SelectedCount));
                OnPropertyChanged(nameof(HasSelection));
            };
        }

        public RelayCommand ResetFiltersCommand { get; }

        /// <summary>Clears every filter control (search, Category, and any per-column
        /// Excel-style filters) back to its default state.</summary>
        private void ResetFilters()
        {
            _searchText = string.Empty;
            OnPropertyChanged(nameof(SearchText));

            _selectedCategory = "All Categories";
            OnPropertyChanged(nameof(SelectedCategory));

            _columnFilters.Clear();
            _sortColumn = null;
            _sortAscending = true;

            ApplyFilterFromTop();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var models = await _repository.GetAllActiveModelsAsync() ?? new List<ConsumableModelDto>();
                var cartridgeModels = await _cartridgeRepository.GetAllActiveModelsAsync() ?? new List<CartridgeModelDto>();

                _all = models.Select(m => new ConsumableModelRowVm(m))
                    .Concat(cartridgeModels.Select(c => new ConsumableModelRowVm(c)))
                    .ToList();
                TotalModels = _all.Count;
                TotalStock = _all.Sum(r => r.AvailableStock);
                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("ViewConsumableModelsViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public async Task ArchiveAsync(ConsumableModelRowVm row, string archivedBy)
        {
            if (row.IsCartridge)
                await _cartridgeRepository.ArchiveAsync(row.ConsumableModelId, reason: null, deactivate: true, archivedBy: archivedBy);
            else
                await _repository.ArchiveAsync(row.ConsumableModelId);
        }

        /// <summary>
        /// Number of Item rows still linked to this model — shown to the user before a
        /// permanent delete so they know how many items will be unlinked (Consumable) or
        /// whether the delete will be blocked outright (Cartridge, see DeleteAsync).
        /// </summary>
        public async Task<int> GetLinkedItemCountAsync(ConsumableModelRowVm row)
        {
            if (row.IsCartridge)
            {
                var deps = await _cartridgeRepository.CheckDependenciesAsync(row.ConsumableModelId);
                return deps.itemCount;
            }
            return await _repository.CheckDependenciesAsync(row.ConsumableModelId);
        }

        /// <summary>
        /// Permanently deletes the model. Cartridge models refuse (throwing
        /// InvalidOperationException) if EmptyCartridge/VendorCartridgeBatchLine rows still
        /// reference them; Consumable models have no such hard-blocking dependency and always
        /// unlink their Item rows before deleting.
        /// </summary>
        public async Task DeleteAsync(ConsumableModelRowVm row)
        {
            if (row.IsCartridge)
                await _cartridgeRepository.DeleteAsync(row.ConsumableModelId);
            else
                await _repository.DeleteAsync(row.ConsumableModelId);
        }

        /// <summary>
        /// For a cartridge-backed row, returns the counts of physical inventory/audit history
        /// (EmptyCartridge, VendorCartridgeBatchLine) that would block a normal DeleteAsync.
        /// Always (0,0) for a plain consumable row — those have no hard-blocking dependency.
        /// </summary>
        public async Task<(int emptyCartridgeCount, int batchLineCount)> GetForceDeleteBlockersAsync(ConsumableModelRowVm row)
        {
            if (!row.IsCartridge) return (0, 0);
            var deps = await _cartridgeRepository.CheckDependenciesAsync(row.ConsumableModelId);
            return (deps.emptyCartridgeCount, deps.batchLineCount);
        }

        /// <summary>
        /// Deletes the model, destroying whatever blocking history DeleteAsync refused to touch
        /// (cartridge models only — see CartridgeModelRepository.ForceDeleteAsync). No-op
        /// distinction for plain consumable rows, which DeleteAsync never blocks anyway.
        /// </summary>
        public async Task ForceDeleteAsync(ConsumableModelRowVm row)
        {
            if (row.IsCartridge)
                await _cartridgeRepository.ForceDeleteAsync(row.ConsumableModelId);
            else
                await _repository.DeleteAsync(row.ConsumableModelId);
        }

        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage() { if (_currentPage > 1) { _currentPage--; RebuildPagedRows(); } }

        public List<ConsumableModelRowVm> GetFilteredForExport() => _filtered.ToList();

        public List<ConsumableModelRowVm> GetSelectedRows() => _filtered.Where(r => r.IsSelected).ToList();

        public void SelectAll(bool select)
        {
            foreach (var row in _filtered) row.IsSelected = select;
        }

        private void ApplyFilterFromTop() { _currentPage = 1; ApplyFilter(); }

        private void ApplyFilter()
        {
            var search = (_searchText ?? string.Empty).Trim().ToLowerInvariant();
            var category = _selectedCategory ?? "All Categories";

            IEnumerable<ConsumableModelRowVm> query = _all;

            if (!string.IsNullOrEmpty(search))
                query = query.Where(r =>
                    (r.ModelNumber ?? "").ToLowerInvariant().Contains(search) ||
                    (r.Category ?? "").ToLowerInvariant().Contains(search));

            // Compare canonical category names: the dropdown says "Print Head" while models are
            // stored as "Printhead" (and older rows may use other spellings).
            if (!string.Equals(category, "All Categories", StringComparison.OrdinalIgnoreCase))
            {
                var wanted = Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(category);
                query = query.Where(r => string.Equals(
                    Yakult.Inventory.App.Models.ConsumableCategories.Canonicalize(r.Category), wanted, StringComparison.OrdinalIgnoreCase));
            }

            _filtered = query.ToList();

            // Apply per-column filters on top of search/category filter
            foreach (var kv in _columnFilters)
            {
                string col = kv.Key;
                var allowed = kv.Value;
                _filtered = _filtered.Where(r => allowed.Contains(GetColumnValue(r, col) ?? "")).ToList();
            }

            IEnumerable<ConsumableModelRowVm> ordered = _filtered;
            if (!string.IsNullOrEmpty(_sortColumn))
            {
                ordered = _sortAscending
                    ? ordered.OrderBy(r => GetColumnValue(r, _sortColumn) ?? "", StringComparer.OrdinalIgnoreCase)
                    : ordered.OrderByDescending(r => GetColumnValue(r, _sortColumn) ?? "", StringComparer.OrdinalIgnoreCase);
            }
            _filtered = ordered.ToList();
            _selection.Sync(_filtered);

            RebuildPagedRows();
        }

        private static string GetColumnValue(ConsumableModelRowVm r, string column)
        {
            switch (column)
            {
                case "ID":              return r.ConsumableModelId.ToString();
                case "Model Number":    return r.ModelNumber;
                case "Category":        return r.Category;
                case "Available Stock": return r.AvailableStock.ToString();
                case "Requestable":     return r.IsRequestable ? "Yes" : "No";
                case "Created At":      return r.CreatedAt.ToString("yyyy-MM-dd");
                case "Created By":      return r.CreatedByName;
                default:                return null;
            }
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                _columnFilters.Remove(column);
            else
                _columnFilters[column] = values;
            _currentPage = 1;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            _currentPage = 1;
            ApplyFilter();
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn = column;
            _sortAscending = ascending;
            _currentPage = 1;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            IEnumerable<string> values = _all.Select(r => GetColumnValue(r, column) ?? "");
            return values.Where(v => !string.IsNullOrEmpty(v))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v)
                         .ToList();
        }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} model{(_filtered.Count == 1 ? "" : "s")})";
        }
    }
}
