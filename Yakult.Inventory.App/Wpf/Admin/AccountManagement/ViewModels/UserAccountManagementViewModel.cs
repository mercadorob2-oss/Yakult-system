using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels
{
    public class UserAccountManagementViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<LegacyUserAccountDto> _all      = new List<LegacyUserAccountDto>();
        private List<LegacyUserAccountDto> _filtered = new List<LegacyUserAccountDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText   = string.Empty;
        private string _filterOption = "All";
        private int    _totalRecords;
        private int    _activeCount;
        private string _pageInfo = "Page 1 of 1  (0 users)";
        private bool   _canGoPrev;
        private bool   _canGoNext;

        public ObservableCollection<LegacyUserAccountDto> PagedRows { get; }
            = new ObservableCollection<LegacyUserAccountDto>();

        public List<LegacyUserAccountDto> AllRows => _all;

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public int    ActiveCount  { get => _activeCount;  set => SetField(ref _activeCount,  value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev    { get => _canGoPrev;    set => SetField(ref _canGoPrev,    value); }
        public bool   CanGoNext    { get => _canGoNext;    set => SetField(ref _canGoNext,    value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public string FilterOption
        {
            get => _filterOption;
            set { if (SetField(ref _filterOption, value)) ApplyFilter(); }
        }

        public UserAccountManagementViewModel()
        {
            _cs = DatabaseConfig.ConnectionString;
        }

        public async Task LoadAsync()
        {
            _columnFilters.Clear();
            _sortColumn    = null;
            _sortAscending = true;
            IsLoading = true;
            try
            {
                _all = new List<LegacyUserAccountDto>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT
                            u.UserId,
                            u.Name AS Username,
                            u.EmailAddress,
                            u.IsActive,
                            u.IsDeveloper,
                            u.LastLoginDate,
                            u.MustChangePassword,
                            u.EmpId,
                            e.Name AS EmployeeName,
                            e.EmployeeNumber,
                            STUFF((
                                SELECT ', ' + r.RoleName
                                FROM UserRole ur
                                INNER JOIN Role r ON ur.RoleId = r.RoleId
                                WHERE ur.UserId = u.UserId AND r.IsActive = 1
                                FOR XML PATH(''), TYPE
                            ).value('.', 'NVARCHAR(MAX)'), 1, 2, '') AS Roles
                        FROM [User] u
                        LEFT JOIN Employee e ON u.EmpId = e.EmpId
                        ORDER BY u.Name";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _all.Add(new LegacyUserAccountDto
                            {
                                UserId             = reader.GetInt32(0),
                                Username           = reader.GetString(1),
                                Email              = reader.IsDBNull(2)  ? "" : reader.GetString(2),
                                IsActive           = reader.GetBoolean(3),
                                IsDeveloper        = reader.GetBoolean(4),
                                LastLoginDate      = reader.IsDBNull(5)  ? (DateTime?)null : reader.GetDateTime(5),
                                MustChangePassword = reader.GetBoolean(6),
                                EmpId              = reader.IsDBNull(7)  ? (int?)null : reader.GetInt32(7),
                                EmployeeName       = reader.IsDBNull(8)  ? "" : reader.GetString(8),
                                EmployeeNumber     = reader.IsDBNull(9)  ? "" : reader.GetString(9),
                                Roles              = reader.IsDBNull(10) ? "" : reader.GetString(10)
                            });
                        }
                    }
                }

                TotalRecords = _all.Count;
                ActiveCount  = _all.Count(u => u.IsActive);
                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ApplyFilter()
        {
            var search = (_searchText ?? "").Trim().ToLowerInvariant();
            var filter = _filterOption ?? "All";

            _filtered = _all.Where(user =>
            {
                if (!string.IsNullOrEmpty(search))
                {
                    bool matches =
                        (user.Username     ?? "").ToLowerInvariant().Contains(search) ||
                        (user.Email        ?? "").ToLowerInvariant().Contains(search) ||
                        (user.EmployeeName ?? "").ToLowerInvariant().Contains(search) ||
                        (user.Roles        ?? "").ToLowerInvariant().Contains(search);
                    if (!matches) return false;
                }

                if (filter == "Active Only"   && !user.IsActive) return false;
                if (filter == "Inactive Only" &&  user.IsActive) return false;

                return true;
            }).ToList();

            foreach (var kv in _columnFilters)
            {
                string col     = kv.Key;
                var    allowed = kv.Value;
                _filtered = _filtered.Where(u => allowed.Contains(GetUserColumnValue(u, col) ?? "")).ToList();
            }

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                _filtered = _sortAscending
                    ? _filtered.OrderBy(u => GetUserColumnValue(u, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase).ToList()
                    : _filtered.OrderByDescending(u => GetUserColumnValue(u, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase).ToList();
            }

            RebuildPagedRows();
        }

        private static string GetUserColumnValue(LegacyUserAccountDto user, string column)
        {
            switch (column)
            {
                case "Username": return user.Username ?? "";
                case "Email":    return user.Email ?? "";
                case "Employee": return user.EmployeeName ?? "";
                case "Roles":    return user.Roles ?? "";
                case "Status":   return user.IsActive ? "Active" : "Inactive";
                default:         return null;
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
            _sortColumn    = column;
            _sortAscending = ascending;
            _currentPage   = 1;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            IEnumerable<string> values;
            switch (column)
            {
                case "Username": values = _all.Select(u => u.Username ?? ""); break;
                case "Email":    values = _all.Select(u => u.Email ?? ""); break;
                case "Employee": values = _all.Select(u => u.EmployeeName ?? ""); break;
                case "Roles":    values = _all.Select(u => u.Roles ?? ""); break;
                case "Status":   values = _all.Select(u => u.IsActive ? "Active" : "Inactive"); break;
                default: return new List<string>();
            }
            return values.Where(v => !string.IsNullOrEmpty(v))
                         .Distinct(System.StringComparer.OrdinalIgnoreCase)
                         .OrderBy(v => v)
                         .ToList();
        }

        public void GoToNextPage()  { if (_currentPage < _totalPages) { _currentPage++; RebuildPagedRows(); } }
        public void GoToPrevPage()  { if (_currentPage > 1)           { _currentPage--; RebuildPagedRows(); } }
        public void GoToFirstPage() { if (_currentPage != 1)           { _currentPage = 1;           RebuildPagedRows(); } }
        public void GoToLastPage()  { if (_currentPage != _totalPages) { _currentPage = _totalPages; RebuildPagedRows(); } }

        private void RebuildPagedRows()
        {
            _totalPages = Math.Max(1, (int)Math.Ceiling(_filtered.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1)           _currentPage = 1;

            PagedRows.Clear();
            foreach (var r in _filtered.Skip((_currentPage - 1) * PageSize).Take(PageSize))
                PagedRows.Add(r);

            CanGoPrev = _currentPage > 1;
            CanGoNext = _currentPage < _totalPages;
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} user{(_filtered.Count == 1 ? "" : "s")})";
        }

        public static string GetStatusText(LegacyUserAccountDto user)
        {
            if (!user.IsActive)           return "Inactive";
            if (user.IsDeveloper)         return "Developer";
            if (user.MustChangePassword)  return "Reset Req'd";
            return "Active";
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        using (var cmd = new SqlCommand("DELETE FROM UserRole WHERE UserId = @Id", con, tx))
                        { cmd.Parameters.AddWithValue("@Id", userId); await cmd.ExecuteNonQueryAsync(); }

                        using (var cmd = new SqlCommand("DELETE FROM [User] WHERE UserId = @Id", con, tx))
                        { cmd.Parameters.AddWithValue("@Id", userId); await cmd.ExecuteNonQueryAsync(); }

                        tx.Commit();
                        return true;
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }
        }

        public async Task ResetPasswordAsync(int userId, string tempPassword)
        {
            const string sql = @"
                UPDATE [User]
                SET [Password] = CONVERT(VARBINARY(MAX), @Password),
                    MustChangePassword = 1,
                    IsTemporaryPassword = 1
                WHERE UserId = @UserId";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Password", tempPassword);
                    cmd.Parameters.AddWithValue("@UserId",   userId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}
