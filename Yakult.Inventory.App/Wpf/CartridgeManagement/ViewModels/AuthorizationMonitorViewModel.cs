using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels
{
    public class AuthorizationMonitorRowVm
    {
        private readonly CartridgeAuthorizationModel _raw;

        public AuthorizationMonitorRowVm(CartridgeAuthorizationModel raw)
        {
            _raw = raw;
        }

        public int    AuthorizationId  => _raw.AuthorizationId;
        public string EmployeeName     => _raw.EmployeeName     ?? $"EmpId {_raw.EmployeeId}";
        public string EmployeePosition => _raw.EmployeePosition ?? "—";
        public string DepartmentName   => _raw.DepartmentName   ?? $"DeptId {_raw.DepartmentId}";
        public string BranchName       => _raw.BranchName       ?? "—";
        public string CompanyName      => _raw.CompanyName      ?? "—";
        public string RequestedModels  => FormatRequestedModels(_raw.RequestedModels);
        public string Status           => _raw.Status           ?? "—";
        public string SignedByName     => _raw.SignedByName     ?? "—";
        public string SignedDate       => _raw.SignedDate.HasValue
                                             ? _raw.SignedDate.Value.ToString("yyyy-MM-dd HH:mm")
                                             : "—";
        public string CreatedDate      => _raw.CreatedDate.ToString("yyyy-MM-dd HH:mm");

        private static string FormatRequestedModels(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "—";
            if (!raw.TrimStart().StartsWith("[")) return raw;

            try
            {
                var items = JsonSerializer.Deserialize<List<JsonElement>>(raw);
                if (items == null || items.Count == 0) return raw;

                var parts = items.Select(e =>
                {
                    string model = e.TryGetProperty("model", out var m) ? m.GetString() ?? "" : "";
                    int qty = e.TryGetProperty("qty", out var q) ? q.GetInt32() : 0;
                    return qty > 0 ? $"{model} (x{qty})" : model;
                }).Where(s => !string.IsNullOrWhiteSpace(s));

                return string.Join(", ", parts);
            }
            catch
            {
                return raw;
            }
        }
    }

    public class AuthorizationMonitorViewModel : ViewModelBase
    {
        private const int PageSize = 20;

        private List<AuthorizationMonitorRowVm> _all      = new List<AuthorizationMonitorRowVm>();
        private List<AuthorizationMonitorRowVm> _filtered = new List<AuthorizationMonitorRowVm>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private bool   _isLoading;
        private string _searchText    = string.Empty;
        private int    _totalRecords;
        private int    _pendingCount;
        private int    _approvedCount;
        private int    _rejectedCount;
        private string _pageInfo = "Page 1 of 1  (0 records)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<AuthorizationMonitorRowVm> PagedRows { get; }
            = new ObservableCollection<AuthorizationMonitorRowVm>();

        public bool   IsLoading     { get => _isLoading;     set => SetField(ref _isLoading,     value); }
        public int    TotalRecords  { get => _totalRecords;  set => SetField(ref _totalRecords,  value); }
        public int    PendingCount  { get => _pendingCount;  set => SetField(ref _pendingCount,  value); }
        public int    ApprovedCount { get => _approvedCount; set => SetField(ref _approvedCount, value); }
        public int    RejectedCount { get => _rejectedCount; set => SetField(ref _rejectedCount, value); }
        public string PageInfo      { get => _pageInfo;      set => SetField(ref _pageInfo,      value); }
        public bool   CanGoPrev     { get => _canGoPrev;     set => SetField(ref _canGoPrev,     value); }
        public bool   CanGoNext     { get => _canGoNext;     set => SetField(ref _canGoNext,     value); }

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
                var repo = new CartridgeAuthorizationRepository();
                var raw  = await Task.Run(() => repo.GetAllAuthorizationsAsync())
                           ?? new List<CartridgeAuthorizationModel>();

                _all = raw.OrderByDescending(x => x.CreatedDate)
                          .Select(x => new AuthorizationMonitorRowVm(x))
                          .ToList();

                TotalRecords  = _all.Count;
                PendingCount  = raw.Count(x => x.Status == "Pending");
                ApprovedCount = raw.Count(x => x.Status == "Approved");
                RejectedCount = raw.Count(x => x.Status == "Rejected");

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
            string q   = (_searchText ?? string.Empty).Trim();
            bool   hasQ = !string.IsNullOrWhiteSpace(q);

            _filtered = hasQ
                ? _all.Where(a =>
                    Contains(a.EmployeeName,    q) || Contains(a.EmployeePosition, q) ||
                    Contains(a.DepartmentName,  q) || Contains(a.BranchName,       q) ||
                    Contains(a.CompanyName,     q) || Contains(a.Status,           q) ||
                    Contains(a.SignedByName,    q) || Contains(a.RequestedModels,  q)).ToList()
                : new List<AuthorizationMonitorRowVm>(_all);

            _totalPages = Math.Max(1, (int)Math.Ceiling((double)_filtered.Count / PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;

            RebuildPagedRows();
        }

        private void RebuildPagedRows()
        {
            PagedRows.Clear();
            foreach (var row in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(row);

            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} records)";
            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
        }

        public void GoToPrevPage() { if (_currentPage > 1)          { _currentPage--; RebuildPagedRows(); } }
        public void GoToNextPage() { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }

        private static bool Contains(string source, string q) =>
            source != null && source.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
