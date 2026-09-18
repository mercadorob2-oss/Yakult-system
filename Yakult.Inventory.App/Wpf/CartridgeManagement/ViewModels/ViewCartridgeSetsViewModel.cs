using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class ViewCartridgeSetsViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private readonly SetRepository _repository = new SetRepository();

        private List<SetDto> _all      = new List<SetDto>();
        private List<SetDto> _filtered = new List<SetDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool    _isLoading;
        private string  _searchText = string.Empty;
        private int     _totalSets;
        private string  _pageInfo = "Page 1 of 1  (0 sets)";
        private bool    _canGoPrev;
        private bool    _canGoNext;
        private SetDto  _selectedSet;

        public ObservableCollection<SetDto> PagedRows { get; }
            = new ObservableCollection<SetDto>();

        public bool   IsLoading   { get => _isLoading;   set => SetField(ref _isLoading,   value); }
        public int    TotalSets   { get => _totalSets;   set => SetField(ref _totalSets,   value); }
        public string PageInfo    { get => _pageInfo;    set => SetField(ref _pageInfo,    value); }
        public bool   CanGoPrev   { get => _canGoPrev;   set => SetField(ref _canGoPrev,   value); }
        public bool   CanGoNext   { get => _canGoNext;   set => SetField(ref _canGoNext,   value); }
        public SetDto SelectedSet { get => _selectedSet; set => SetField(ref _selectedSet, value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilterFromTop(); }
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await _repository.GetCartridgeSetsAsync() ?? new List<SetDto>();
                TotalSets = _all.Count;
                ApplyFilterFromTop();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ApplyFilterFromTop()
        {
            _currentPage = 1;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            string q    = (_searchText ?? string.Empty).Trim().ToLowerInvariant();
            bool   hasQ = !string.IsNullOrWhiteSpace(q);

            _filtered = hasQ
                ? _all.Where(s =>
                    Contains(s.SetCode,              q) || Contains(s.SetType,              q) ||
                    Contains(s.Status,               q) || Contains(s.CreatedByName,        q) ||
                    Contains(s.CurrentEmployeeName,  q) || Contains(s.CurrentCompanyName,   q) ||
                    Contains(s.CurrentBranchName,    q) || Contains(s.CurrentDepartmentName,q) ||
                    Contains(s.Remarks,              q) || Contains(s.DocumentNumber,       q) ||
                    Contains(s.ReferenceNumber,      q)).ToList()
                : new List<SetDto>(_all);

            _totalPages = Math.Max(1, (int)Math.Ceiling((double)_filtered.Count / PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} sets)";
            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
        }

        public void GoToPrevPage() { if (_currentPage > 1)          { _currentPage--; RebuildPagedRows(); } }
        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }

        private static bool Contains(string source, string q) =>
            source != null && source.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
