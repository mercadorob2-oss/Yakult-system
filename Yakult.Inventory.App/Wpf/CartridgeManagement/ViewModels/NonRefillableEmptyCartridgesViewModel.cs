using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class NonRefillableEmptyCartridgesViewModel : ViewModelBase
    {
        private const int PageSize = 20;
        private readonly CartridgeRefillService _service;

        private List<NonRefillableGroupedDto> _all      = new List<NonRefillableGroupedDto>();
        private List<NonRefillableGroupedDto> _filtered = new List<NonRefillableGroupedDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText      = string.Empty;
        private string _conditionFilter = "All";
        private int    _totalGroups;
        private int    _totalUnits;
        private int    _goodQty;
        private int    _damagedQty;
        private string _pageInfo        = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<NonRefillableGroupedDto> PagedRows { get; }
            = new ObservableCollection<NonRefillableGroupedDto>();

        public NonRefillableEmptyCartridgesViewModel()
        {
            _service = new CartridgeRefillService();
        }

        public bool   IsLoading       { get => _isLoading;       set => SetField(ref _isLoading,       value); }
        public string SearchText      { get => _searchText;      set { if (SetField(ref _searchText,      value)) ApplyFilterFromTop(); } }
        public string ConditionFilter { get => _conditionFilter; set { if (SetField(ref _conditionFilter, value)) ApplyFilterFromTop(); } }
        public int    TotalGroups     { get => _totalGroups;     set => SetField(ref _totalGroups,     value); }
        public int    TotalUnits      { get => _totalUnits;      set => SetField(ref _totalUnits,      value); }
        public int    GoodQty         { get => _goodQty;         set => SetField(ref _goodQty,         value); }
        public int    DamagedQty      { get => _damagedQty;      set => SetField(ref _damagedQty,      value); }
        public string PageInfo        { get => _pageInfo;        set => SetField(ref _pageInfo,        value); }
        public bool   CanGoPrev       { get => _canGoPrev;       set => SetField(ref _canGoPrev,       value); }
        public bool   CanGoNext       { get => _canGoNext;       set => SetField(ref _canGoNext,       value); }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await _service.GetNonRefillableGroupedAsync();

                TotalGroups = _all.Select(r => (r.ReqId, r.CartridgeModelId)).Distinct().Count();
                TotalUnits  = _all.Sum(r => r.TotalQuantity);
                GoodQty     = _all.Where(r => r.ConditionType != "DAMAGED").Sum(r => r.TotalQuantity);
                DamagedQty  = _all.Where(r => r.ConditionType == "DAMAGED").Sum(r => r.TotalQuantity);

                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("NonRefillableEmptyCartridgesViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage() { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }

        private void ApplyFilterFromTop()
        {
            _currentPage = 1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            var search = _searchText?.Trim().ToLowerInvariant() ?? string.Empty;

            _filtered = _all.Where(r =>
            {
                if (_conditionFilter == "Good"    && r.ConditionType == "DAMAGED") return false;
                if (_conditionFilter == "Damaged" && r.ConditionType != "DAMAGED") return false;

                if (!string.IsNullOrEmpty(search))
                {
                    bool matchReqId = r.ReqId.HasValue && r.ReqId.Value.ToString().Contains(search);
                    bool matchModel = (r.CartridgeModel ?? string.Empty).ToLowerInvariant().Contains(search);
                    if (!matchReqId && !matchModel) return false;
                }

                return true;
            }).ToList();

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} record{(_filtered.Count == 1 ? "" : "s")})";
        }
    }
}
