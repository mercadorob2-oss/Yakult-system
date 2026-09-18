using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class FulfilledCartridgesViewModel : ViewModelBase
    {
        private const int PageSize = 25;

        private readonly CartridgeManagementRepository _repo = new CartridgeManagementRepository();

        private List<FulfilledCartridgeRowDto> _all      = new List<FulfilledCartridgeRowDto>();
        private List<FulfilledCartridgeRowDto> _filtered = new List<FulfilledCartridgeRowDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText  = string.Empty;
        private int    _totalRecords;
        private string _pageInfo    = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        private FulfilledCartridgeRowDto _selectedRow;

        public ObservableCollection<FulfilledCartridgeRowDto> PagedRows { get; }
            = new ObservableCollection<FulfilledCartridgeRowDto>();

        public Action<FulfilledCartridgeRowDto> ReprintRequested { get; set; }

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public string SearchText   { get => _searchText;   set { if (SetField(ref _searchText,   value)) ApplyFilterFromTop(); } }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public FulfilledCartridgeRowDto SelectedRow
        {
            get => _selectedRow;
            set
            {
                if (SetField(ref _selectedRow, value))
                    OnPropertyChanged(nameof(CanReprint));
            }
        }

        public bool CanReprint => _selectedRow != null;

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await Task.Run(() => _repo.GetFulfilledCartridgeHistory());
                TotalRecords = _all.Count;
                _currentPage = 1;
                ApplyFilter();
            }
            catch (Exception ex)
            {
                Logger.LogError("FulfilledCartridgesViewModel.LoadAsync failed", ex);
                throw;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void Reprint()
        {
            if (_selectedRow == null) return;
            ReprintRequested?.Invoke(_selectedRow);
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

            _filtered = string.IsNullOrEmpty(search)
                ? new List<FulfilledCartridgeRowDto>(_all)
                : _all.Where(r =>
                    r.SetCode         .SafeContains(search) ||
                    r.RequesterName   .SafeContains(search) ||
                    r.CompanyName     .SafeContains(search) ||
                    r.BranchName      .SafeContains(search) ||
                    r.DepartmentName  .SafeContains(search) ||
                    r.CartridgeModel  .SafeContains(search) ||
                    r.ReceivedByName  .SafeContains(search) ||
                    r.ReqId.ToString().Contains(search)
                ).ToList();

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

    internal static class StringExtensions
    {
        public static bool SafeContains(this string source, string value) =>
            source != null && source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
