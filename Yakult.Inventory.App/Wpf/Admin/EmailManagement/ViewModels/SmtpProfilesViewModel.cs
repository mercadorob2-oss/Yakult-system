using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.EmailManagement.ViewModels
{
    public class SmtpProfilesViewModel : ViewModelBase
    {
        private const int PageSize = 20;
        private readonly EmailRepository _repo;

        private List<SystemSmtpProfileDto> _all = new List<SystemSmtpProfileDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _pageInfo = "Page 0 of 0 (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<SystemSmtpProfileDto> PagedRows { get; } = new ObservableCollection<SystemSmtpProfileDto>();
        public List<SystemSmtpProfileDto> AllRows => _all;

        public bool   IsLoading { get => _isLoading; set => SetField(ref _isLoading, value); }
        public string PageInfo  { get => _pageInfo;  set => SetField(ref _pageInfo,  value); }
        public bool   CanGoPrev { get => _canGoPrev; set => SetField(ref _canGoPrev, value); }
        public bool   CanGoNext { get => _canGoNext; set => SetField(ref _canGoNext, value); }

        public SmtpProfilesViewModel(EmailRepository repo)
        {
            _repo = repo;
        }

        public async Task LoadAsync()
        {
            IsLoading = true;
            try
            {
                _all = await _repo.GetSmtpProfilesAsync(activeOnly: false);
                _currentPage = 1;
                RebuildPagedRows();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void GoToNextPage()  { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }
        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPagedRows(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPagedRows(); } }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_all.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _all.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = _all.Count == 0
                ? "Page 0 of 0 (0 records)"
                : $"Page {_currentPage} of {_totalPages} ({_all.Count} records)";
        }

        public Task<SystemSmtpProfileDto> GetByIdAsync(int profileId) => _repo.GetSmtpProfileByIdAsync(profileId);

        public Task SaveAsync(SystemSmtpProfileDto profile) => _repo.SaveSmtpProfileAsync(profile);
    }
}
