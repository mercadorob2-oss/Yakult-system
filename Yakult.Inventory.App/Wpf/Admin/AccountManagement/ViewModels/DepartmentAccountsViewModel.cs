using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages.Admin.AccountManagement;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Admin.AccountManagement.ViewModels
{
    internal class DepartmentAccountsViewModel : ViewModelBase
    {
        private const int PageSize = 15;
        private readonly string _cs;

        private List<DepartmentAccountRowDto> _all      = new List<DepartmentAccountRowDto>();
        private List<DepartmentAccountRowDto> _filtered = new List<DepartmentAccountRowDto>();
        private int _currentPage = 1;
        private int _totalPages  = 1;

        private readonly Dictionary<string, HashSet<string>> _columnFilters
            = new Dictionary<string, HashSet<string>>(System.StringComparer.OrdinalIgnoreCase);

        private string _sortColumn    = null;
        private bool   _sortAscending = true;

        private bool   _isLoading;
        private string _searchText     = string.Empty;
        private string _filterOption   = "All";
        private int    _totalRecords;
        private int    _withAccount;
        private string _pageInfo       = "Page 1 of 1  (0 rows)";
        private bool   _canGoPrev;
        private bool   _canGoNext;
        private string _statusMessage;

        public ObservableCollection<DepartmentAccountRowDto> PagedRows { get; }
            = new ObservableCollection<DepartmentAccountRowDto>();

        public List<DepartmentAccountRowDto> AllRows => _all;

        public List<string>            EmailCache { get; private set; } = new List<string>();
        public Dictionary<string, int> EmailIdMap { get; private set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public bool   IsLoading    { get => _isLoading;    set => SetField(ref _isLoading,    value); }
        public int    TotalRecords { get => _totalRecords; set => SetField(ref _totalRecords, value); }
        public int    WithAccount  { get => _withAccount;  set => SetField(ref _withAccount,  value); }
        public string PageInfo     { get => _pageInfo;     set => SetField(ref _pageInfo,     value); }
        public bool   CanGoPrev       { get => _canGoPrev;      set => SetField(ref _canGoPrev,      value); }
        public bool   CanGoNext       { get => _canGoNext;      set => SetField(ref _canGoNext,      value); }
        public bool   HasStatusMessage => !string.IsNullOrEmpty(_statusMessage);
        public string StatusMessage
        {
            get => _statusMessage;
            set { SetField(ref _statusMessage, value); OnPropertyChanged(nameof(HasStatusMessage)); }
        }

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

        public DepartmentAccountsViewModel()
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
                _all = new List<DepartmentAccountRowDto>();

                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        WITH Combinations AS (
                            SELECT DISTINCT
                                c.ComId, c.Name AS CompanyName, c.Acronym AS CompanyAcronym,
                                d.DeptId, d.Name AS DepartmentName, d.Acronym AS DeptAcronym,
                                b.BranchId, b.Name AS BranchName, b.Acronym AS BranchAcronym
                            FROM dbo.Employee e
                            INNER JOIN dbo.Company    c ON e.ComId    = c.ComId
                            INNER JOIN dbo.Department d ON e.DeptId   = d.DeptId
                            INNER JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                            WHERE e.Active = 1

                            UNION

                            SELECT DISTINCT
                                c.ComId, c.Name AS CompanyName, c.Acronym AS CompanyAcronym,
                                d.DeptId, d.Name AS DepartmentName, d.Acronym AS DeptAcronym,
                                b.BranchId, b.Name AS BranchName, b.Acronym AS BranchAcronym
                            FROM dbo.BranchDepartmentCompany bdc
                            INNER JOIN dbo.Company    c ON bdc.CompanyID    = c.ComId
                            INNER JOIN dbo.Branch     b ON bdc.BranchID     = b.BranchId
                            INNER JOIN dbo.Department d ON bdc.DepartmentID = d.DeptId
                            WHERE b.Active = 1 AND d.Active = 1
                        )
                        SELECT
                            comb.CompanyName,
                            comb.DepartmentName,
                            comb.BranchName,
                            comb.CompanyAcronym,
                            comb.DeptAcronym,
                            comb.BranchAcronym,
                            da.Id           AS AccountId,
                            da.Username,
                            ISNULL(da.IsActive, 0)                               AS AccountIsActive,
                            da.DateCreated                                        AS AccountDateCreated,
                            CASE WHEN da.PasswordHash IS NOT NULL THEN 1 ELSE 0 END AS HasPassword,
                            de.EmailAddressId                                    AS DeptEmailAddressId,
                            dea.EmailAddress                                     AS DepartmentEmail,
                            da.EmailAddressId                                    AS BranchEmailAddressId,
                            bea.EmailAddress                                     AS BranchEmail,
                            comb.ComId, comb.DeptId, comb.BranchId
                        FROM Combinations comb
                        -- Same match as login: the saved ID when set, otherwise the name.
                        LEFT JOIN dbo.DepartmentAccount da
                               ON  (da.ComId    = comb.ComId    OR (da.ComId    IS NULL AND da.CompanyName    = comb.CompanyName))
                               AND (da.DeptId   = comb.DeptId   OR (da.DeptId   IS NULL AND da.DepartmentName = comb.DepartmentName))
                               AND (da.BranchId = comb.BranchId OR (da.BranchId IS NULL AND da.BranchName     = comb.BranchName))
                        LEFT JOIN dbo.DepartmentEmail de
                               ON  de.CompanyName    = comb.CompanyName
                               AND de.DepartmentName = comb.DepartmentName
                        LEFT JOIN dbo.EmailAddress dea ON dea.EmailId = de.EmailAddressId
                        LEFT JOIN dbo.EmailAddress bea ON bea.EmailId = da.EmailAddressId
                        ORDER BY comb.CompanyName, comb.DepartmentName, comb.BranchName";

                    using (var cmd = new SqlCommand(sql, con) { CommandTimeout = 120 })
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            _all.Add(new DepartmentAccountRowDto
                            {
                                CompanyName        = reader.GetString(0),
                                DepartmentName     = reader.GetString(1),
                                BranchName         = reader.GetString(2),
                                CompanyAcronym     = reader.IsDBNull(3)  ? null : reader.GetString(3),
                                DeptAcronym        = reader.IsDBNull(4)  ? null : reader.GetString(4),
                                BranchAcronym      = reader.IsDBNull(5)  ? null : reader.GetString(5),
                                AccountId          = reader.IsDBNull(6)  ? (int?)null : reader.GetInt32(6),
                                Username           = reader.IsDBNull(7)  ? null : reader.GetString(7),
                                AccountIsActive    = !reader.IsDBNull(8) && reader.GetBoolean(8),
                                AccountDateCreated = reader.IsDBNull(9)  ? (DateTime?)null : reader.GetDateTime(9),
                                HasPassword        = reader.GetInt32(10) != 0,
                                DeptEmailAddressId = reader.IsDBNull(11) ? (int?)null : reader.GetInt32(11),
                                DepartmentEmail    = reader.IsDBNull(12) ? null : reader.GetString(12),
                                EmailAddressId     = reader.IsDBNull(13) ? (int?)null : reader.GetInt32(13),
                                BranchEmail        = reader.IsDBNull(14) ? null : reader.GetString(14),
                                ComId              = reader.GetInt32(15),
                                DeptId             = reader.GetInt32(16),
                                BranchId           = reader.GetInt32(17)
                            });
                        }
                    }

                    // Request Portal submission counts per department (Dept. Level + employees).
                    var counts = await new DepartmentRequestHistoryRepository().GetSubmissionCountsAsync();
                    foreach (var row in _all)
                    {
                        if (counts.TryGetValue((row.ComId, row.BranchId, row.DeptId), out var c))
                        {
                            row.DeptLevelRequestCount = c.DeptLevelCount;
                            row.EmployeeRequestCount  = c.EmployeeCount;
                        }
                    }

                    // Email cache
                    var emailList = new List<string>();
                    var emailMap  = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    const string emailSql = "SELECT EmailId, EmailAddress FROM dbo.EmailAddress WHERE IsActive = 1 ORDER BY EmailAddress";
                    using (var cmd = new SqlCommand(emailSql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            int    id   = reader.GetInt32(0);
                            string addr = reader.GetString(1);
                            emailList.Add(addr);
                            emailMap[addr] = id;
                        }
                    }
                    EmailCache = emailList;
                    EmailIdMap = emailMap;
                }

                TotalRecords = _all.Count;
                WithAccount  = _all.Count(r => r.HasAccount);
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

            _filtered = _all.Where(r =>
            {
                if (!string.IsNullOrEmpty(search))
                {
                    bool hit =
                        r.CompanyName.ToLowerInvariant().Contains(search) ||
                        r.DepartmentName.ToLowerInvariant().Contains(search) ||
                        r.BranchName.ToLowerInvariant().Contains(search) ||
                        (r.Username        ?? "").ToLowerInvariant().Contains(search) ||
                        (r.DepartmentEmail ?? "").ToLowerInvariant().Contains(search) ||
                        (r.BranchEmail     ?? "").ToLowerInvariant().Contains(search);
                    if (!hit) return false;
                }

                switch (filter)
                {
                    case "Active":               return r.HasAccount && r.HasPassword && r.AccountIsActive;
                    case "Needs Password":        return r.HasAccount && !r.HasPassword;
                    case "Inactive":              return r.HasAccount && !r.AccountIsActive;
                    case "Has Branch Email":      return r.EmailAddressId.HasValue;
                    case "No Branch Email":       return !r.EmailAddressId.HasValue;
                    case "No Email (Both Empty)": return !r.EmailAddressId.HasValue && !r.DeptEmailAddressId.HasValue;
                    case "Has Requests":          return r.TotalRequestCount > 0;
                    case "Has Requests, No Account": return r.TotalRequestCount > 0 && !r.HasAccount;
                    default:                      return true;
                }
            }).ToList();

            foreach (var kv in _columnFilters)
            {
                string col     = kv.Key;
                var    allowed = kv.Value;
                _filtered = _filtered.Where(r => allowed.Contains(GetDeptColumnValue(r, col) ?? "")).ToList();
            }

            if (!string.IsNullOrEmpty(_sortColumn))
            {
                _filtered = _sortAscending
                    ? _filtered.OrderBy(r => GetDeptColumnValue(r, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase).ToList()
                    : _filtered.OrderByDescending(r => GetDeptColumnValue(r, _sortColumn) ?? "", System.StringComparer.OrdinalIgnoreCase).ToList();
            }

            RebuildPagedRows();
        }

        private static string GetDeptColumnValue(DepartmentAccountRowDto r, string column)
        {
            switch (column)
            {
                case "Company":    return r.CompanyName ?? "";
                case "Department": return r.DepartmentName ?? "";
                case "Branch":     return r.BranchName ?? "";
                case "Dept Email": return r.DepartmentEmail ?? "";
                case "Branch Email": return r.BranchEmail ?? "";
                case "Username":   return r.Username ?? "";
                // Zero-padded so the string sort orders it numerically.
                case "Requests":   return r.TotalRequestCount.ToString("D6");
                case "Status":
                    if (!r.HasAccount)                             return "No Account";
                    if (r.HasAccount && !r.HasPassword)            return "Needs Password";
                    if (r.HasAccount && !r.AccountIsActive)        return "Inactive";
                    return "Active";
                default: return null;
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
                case "Company":    values = _all.Select(r => r.CompanyName ?? ""); break;
                case "Department": values = _all.Select(r => r.DepartmentName ?? ""); break;
                case "Branch":     values = _all.Select(r => r.BranchName ?? ""); break;
                case "Dept Email": values = _all.Select(r => r.DepartmentEmail ?? ""); break;
                case "Branch Email": values = _all.Select(r => r.BranchEmail ?? ""); break;
                case "Username":   values = _all.Select(r => r.Username ?? ""); break;
                case "Status":
                    values = _all.Select(r =>
                    {
                        if (!r.HasAccount)                  return "No Account";
                        if (!r.HasPassword)                 return "Needs Password";
                        if (!r.AccountIsActive)             return "Inactive";
                        return "Active";
                    });
                    break;
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
            PageInfo  = $"Page {_currentPage} of {_totalPages}  ({_filtered.Count} row{(_filtered.Count == 1 ? "" : "s")})";
        }

        // ── Email operations ──────────────────────────────────────────────────

        public async Task<int> InsertOrGetEmailIdAsync(string email)
        {
            const string sql = @"
                UPDATE dbo.EmailAddress SET IsActive = 1 WHERE EmailAddress = @Email AND IsActive = 0;
                IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @Email)
                    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive) VALUES (@Email, @Email, 1);
                SELECT EmailId FROM dbo.EmailAddress WHERE EmailAddress = @Email;";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    int id = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    EmailIdMap[email] = id;
                    if (!EmailCache.Contains(email, StringComparer.OrdinalIgnoreCase)) EmailCache.Add(email);
                    return id;
                }
            }
        }

        public async Task UpsertDeptEmailAsync(string company, string department, int emailId)
        {
            const string sql = @"
                IF EXISTS (SELECT 1 FROM dbo.DepartmentEmail WHERE CompanyName=@Co AND DepartmentName=@Dept)
                    UPDATE dbo.DepartmentEmail SET EmailAddressId=@Id WHERE CompanyName=@Co AND DepartmentName=@Dept
                ELSE
                    INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId) VALUES (@Co, @Dept, @Id)";

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    cmd.Parameters.AddWithValue("@Id",   emailId);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            string emailText = EmailIdMap.FirstOrDefault(kv => kv.Value == emailId).Key ?? string.Empty;
            int?   did       = emailId;
            foreach (var r in _all.Where(r => r.CompanyName == company && r.DepartmentName == department))
            {
                r.DepartmentEmail    = emailText;
                r.DeptEmailAddressId = did;
            }
        }

        public async Task DeleteDeptEmailAsync(string company, string department)
        {
            const string sql = "DELETE FROM dbo.DepartmentEmail WHERE CompanyName=@Co AND DepartmentName=@Dept";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Co",   company);
                    cmd.Parameters.AddWithValue("@Dept", department);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            foreach (var r in _all.Where(r => r.CompanyName == company && r.DepartmentName == department))
            {
                r.DepartmentEmail    = null;
                r.DeptEmailAddressId = null;
            }
        }

        public async Task SaveBranchEmailAsync(DepartmentAccountRowDto row, int emailId)
        {
            if (!row.AccountId.HasValue)
            {
                const string insert = @"
                    INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName, ComId, DeptId, BranchId)
                    OUTPUT INSERTED.Id VALUES (@Co, @Dept, @Br, @ComId, @DeptId, @BranchId)";
                using (var con = new SqlConnection(_cs))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(insert, con))
                    {
                        cmd.Parameters.AddWithValue("@Co",   row.CompanyName);
                        cmd.Parameters.AddWithValue("@Dept", row.DepartmentName);
                        cmd.Parameters.AddWithValue("@Br",   row.BranchName);
                        AddScopeIdParams(cmd, row);
                        row.AccountId = (int)await cmd.ExecuteScalarAsync();
                    }
                }
            }

            const string sql = "UPDATE dbo.DepartmentAccount SET EmailAddressId=@Id WHERE Id=@AccountId";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id",        emailId);
                    cmd.Parameters.AddWithValue("@AccountId", row.AccountId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            string emailText = EmailIdMap.FirstOrDefault(kv => kv.Value == emailId).Key ?? string.Empty;
            row.EmailAddressId = emailId;
            row.BranchEmail    = emailText;
        }

        public async Task DeleteBranchEmailAsync(DepartmentAccountRowDto row)
        {
            if (!row.AccountId.HasValue) return;
            const string sql = "UPDATE dbo.DepartmentAccount SET EmailAddressId=NULL WHERE Id=@Id";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            row.EmailAddressId = null;
            row.BranchEmail    = null;
        }

        // Pins the account to the combination's IDs (see Migration_DepartmentAccount_AddScopeIds.sql).
        private static void AddScopeIdParams(SqlCommand cmd, DepartmentAccountRowDto row)
        {
            cmd.Parameters.AddWithValue("@ComId",    row.ComId    > 0 ? (object)row.ComId    : DBNull.Value);
            cmd.Parameters.AddWithValue("@DeptId",   row.DeptId   > 0 ? (object)row.DeptId   : DBNull.Value);
            cmd.Parameters.AddWithValue("@BranchId", row.BranchId > 0 ? (object)row.BranchId : DBNull.Value);
        }

        public async Task<bool> CreateAccountAsync(DepartmentAccountRowDto row, string username, string password)
        {
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                // Validate username uniqueness
                const string checkSql = @"
                    SELECT COUNT(*) FROM dbo.DepartmentAccount WHERE Username = @Username
                    UNION ALL
                    SELECT COUNT(*) FROM dbo.[User]             WHERE Name     = @Username";
                using (var checkCmd = new SqlCommand(checkSql, con))
                {
                    checkCmd.Parameters.AddWithValue("@Username", username);
                    using (var r = await checkCmd.ExecuteReaderAsync())
                    {
                        int total = 0;
                        while (await r.ReadAsync()) total += r.GetInt32(0);
                        if (total > 0) return false;
                    }
                }

                byte[] salt = PasswordHelper.GenerateSalt();
                byte[] hash = PasswordHelper.HashPassword(password, salt);
                string plain = AesEncryptionHelper.Encrypt(password);

                using (var tx = con.BeginTransaction())
                {
                    try
                    {
                        const string insertUserSql = @"
                            INSERT INTO dbo.[User] (Name, PasswordHash, PasswordSalt, IsActive, DateCreated)
                            VALUES (@Name, @Hash, @Salt, 1, GETDATE());
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        int newUserId;
                        using (var cmd = new SqlCommand(insertUserSql, con, tx))
                        {
                            cmd.Parameters.AddWithValue("@Name", username);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                            newUserId = (int)await cmd.ExecuteScalarAsync();
                        }

                        if (row.AccountId.HasValue)
                        {
                            const string updateSql = @"
                                UPDATE dbo.DepartmentAccount
                                SET Username=@Username, PasswordHash=@Hash, PasswordSalt=@Salt,
                                    IsActive=1, DateCreated=GETDATE(), UserId=@UserId, PlainPassword=@Plain,
                                    ComId=@ComId, DeptId=@DeptId, BranchId=@BranchId
                                WHERE Id=@Id";
                            using (var cmd = new SqlCommand(updateSql, con, tx))
                            {
                                cmd.Parameters.AddWithValue("@Id",       row.AccountId.Value);
                                AddScopeIdParams(cmd, row);
                                cmd.Parameters.AddWithValue("@Username", username);
                                cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                                cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                                cmd.Parameters.AddWithValue("@UserId", newUserId);
                                cmd.Parameters.AddWithValue("@Plain",  plain);
                                await cmd.ExecuteNonQueryAsync();
                            }
                        }
                        else
                        {
                            const string insertSql = @"
                                INSERT INTO dbo.DepartmentAccount
                                    (CompanyName, DepartmentName, BranchName, Username, PasswordHash, PasswordSalt, IsActive, DateCreated, UserId, PlainPassword,
                                     ComId, DeptId, BranchId)
                                OUTPUT INSERTED.Id
                                VALUES (@Co, @Dept, @Br, @Username, @Hash, @Salt, 1, GETDATE(), @UserId, @Plain,
                                        @ComId, @DeptId, @BranchId)";
                            using (var cmd = new SqlCommand(insertSql, con, tx))
                            {
                                AddScopeIdParams(cmd, row);
                                cmd.Parameters.AddWithValue("@Co",       row.CompanyName);
                                cmd.Parameters.AddWithValue("@Dept",     row.DepartmentName);
                                cmd.Parameters.AddWithValue("@Br",       row.BranchName);
                                cmd.Parameters.AddWithValue("@Username", username);
                                cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                                cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                                cmd.Parameters.AddWithValue("@UserId",   newUserId);
                                cmd.Parameters.AddWithValue("@Plain",    plain);
                                row.AccountId = (int)await cmd.ExecuteScalarAsync();
                            }
                        }

                        tx.Commit();
                    }
                    catch
                    {
                        tx.Rollback();
                        throw;
                    }
                }
            }

            row.Username           = username;
            row.HasPassword        = true;
            row.AccountIsActive    = true;
            row.AccountDateCreated = DateTime.Now;
            return true;
        }

        public async Task ChangePasswordAsync(DepartmentAccountRowDto row, string newPassword)
        {
            if (!row.AccountId.HasValue) return;

            byte[] salt  = PasswordHelper.GenerateSalt();
            byte[] hash  = PasswordHelper.HashPassword(newPassword, salt);
            string plain = AesEncryptionHelper.Encrypt(newPassword);

            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();

                const string sqlDept = @"
                    UPDATE dbo.DepartmentAccount
                    SET PasswordHash=@Hash, PasswordSalt=@Salt, PlainPassword=@Plain
                    WHERE Id=@Id";
                using (var cmd = new SqlCommand(sqlDept, con))
                {
                    cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                    cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                    cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                    cmd.Parameters.AddWithValue("@Plain", plain);
                    await cmd.ExecuteNonQueryAsync();
                }

                const string sqlUser = @"
                    UPDATE dbo.[User]
                    SET PasswordHash=@Hash, PasswordSalt=@Salt
                    WHERE UserId = (SELECT UserId FROM dbo.DepartmentAccount WHERE Id=@Id)";
                using (var cmd = new SqlCommand(sqlUser, con))
                {
                    cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                    cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                    cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });
                    await cmd.ExecuteNonQueryAsync();
                }
            }

            row.HasPassword = true;
        }

        public async Task<string> GetPlainPasswordAsync(DepartmentAccountRowDto row)
        {
            if (!row.AccountId.HasValue) return null;
            const string sql = "SELECT PlainPassword FROM dbo.DepartmentAccount WHERE Id=@Id";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                    var result = await cmd.ExecuteScalarAsync();
                    return AesEncryptionHelper.Decrypt(result as string);
                }
            }
        }

        public async Task<bool> VerifyAdminPasswordAsync(string enteredPassword)
        {
            const string sql = "SELECT PasswordHash, PasswordSalt FROM dbo.[User] WHERE UserId=@Id";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", AppSession.CurrentUserId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync()) return false;
                        if (reader.IsDBNull(0) || reader.IsDBNull(1)) return false;
                        byte[] storedHash = (byte[])reader[0];
                        byte[] storedSalt = (byte[])reader[1];
                        return PasswordHelper.VerifyPassword(enteredPassword, storedHash, storedSalt);
                    }
                }
            }
        }

        public async Task DeleteAccountAsync(DepartmentAccountRowDto row)
        {
            if (!row.AccountId.HasValue) return;
            const string sql = "UPDATE dbo.DepartmentAccount SET PasswordHash=NULL,PasswordSalt=NULL,IsActive=0 WHERE Id=@Id";
            using (var con = new SqlConnection(_cs))
            {
                await con.OpenAsync();
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Id", row.AccountId.Value);
                    await cmd.ExecuteNonQueryAsync();
                }
            }
            row.HasPassword     = false;
            row.AccountIsActive = false;
        }

        public static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return false;
            try { _ = new System.Net.Mail.MailAddress(email); return true; }
            catch { return false; }
        }
    }
}
