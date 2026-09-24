using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels
{
    public sealed class ApproverRow
    {
        public string EmployeeNumber { get; set; }
        public string Title          { get; set; }
        public string EmployeeName   { get; set; }
        public string Position       { get; set; }
        public string Department     { get; set; }
        public string Company        { get; set; }
        public string Branch         { get; set; }
        public string IsApprover     { get; set; }
        public string SystemRoles    { get; set; }
        public bool   IsArchived     { get; set; }
        public int    EmpId          { get; set; }
        public bool   HasAccount     { get; set; }
        public bool   IsSelected     { get; set; }   // checkbox state in the Bulk Create dialog
        public string AccountStatus  => HasAccount ? "Has account" : "No account";
    }

    public sealed class BulkAccountResult
    {
        public string EmployeeNumber { get; set; }
        public string EmployeeName   { get; set; }
        public string Username       { get; set; }
        public string Email          { get; set; }
        public string Role           { get; set; }
        public string Password       { get; set; }
        public string Status         { get; set; }   // Created / Skipped / Failed
        public string Message        { get; set; }
    }

    public sealed class ApproverTitleEntry
    {
        public string PositionTitle { get; set; }
        public string ApprovalRole  { get; set; }
        public int    RolePriority  { get; set; }
    }

    public class ApproverManagementViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<ApproverRow> _all      = new List<ApproverRow>();
        private List<ApproverRow> _filtered = new List<ApproverRow>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText     = string.Empty;
        private bool   _showArchived;
        private string _pageInfo = "Page 0 of 0 (0 rows)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private int    _totalRecords;
        private int    _approverCount;

        public ObservableCollection<ApproverRow> PagedRows { get; } = new ObservableCollection<ApproverRow>();

        public List<ApproverTitleEntry> ApproverTitleEntries { get; private set; } = new List<ApproverTitleEntry>();
        public List<ApproverRow> AllRows => _all;

        public bool   IsLoading      { get => _isLoading;      set => SetField(ref _isLoading,      value); }
        public string PageInfo       { get => _pageInfo;       set => SetField(ref _pageInfo,       value); }
        public bool   CanGoPrev      { get => _canGoPrev;      set => SetField(ref _canGoPrev,      value); }
        public bool   CanGoNext      { get => _canGoNext;      set => SetField(ref _canGoNext,      value); }
        public int    TotalRecords   { get => _totalRecords;   set => SetField(ref _totalRecords,   value); }
        public int    ApproverCount  { get => _approverCount;  set => SetField(ref _approverCount,  value); }

        public string SearchText
        {
            get => _searchText;
            set { if (SetField(ref _searchText, value)) ApplyFilter(); }
        }

        public bool ShowArchived
        {
            get => _showArchived;
            set { if (SetField(ref _showArchived, value)) ApplyFilter(); }
        }

        // ── Position groups (fallback when dbo.ApprovalRoleTitle is empty) ────
        private static readonly List<(string Key, string Display)> CoordinatorPositions = new List<(string, string)>
        {
            ("COORDINATOR",                "Coordinator"),
            ("ACCOUNT COORDINATOR",        "Account Coordinator"),
            ("ACTING ACCOUNT COORDINATOR", "Acting Account Coordinator"),
            ("ASST. COORDINATOR",          "Asst. Coordinator"),
            ("ASSISTANT COORDINATOR",      "Assistant Coordinator"),
            ("LADY COORDINATOR",           "Lady Coordinator"),
        };

        private static readonly List<(string Key, string Display)> ManagerPositions = new List<(string, string)>
        {
            ("MANAGER",                  "Manager"),
            ("ASST. MANAGER",            "Asst. Manager"),
            ("ASSISTANT MANAGER",        "Assistant Manager"),
            ("JR. ASST. MANAGER",        "Jr. Asst. Manager"),
            ("JUNIOR ASSISTANT MANAGER", "Junior Asst. Manager"),
            ("ACTING JR. ASST. MANAGER", "Acting Jr. Asst. Manager"),
        };

        private static readonly List<(string Key, string Display)> SupervisorPositions = new List<(string, string)>
        {
            ("SUPERVISOR", "Supervisor"),
        };

        private static readonly HashSet<string> HigherUpPositions = new HashSet<string>(
            CoordinatorPositions.Select(p => p.Key)
                .Concat(ManagerPositions.Select(p => p.Key))
                .Concat(SupervisorPositions.Select(p => p.Key)),
            StringComparer.OrdinalIgnoreCase);

        public ApproverManagementViewModel()
        {
            _cs = DatabaseConfig.ConnectionString;
        }

        private async Task<List<ApproverTitleEntry>> LoadApproverTitlesFromDbAsync(SqlConnection con)
        {
            var results = new List<ApproverTitleEntry>();
            try
            {
                using (var cmd = new SqlCommand(
                    "SELECT PositionTitle, ApprovalRole, RolePriority FROM dbo.ApprovalRoleTitle WHERE IsActive = 1 ORDER BY RolePriority DESC, PositionTitle", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync())
                        results.Add(new ApproverTitleEntry
                        {
                            PositionTitle = r.GetString(0),
                            ApprovalRole  = r.GetString(1),
                            RolePriority  = r.GetInt32(2)
                        });
            }
            catch { /* table may not exist yet — caller falls back to hardcoded */ }
            return results;
        }

        public async Task LoadAsync()
        {
            if (string.IsNullOrWhiteSpace(_cs)) return;

            IsLoading = true;
            try
            {
                var results = new List<ApproverRow>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    ApproverTitleEntries = await LoadApproverTitlesFromDbAsync(con);

                    IEnumerable<string> positionKeys = ApproverTitleEntries.Count > 0
                        ? ApproverTitleEntries.Select(t => t.PositionTitle)
                        : (IEnumerable<string>)HigherUpPositions;

                    var posArray  = positionKeys.ToArray();
                    var posParams = posArray.Select((p, i) => $"@p{i}").ToArray();
                    string inClause = string.Join(", ", posParams);

                    string sql = $@"
                        SELECT
                            e.EmployeeNumber,
                            t.Code,
                            e.Name         AS EmployeeName,
                            e.Position,
                            d.Name         AS Department,
                            c.Name         AS Company,
                            b.Name         AS Branch,
                            CASE WHEN UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause}) THEN 'Yes' ELSE 'No' END AS IsApprover,
                            ISNULL(
                                STUFF((
                                    SELECT ', ' + r.RoleName
                                    FROM dbo.[User] u2
                                    INNER JOIN dbo.UserRole ur ON ur.UserId = u2.UserId
                                    INNER JOIN dbo.Role r      ON r.RoleId  = ur.RoleId AND r.IsActive = 1
                                    WHERE u2.EmpId = e.EmpId
                                    FOR XML PATH(''), TYPE
                                ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
                            'No Account') AS SystemRoles,
                            CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END AS IsArchived,
                            e.EmpId,
                            CASE WHEN EXISTS (SELECT 1 FROM dbo.[User] ux WHERE ux.EmpId = e.EmpId) THEN 1 ELSE 0 END AS HasAccount
                        FROM dbo.Employee e
                        LEFT JOIN dbo.Title        t   ON e.TitleId  = t.TitleId
                        LEFT JOIN dbo.Department   d   ON e.DeptId   = d.DeptId
                        LEFT JOIN dbo.Company      c   ON e.ComId    = c.ComId
                        LEFT JOIN dbo.Branch       b   ON e.BranchId = b.BranchId
                        LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
                        WHERE UPPER(LTRIM(RTRIM(e.Position))) IN ({inClause})
                        ORDER BY e.Name";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        for (int i = 0; i < posArray.Length; i++)
                            cmd.Parameters.AddWithValue($"@p{i}", posArray[i].ToUpperInvariant());

                        using (var reader = await cmd.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                results.Add(new ApproverRow
                                {
                                    EmployeeNumber = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                    Title          = reader.IsDBNull(1) ? "" : reader.GetString(1),
                                    EmployeeName   = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                    Position       = reader.IsDBNull(3) ? "" : reader.GetString(3),
                                    Department     = reader.IsDBNull(4) ? "" : reader.GetString(4),
                                    Company        = reader.IsDBNull(5) ? "" : reader.GetString(5),
                                    Branch         = reader.IsDBNull(6) ? "" : reader.GetString(6),
                                    IsApprover     = reader.IsDBNull(7) ? "" : reader.GetString(7),
                                    SystemRoles    = reader.IsDBNull(8) ? "No Account" : reader.GetString(8),
                                    IsArchived     = !reader.IsDBNull(9) && reader.GetInt32(9) == 1,
                                    EmpId          = reader.GetInt32(10),
                                    HasAccount     = reader.GetInt32(11) == 1,
                                });
                            }
                        }
                    }
                }

                _all = results;
                TotalRecords  = _all.Count;
                ApproverCount = _all.Count(r => string.Equals(r.IsApprover, "Yes", StringComparison.OrdinalIgnoreCase));
                _currentPage = 1;
                ApplyFilter();
            }
            finally
            {
                IsLoading = false;
            }
        }

        // Produces a sort key so "141X" (3-digit manager code) sorts beside "141"
        // rather than after all 4-digit numbers: pads the leading digit run to 10 chars.
        internal static string EmpNumSortKey(string empNum)
        {
            if (string.IsNullOrEmpty(empNum)) return "";
            int i = 0;
            while (i < empNum.Length && char.IsDigit(empNum[i])) i++;
            return empNum.Substring(0, i).PadLeft(10, '0') + empNum.Substring(i).ToUpperInvariant();
        }

        internal static string GetColumnValue(ApproverRow r, string column)
        {
            switch (column)
            {
                case "EmployeeNumber": return r.EmployeeNumber;
                case "Title":          return r.Title;
                case "EmployeeName":   return r.EmployeeName;
                case "Position":       return r.Position;
                case "Department":     return r.Department;
                case "Company":        return r.Company;
                case "Branch":         return r.Branch;
                case "IsApprover":     return r.IsApprover;
                case "SystemRoles":    return r.SystemRoles;
                default:               return null;
            }
        }

        public void ApplyFilter()
        {
            string q = (_searchText ?? "").Trim();

            IEnumerable<ApproverRow> filtered = _showArchived
                ? _all
                : _all.Where(r => !r.IsArchived);

            if (!string.IsNullOrWhiteSpace(q))
            {
                filtered = filtered.Where(x =>
                    x.EmployeeNumber.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.EmployeeName.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Position.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Department.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0
                    || x.Branch.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            foreach (var kvp in _columnFilters)
            {
                var selected = kvp.Value;
                string col = kvp.Key;
                filtered = filtered.Where(r => selected.Contains(GetColumnValue(r, col) ?? "", StringComparer.OrdinalIgnoreCase));
            }

            _filtered = filtered.ToList();

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                Func<ApproverRow, string> key = _sortColumn == "EmployeeNumber"
                    ? (Func<ApproverRow, string>)(r => EmpNumSortKey(r.EmployeeNumber))
                    : (r => GetColumnValue(r, _sortColumn) ?? "");

                _filtered = _sortAscending
                    ? _filtered.OrderBy(key, StringComparer.OrdinalIgnoreCase).ToList()
                    : _filtered.OrderByDescending(key, StringComparer.OrdinalIgnoreCase).ToList();
            }

            _currentPage = 1;
            RebuildPagedRows();
        }

        /// <summary>
        /// Position to system role: any Manager-family position gets "Manager", Supervisor gets
        /// "Supervisor". "IT Manager" / "IT Supervisor" are IT Department roles and are never
        /// assigned from a position. Coordinators get none (that role is deprecated).
        /// </summary>
        internal string RoleNameForPosition(string position)
        {
            string p = (position ?? "").Trim();
            if (p.Length == 0) return null;

            var entry = ApproverTitleEntries.FirstOrDefault(t =>
                string.Equals(t.PositionTitle?.Trim(), p, StringComparison.OrdinalIgnoreCase));
            string role = entry?.ApprovalRole;

            if (string.IsNullOrEmpty(role))
                role = ManagerPositions.Any(m => m.Key.Equals(p, StringComparison.OrdinalIgnoreCase)) ? "Manager"
                     : SupervisorPositions.Any(m => m.Key.Equals(p, StringComparison.OrdinalIgnoreCase)) ? "Supervisor"
                     : null;

            return string.Equals(role, "Manager", StringComparison.OrdinalIgnoreCase) ? "Manager"
                 : string.Equals(role, "Supervisor", StringComparison.OrdinalIgnoreCase) ? "Supervisor"
                 : null;
        }

        /// <summary>Rows currently shown (search, archived toggle and column filters applied), across all pages.</summary>
        public List<ApproverRow> FilteredRows => _filtered;

        /// <summary>Copy of the active column filters, so a dialog can start from the same filter state.</summary>
        public Dictionary<string, HashSet<string>> GetColumnFiltersSnapshot()
            => _columnFilters.ToDictionary(
                kv => kv.Key,
                kv => new HashSet<string>(kv.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Creates a dbo.[User] account for each row, linked by EmpId. Each account is its own
        /// transaction so one failure does not undo the others. Employees that already have an
        /// account are skipped. The username is the employee name (as in the single-account flow);
        /// if that name is taken, the employee number is appended.
        /// </summary>
        public async Task<List<BulkAccountResult>> CreateAccountsAsync(
            IEnumerable<ApproverRow> rows,
            Func<ApproverRow, string> passwordFor,
            bool mustChangePassword,
            bool assignRoleFromPosition = false,
            IProgress<int> progress = null)
        {
            var results = new List<BulkAccountResult>();
            int done = 0;

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new SqlCommand("SELECT Name FROM dbo.[User]", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync()) usedNames.Add(r.GetString(0).Trim());

                // System roles by name, only those that exist and are active.
                var roleIds = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                if (assignRoleFromPosition)
                {
                    using (var cmd = new SqlCommand("SELECT RoleId, RoleName FROM dbo.Role WHERE IsActive = 1 AND RoleName IN ('Manager','Supervisor')", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync()) roleIds[r.GetString(1)] = r.GetInt32(0);
                }

                // Emails already held by an account (dbo.User.EmailAddress is unique).
                var usedEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                using (var cmd = new SqlCommand("SELECT EmailAddress FROM dbo.[User] WHERE EmailAddress IS NOT NULL", con))
                using (var r = await cmd.ExecuteReaderAsync())
                    while (await r.ReadAsync()) usedEmails.Add(r.GetString(0).Trim());

                // Personal email = the employee's primary email (Employee Management "Personal Email").
                // Branch email = the DepartmentAccount email for their company/department/branch.
                var personalEmails = new Dictionary<int, string>();
                var branchEmails   = new Dictionary<int, string>();
                try
                {
                    using (var cmd = new SqlCommand(@"
                        SELECT ee.EmpId, ea.EmailAddress
                        FROM dbo.EmployeeEmail ee
                        INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
                        WHERE ee.IsPrimary = 1 AND ee.IsActive = 1 AND ea.IsActive = 1
                        ORDER BY ee.EmployeeEmailId", con))
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                        {
                            int id = r.GetInt32(0);
                            if (!personalEmails.ContainsKey(id)) personalEmails[id] = r.GetString(1).Trim();
                        }

                    using (var cmd = new SqlCommand(@"
                        SELECT e.EmpId, ba.EmailAddress
                        FROM dbo.Employee e
                        INNER JOIN dbo.Company    c  ON c.ComId    = e.ComId
                        INNER JOIN dbo.Branch     b  ON b.BranchId = e.BranchId
                        INNER JOIN dbo.Department d  ON d.DeptId   = e.DeptId
                        INNER JOIN dbo.DepartmentAccount da
                                ON da.CompanyName = c.Name AND da.DepartmentName = d.Name AND da.BranchName = b.Name
                        INNER JOIN dbo.EmailAddress ba ON ba.EmailId = da.EmailAddressId
                        ORDER BY e.EmpId", con) { CommandTimeout = 120 })
                    using (var r = await cmd.ExecuteReaderAsync())
                        while (await r.ReadAsync())
                        {
                            int id = r.GetInt32(0);
                            if (!branchEmails.ContainsKey(id)) branchEmails[id] = r.GetString(1).Trim();
                        }
                }
                catch { /* email tables missing on older DBs: placeholder emails are used */ }

                foreach (var row in rows)
                {
                    var res = new BulkAccountResult
                    {
                        EmployeeNumber = row.EmployeeNumber,
                        EmployeeName   = row.EmployeeName
                    };
                    results.Add(res);

                    try
                    {
                        string baseName = (row.EmployeeName ?? "").Trim();
                        if (baseName.Length == 0)
                        {
                            res.Status = "Failed"; res.Message = "Employee has no name.";
                            continue;
                        }

                        string username = baseName;
                        if (usedNames.Contains(username))
                            username = $"{baseName} ({row.EmployeeNumber})";
                        if (username.Length > 100 || usedNames.Contains(username))
                        {
                            res.Status = "Failed"; res.Message = "Could not build a unique username.";
                            continue;
                        }

                        // Personal email first, then the branch email, then the placeholder the
                        // single-account flow uses. dbo.User.EmailAddress is unique, so an address
                        // already held by another account (branch emails are shared) is skipped.
                        string email = null;
                        foreach (var cand in new[]
                        {
                            personalEmails.TryGetValue(row.EmpId, out var pe) ? pe : null,
                            branchEmails.TryGetValue(row.EmpId, out var be) ? be : null
                        })
                        {
                            if (!string.IsNullOrWhiteSpace(cand) && cand.Length <= 255 && !usedEmails.Contains(cand))
                            { email = cand; break; }
                        }
                        if (email == null) email = $"user{row.EmpId}@yakult.local";

                        string password = passwordFor(row);
                        byte[] salt = Yakult.Inventory.App.Helpers.PasswordHelper.GenerateSalt();
                        byte[] hash = Yakult.Inventory.App.Helpers.PasswordHelper.HashPassword(password, salt);

                        const string sql = @"
                            IF EXISTS (SELECT 1 FROM dbo.[User] WHERE EmpId = @EmpId)
                                SELECT CAST(0 AS INT);
                            ELSE
                            BEGIN
                                INSERT INTO dbo.[User] (
                                    Name, EmailAddress, DateCreated, IsDeveloper, EmpId,
                                    PasswordHash, PasswordSalt, IsTemporaryPassword, MustChangePassword, IsActive)
                                VALUES (
                                    @Name, @Email, GETDATE(), 0, @EmpId,
                                    @Hash, @Salt, @IsTemp, @MustChange, 1);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);
                            END";

                        int userId;
                        using (var tx = con.BeginTransaction())
                        {
                            try
                            {
                                using (var cmd = new SqlCommand(sql, con, tx))
                                {
                                    cmd.Parameters.AddWithValue("@Name",       username);
                                    cmd.Parameters.AddWithValue("@Email",      email);
                                    cmd.Parameters.AddWithValue("@EmpId",      row.EmpId);
                                    cmd.Parameters.AddWithValue("@Hash",       hash);
                                    cmd.Parameters.AddWithValue("@Salt",       salt);
                                    cmd.Parameters.AddWithValue("@IsTemp",     mustChangePassword);
                                    cmd.Parameters.AddWithValue("@MustChange", mustChangePassword);
                                    userId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                                }

                                string roleName = assignRoleFromPosition ? RoleNameForPosition(row.Position) : null;
                                if (userId != 0 && roleName != null && roleIds.TryGetValue(roleName, out int roleId))
                                {
                                    using (var rc = new SqlCommand(
                                        "INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned) VALUES (@U, @R, GETDATE())", con, tx))
                                    {
                                        rc.Parameters.AddWithValue("@U", userId);
                                        rc.Parameters.AddWithValue("@R", roleId);
                                        await rc.ExecuteNonQueryAsync();
                                    }
                                    res.Role = roleName;
                                }
                                tx.Commit();
                            }
                            catch { tx.Rollback(); throw; }
                        }

                        if (userId == 0)
                        {
                            res.Status = "Skipped"; res.Message = "Already has an account.";
                            continue;
                        }

                        usedNames.Add(username);
                        usedEmails.Add(email);
                        row.HasAccount = true;
                        res.Username = username;
                        res.Email    = email;
                        res.Password = password;
                        res.Status   = "Created";

                        Yakult.Inventory.App.Services.ActivityLogger.Log(Yakult.Inventory.App.Services.ActivityLogger.Actions.Create, "User", userId,
                            $"Bulk-created account '{username}' for employee {row.EmployeeNumber}");
                    }
                    catch (Exception ex)
                    {
                        res.Status  = "Failed";
                        res.Message = ex.Message;
                    }
                    finally
                    {
                        progress?.Report(++done);
                    }
                }
            }

            return results;
        }

        public HashSet<string> GetColumnFilter(string column)
            => _columnFilters.TryGetValue(column, out var f) ? f : null;

        public void SetColumnFilter(string column, HashSet<string> values)
        {
            if (values == null || values.Count == 0)
                _columnFilters.Remove(column);
            else
                _columnFilters[column] = values;
            ApplyFilter();
        }

        public void ClearColumnFilter(string column)
        {
            _columnFilters.Remove(column);
            ApplyFilter();
        }

        public void SetSort(string column, bool ascending)
        {
            _sortColumn    = column;
            _sortAscending = ascending;
            ApplyFilter();
        }

        public List<string> GetUniqueValuesForColumn(string column)
        {
            return _all.Select(r => GetColumnValue(r, column) ?? "")
                       .Where(v => !string.IsNullOrEmpty(v))
                       .Distinct(StringComparer.OrdinalIgnoreCase)
                       .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
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
            PageInfo  = _filtered.Count == 0
                ? "Page 0 of 0 (0 rows)"
                : $"Page {_currentPage} of {_totalPages} ({_filtered.Count} rows)";
        }
    }
}
