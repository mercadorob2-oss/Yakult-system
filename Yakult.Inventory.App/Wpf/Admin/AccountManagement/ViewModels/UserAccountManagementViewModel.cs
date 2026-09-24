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

        // Replaces userNNN@yakult.local placeholder emails with the linked employee's email:
        // Personal Email first, then Branch Email. Only placeholders are touched, and because
        // dbo.User.EmailAddress is unique an address already held by another account is skipped
        // (branch emails are shared, so only one account per branch can take it).
        private const string SyncPlaceholderEmailsSql = @"
            SET NOCOUNT ON;
            IF OBJECT_ID('dbo.EmployeeEmail') IS NULL OR OBJECT_ID('dbo.EmailAddress') IS NULL RETURN;

            SELECT u.UserId, u.EmpId, e.ComId, e.BranchId, e.DeptId
            INTO #T
            FROM dbo.[User] u
            INNER JOIN dbo.Employee e ON e.EmpId = u.EmpId
            WHERE u.EmailAddress LIKE 'user%@yakult.local'
              AND REPLACE(REPLACE(u.EmailAddress, 'user', ''), '@yakult.local', '') <> ''
              AND REPLACE(REPLACE(u.EmailAddress, 'user', ''), '@yakult.local', '') NOT LIKE '%[^0-9]%';

            IF NOT EXISTS (SELECT 1 FROM #T) RETURN;

            CREATE TABLE #Plan (UserId INT NOT NULL PRIMARY KEY, NewEmail NVARCHAR(255) NOT NULL);

            INSERT INTO #Plan (UserId, NewEmail)
            SELECT x.UserId, x.EmailAddress
            FROM (
                SELECT t.UserId, ea.EmailAddress,
                       ROW_NUMBER() OVER (PARTITION BY LOWER(ea.EmailAddress) ORDER BY t.UserId) AS pick
                FROM #T t
                CROSS APPLY (SELECT TOP 1 ee.EmailId FROM dbo.EmployeeEmail ee
                             WHERE ee.EmpId = t.EmpId AND ee.IsPrimary = 1 AND ee.IsActive = 1
                             ORDER BY ee.EmployeeEmailId) pe
                INNER JOIN dbo.EmailAddress ea ON ea.EmailId = pe.EmailId AND ea.IsActive = 1
                WHERE NOT EXISTS (SELECT 1 FROM dbo.[User] o WHERE o.EmailAddress = ea.EmailAddress AND o.UserId <> t.UserId)
            ) x WHERE x.pick = 1;

            INSERT INTO #Plan (UserId, NewEmail)
            SELECT x.UserId, x.EmailAddress
            FROM (
                SELECT t.UserId, ba.EmailAddress,
                       ROW_NUMBER() OVER (PARTITION BY LOWER(ba.EmailAddress) ORDER BY t.UserId) AS pick
                FROM #T t
                INNER JOIN dbo.Company    c ON c.ComId    = t.ComId
                INNER JOIN dbo.Branch     b ON b.BranchId = t.BranchId
                INNER JOIN dbo.Department d ON d.DeptId   = t.DeptId
                INNER JOIN dbo.DepartmentAccount da
                        ON da.CompanyName = c.Name AND da.DepartmentName = d.Name AND da.BranchName = b.Name
                INNER JOIN dbo.EmailAddress ba ON ba.EmailId = da.EmailAddressId
                WHERE NOT EXISTS (SELECT 1 FROM #Plan p WHERE p.UserId = t.UserId)
                  AND NOT EXISTS (SELECT 1 FROM #Plan p WHERE p.NewEmail = ba.EmailAddress)
                  AND NOT EXISTS (SELECT 1 FROM dbo.[User] o WHERE o.EmailAddress = ba.EmailAddress AND o.UserId <> t.UserId)
            ) x WHERE x.pick = 1;

            UPDATE u SET u.EmailAddress = p.NewEmail
            FROM dbo.[User] u
            INNER JOIN #Plan p ON p.UserId = u.UserId
            WHERE u.EmailAddress LIKE 'user%@yakult.local';";

        // Accounts with NO role yet get one from the linked employee's position: Manager-family
        // positions -> Manager, SUPERVISOR -> Supervisor. IT Manager / IT Supervisor are IT
        // Department roles and are never assigned from a position; coordinators get none.
        // Accounts that already hold any role are never touched.
        private static string BuildSyncRolesSql(bool hasApprovalRoleTitle)
        {
            string artSelect = hasApprovalRoleTitle ? "art.ApprovalRole" : "CAST(NULL AS NVARCHAR(50))";
            string artJoin   = hasApprovalRoleTitle
                ? @"LEFT JOIN dbo.ApprovalRoleTitle art
                           ON UPPER(LTRIM(RTRIM(art.PositionTitle))) = UPPER(LTRIM(RTRIM(e.Position))) AND art.IsActive = 1"
                : "";

            return $@"
                SET NOCOUNT ON;
                DECLARE @Manager    INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Manager'    AND IsActive = 1);
                DECLARE @Supervisor INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Supervisor' AND IsActive = 1);

                INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned)
                SELECT x.UserId, CASE x.NewRole WHEN 'Manager' THEN @Manager ELSE @Supervisor END, GETDATE()
                FROM (
                    SELECT u.UserId,
                           CASE
                               WHEN {artSelect} IN ('Manager', 'Supervisor') THEN {artSelect}
                               WHEN {artSelect} IS NULL AND UPPER(LTRIM(RTRIM(e.Position))) IN
                                    ('MANAGER', 'ASST. MANAGER', 'ASSISTANT MANAGER', 'JR. ASST. MANAGER',
                                     'JUNIOR ASSISTANT MANAGER', 'ACTING JR. ASST. MANAGER') THEN 'Manager'
                               WHEN {artSelect} IS NULL AND UPPER(LTRIM(RTRIM(e.Position))) = 'SUPERVISOR' THEN 'Supervisor'
                           END AS NewRole
                    FROM dbo.[User] u
                    INNER JOIN dbo.Employee e ON e.EmpId = u.EmpId
                    {artJoin}
                    WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole ur WHERE ur.UserId = u.UserId)
                ) x
                WHERE x.NewRole IS NOT NULL
                  AND ((x.NewRole = 'Manager' AND @Manager IS NOT NULL) OR (x.NewRole = 'Supervisor' AND @Supervisor IS NOT NULL));";
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

                    // Best effort: a failure here must never stop the list from loading.
                    try
                    {
                        using (var sync = new SqlCommand(SyncPlaceholderEmailsSql, con) { CommandTimeout = 120 })
                            await sync.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[Accounts] placeholder email sync failed: " + ex.Message);
                    }

                    try
                    {
                        bool hasArt;
                        using (var chk = new SqlCommand("SELECT CASE WHEN OBJECT_ID('dbo.ApprovalRoleTitle') IS NULL THEN 0 ELSE 1 END", con))
                            hasArt = Convert.ToInt32(await chk.ExecuteScalarAsync()) == 1;

                        using (var sync = new SqlCommand(BuildSyncRolesSql(hasArt), con) { CommandTimeout = 120 })
                            await sync.ExecuteNonQueryAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine("[Accounts] role-from-position sync failed: " + ex.Message);
                    }

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

            // "Newly Added" / "Oldest Added" order by creation (UserId is an identity, so it is
            // insertion order). A column sort chosen in the grid header takes priority.
            if (string.IsNullOrEmpty(_sortColumn))
            {
                if (filter == "Newly Added")
                    _filtered = _filtered.OrderByDescending(u => u.UserId).ToList();
                else if (filter == "Oldest Added")
                    _filtered = _filtered.OrderBy(u => u.UserId).ToList();
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
