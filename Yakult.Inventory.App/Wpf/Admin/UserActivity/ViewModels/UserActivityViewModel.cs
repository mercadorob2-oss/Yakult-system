using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.UserActivity.ViewModels
{
    /// <summary>Wraps a UserId for the User filter combo; UserId == -1 represents "All users".</summary>
    public sealed class UserItem
    {
        public int    UserId { get; }
        public string Name   { get; }
        public UserItem(int userId, string name) { UserId = userId; Name = name; }
        public override string ToString() => Name;
    }

    public class UserActivityViewModel : ViewModelBase
    {
        private readonly UserActivityRepository _repo = new UserActivityRepository();

        private List<UserActivityLogDto> _currentData = new List<UserActivityLogDto>();
        private int _currentPage  = 1;
        private int _totalPages   = 1;
        private int _itemsPerPage = 20;

        public ObservableCollection<UserActivityLogDto> PagedRows { get; } = new ObservableCollection<UserActivityLogDto>();
        public ObservableCollection<UserItem> UserOptions   { get; } = new ObservableCollection<UserItem>();
        public ObservableCollection<string>   ActionOptions { get; } = new ObservableCollection<string>();
        public ObservableCollection<string>   EntityOptions { get; } = new ObservableCollection<string>();

        private bool     _isLoading;
        private DateTime _dateFrom = new DateTime(2026, 1, 20);
        private DateTime _dateTo   = DateTime.Today;
        private UserItem _selectedUser;
        private string   _selectedAction = "All";
        private string   _selectedEntity = "All";
        private bool     _includeAuditTrail;
        private string   _pageInfo = "Page 0 of 0 (0 records)";
        private bool     _canGoPrev;
        private bool     _canGoNext;
        private int      _totalRecords;

        public bool     IsLoading          { get => _isLoading;         set => SetField(ref _isLoading,         value); }
        public DateTime DateFrom           { get => _dateFrom;          set => SetField(ref _dateFrom,          value); }
        public DateTime DateTo             { get => _dateTo;            set => SetField(ref _dateTo,            value); }
        public UserItem SelectedUser       { get => _selectedUser;      set => SetField(ref _selectedUser,      value); }
        public string   SelectedAction     { get => _selectedAction;    set => SetField(ref _selectedAction,    value); }
        public string   SelectedEntity     { get => _selectedEntity;    set => SetField(ref _selectedEntity,    value); }
        public bool     IncludeAuditTrail  { get => _includeAuditTrail; set => SetField(ref _includeAuditTrail, value); }
        public string   PageInfo           { get => _pageInfo;          set => SetField(ref _pageInfo,          value); }
        public bool     CanGoPrev          { get => _canGoPrev;         set => SetField(ref _canGoPrev,         value); }
        public bool     CanGoNext          { get => _canGoNext;         set => SetField(ref _canGoNext,         value); }
        public int      TotalRecords       { get => _totalRecords;      set => SetField(ref _totalRecords,      value); }

        public int ItemsPerPage
        {
            get => _itemsPerPage;
            set
            {
                if (SetField(ref _itemsPerPage, value))
                {
                    _currentPage = 1;
                    RenderCurrentPage();
                }
            }
        }

        public IReadOnlyList<UserActivityLogDto> CurrentData => _currentData;

        public async Task LoadFilterOptionsAsync()
        {
            var users    = await Task.Run(() => _repo.GetDistinctUsers());
            var actions  = await Task.Run(() => _repo.GetDistinctActionTypes());
            var entities = await Task.Run(() => _repo.GetDistinctEntityTypes());

            UserOptions.Clear();
            UserOptions.Add(new UserItem(-1, "All users"));
            foreach (var u in users)
                UserOptions.Add(new UserItem(u.UserId, u.Name));
            SelectedUser = UserOptions[0];

            ActionOptions.Clear();
            ActionOptions.Add("All");
            foreach (var a in actions)
                ActionOptions.Add(a);
            SelectedAction = "All";

            EntityOptions.Clear();
            EntityOptions.Add("All");
            foreach (var en in entities)
                EntityOptions.Add(en);
            SelectedEntity = "All";
        }

        public UserActivityFilter BuildFilter()
        {
            return new UserActivityFilter
            {
                DateFrom          = DateFrom.Date,
                DateTo            = DateTo.Date,
                UserId            = SelectedUser != null && SelectedUser.UserId >= 0 ? SelectedUser.UserId : (int?)null,
                ActionType        = string.Equals(SelectedAction, "All", StringComparison.OrdinalIgnoreCase) ? null : SelectedAction,
                EntityType        = string.Equals(SelectedEntity, "All", StringComparison.OrdinalIgnoreCase) ? null : SelectedEntity,
                IncludeAuditTrail = IncludeAuditTrail
            };
        }

        public void ClearFilters()
        {
            DateFrom = new DateTime(2026, 1, 20);
            DateTo   = DateTime.Today;
            SelectedUser       = UserOptions.FirstOrDefault();
            SelectedAction     = "All";
            SelectedEntity     = "All";
            IncludeAuditTrail  = false;
            _currentPage = 1;
        }

        public async Task LoadDataAsync()
        {
            IsLoading = true;
            try
            {
                var filter = BuildFilter();
                var data = await Task.Run(() => _repo.GetFiltered(filter));
                _currentData = data;
                TotalRecords = data.Count;
                _currentPage = 1;
                RenderCurrentPage();

                ActivityLogger.LogAsync(
                    ActivityLogger.Actions.View,
                    "UserActivityLog",
                    null,
                    $"Admin viewed activity log ({data.Count} records)");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void RenderCurrentPage()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_currentData.Count / (double)_itemsPerPage));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _currentData.Skip((_currentPage - 1) * _itemsPerPage).Take(_itemsPerPage))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = _currentData.Count == 0
                ? "Page 0 of 0 (0 records)"
                : $"Page {_currentPage} of {_totalPages} ({_currentData.Count} records)";
        }

        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RenderCurrentPage(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)            { _currentPage--;              RenderCurrentPage(); } }
        public void GoToNextPage()  { if (_currentPage < _totalPages)  { _currentPage++;              RenderCurrentPage(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages;  RenderCurrentPage(); } }
    }
}
