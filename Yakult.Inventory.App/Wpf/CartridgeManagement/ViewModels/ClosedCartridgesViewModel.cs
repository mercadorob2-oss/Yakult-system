using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class ClosedCartridgesViewModel : ViewModelBase
    {
        private const int PageSize = 20;
        private readonly CartridgeRefillService _service;
        private readonly string _status;

        private List<ClosedEmptyCartridgeDto> _all      = new List<ClosedEmptyCartridgeDto>();
        private List<ClosedEmptyCartridgeDto> _filtered = new List<ClosedEmptyCartridgeDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText  = string.Empty;
        private int    _totalRecords;
        private int    _totalUnits;
        private string _pageInfo    = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<ClosedEmptyCartridgeDto> PagedRows { get; }
            = new ObservableCollection<ClosedEmptyCartridgeDto>();

        public string PageTitle      { get; }
        public string PageSubtitle   { get; }
        public string ClosedAtHeader { get; }

        public ClosedCartridgesViewModel(string status)
        {
            _status       = status;
            _service      = new CartridgeRefillService();
            PageTitle     = status == "Disposed" ? "Disposed Empty Cartridges" : "Sold Empty Cartridges";
            PageSubtitle  = status == "Disposed"
                ? "Non-refillable cartridges disposed of by IT  •  Read-only audit view"
                : "Non-refillable cartridges sold by IT  •  Read-only audit view";
            ClosedAtHeader = status == "Disposed" ? "Disposed At" : "Sold At";
        }

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public string SearchText   { get => _searchText;   set { if (SetField(ref _searchText,   value)) ApplyFilterFromTop(); } }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public int    TotalUnits   { get => _totalUnits;   set => SetField(ref _totalUnits,   value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = _status == "Disposed"
                    ? await _service.GetDisposedEmptiesAsync()
                    : await _service.GetSoldEmptiesAsync();

                TotalRecords = _all.Count;
                TotalUnits   = _all.Sum(r => r.Quantity);

                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError($"ClosedCartridgesViewModel({_status}).LoadAsync failed", ex);
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
