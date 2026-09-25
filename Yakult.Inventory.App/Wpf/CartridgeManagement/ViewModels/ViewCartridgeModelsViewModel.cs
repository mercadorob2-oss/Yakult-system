using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class CartridgeModelRowVm : ViewModelBase
    {
        private bool _isSelected;

        public CartridgeModelDto Model { get; }

        public bool     IsSelected      { get => _isSelected; set => SetField(ref _isSelected, value); }
        public int      CartridgeModelId => Model.CartridgeModelId;
        public string   ModelNumber      => Model.ModelNumber;
        public bool     IsRequestable    => Model.IsRequestable;
        public bool     IsRefillable     => Model.IsRefillable;
        public int      BrandNewStock    => Model.BrandNewStock;
        public int      RefilledStock    => Model.RefilledStock;
        /// <summary>Issuable units on hand (Brand New + Refilled), as the Cartridge Exchange counts them.</summary>
        public int      ItemCount        => Model.BrandNewStock + Model.RefilledStock;
        public DateTime CreatedAt        => Model.CreatedAt;
        public string   CreatedByName    => Model.CreatedByName;

        public CartridgeModelRowVm(CartridgeModelDto model) { Model = model; }
    }

    public class ViewCartridgeModelsViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private readonly CartridgeModelRepository _repository;

        private List<CartridgeModelRowVm> _all      = new List<CartridgeModelRowVm>();
        private List<CartridgeModelRowVm> _filtered = new List<CartridgeModelRowVm>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText  = string.Empty;
        private int    _totalModels;
        private int    _requestable;
        private string _pageInfo = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<CartridgeModelRowVm> PagedRows { get; }
            = new ObservableCollection<CartridgeModelRowVm>();

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public int    TotalModels  { get => _totalModels;  set => SetField(ref _totalModels,  value); }
        public int    Requestable  { get => _requestable;  set => SetField(ref _requestable,  value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterFromTop(); }
        }

        public bool SelectAll
        {
            get => PagedRows.Count > 0 && PagedRows.All(r => r.IsSelected);
            set
            {
                foreach (var row in PagedRows)
                    row.IsSelected = value;
                OnPropertyChanged(nameof(SelectAll));
            }
        }

        public List<CartridgeModelRowVm> GetCheckedRows()
            => PagedRows.Where(r => r.IsSelected).ToList();

        public ViewCartridgeModelsViewModel()
        {
            _repository = new CartridgeModelRepository();
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                var models = await _repository.GetAllActiveModelsAsync() ?? new List<CartridgeModelDto>();
                _all         = models.Select(m => new CartridgeModelRowVm(m)).ToList();
                TotalModels  = _all.Count;
                Requestable  = _all.Count(r => r.IsRequestable);
                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("ViewCartridgeModelsViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage() { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }

        private void ApplyFilterFromTop() { _currentPage = 1; ApplyFilter(); }

        private void ApplyFilter()
        {
            var search = (_searchText ?? string.Empty).Trim().ToLowerInvariant();

            _filtered = string.IsNullOrEmpty(search)
                ? _all.ToList()
                : _all.Where(r =>
                    (r.ModelNumber ?? "").ToLowerInvariant().Contains(search)
                ).ToList();

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            UnsubscribeRows();
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
            {
                row.PropertyChanged += OnRowPropertyChanged;
                PagedRows.Add(row);
            }

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} model{(_filtered.Count == 1 ? "" : "s")})";
            OnPropertyChanged(nameof(SelectAll));
        }

        private void UnsubscribeRows()
        {
            foreach (var row in PagedRows)
                row.PropertyChanged -= OnRowPropertyChanged;
        }

        private void OnRowPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CartridgeModelRowVm.IsSelected))
                OnPropertyChanged(nameof(SelectAll));
        }
    }
}
