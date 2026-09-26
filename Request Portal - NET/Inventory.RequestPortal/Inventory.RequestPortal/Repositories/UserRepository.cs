using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Repository for User authentication.
    /// TRANSLATED FROM: Yakult.Inventory.App/Data/UserRepository.cs
    /// Uses the same VarBinary password authentication method.
    /// </summary>
    public class UserRepository : IUserRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(
            IConnectionStringProvider connectionStringProvider,
            ILogger<UserRepository> logger)
        {
            _connectionStringProvider = connectionStringProvider;
            _logger = logger;
        }

        /// <summary>
        /// Authenticates user and loads employee data and roles.
        /// COPIED FROM: Yakult.Inventory.App/Data/UserRepository.cs
        ///              + LoginPage.cs (LoadEmployeeDataAsync, LoadUserRolesAsync)
        ///
        /// Uses VarBinary password storage (CONVERT pattern from WinForms).
        /// </summary>
        public async Task<AuthResult> AuthenticateAsync(string username, string password)
        {
            try
            {
                using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
                await con.OpenAsync();

                // Log which database we're authenticating against
                _logger.LogInformation("Authenticating user '{Username}' against database: {Database} on {Server}",
                    username, con.Database, con.DataSource);

                // Step 1: Authenticate user (same SQL as WinForms)
                // LevelRank priority:
                //   1. User.LevelId explicit override (set by admin) — including IT (999)
                //   2. Employee position → Manager / Supervisor / Coordinator / Employee
                //   3. No linked employee → 0 (unassigned)
                // IsDeveloper = 1 is the only automatic 999 path; IT department employees
                // follow their position rank like any other department.
                const string authSql = @"
                    SELECT u.UserId, u.Name, u.EmailAddress,
                           ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                           CASE
                               -- Developers always get the highest level
                               WHEN u.IsDeveloper = 1 THEN 999
                               -- Explicit admin override wins next
                               WHEN al_override.LevelRank IS NOT NULL THEN al_override.LevelRank
                               -- Otherwise derive from Employee.Position
                               WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                        'MANAGER', 'ASST. MANAGER',
                                        'JR. ASST. MANAGER', 'ACTING JR. ASST. MANAGER')
                                   THEN 4
                               WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) = 'SUPERVISOR'
                                   THEN 3
                               WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                                        'COORDINATOR', 'ACCOUNT COORDINATOR',
                                        'ACTING ACCOUNT COORDINATOR',
                                        'ASST. COORDINATOR', 'LADY COORDINATOR')
                                   THEN 2
                               WHEN e.EmpId IS NOT NULL
                                   THEN 1
                               ELSE 0
                           END AS LevelRank
                    FROM   [User]          u
                    LEFT JOIN dbo.AccountLevel al_override ON u.LevelId = al_override.LevelId
                    LEFT JOIN dbo.Employee     e           ON u.EmpId   = e.EmpId
                    WHERE  u.Name       = @Name
                      AND  u.[Password] = CONVERT(VARBINARY(MAX), @Password);";

                int userId = 0;
                string fullName = string.Empty;
                string email = string.Empty;
                bool isDeveloper = false;
                int levelRank = 0;

                using (var cmd = new SqlCommand(authSql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", username);
                    cmd.Parameters.AddWithValue("@Password", password);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        userId      = reader.GetInt32(0);
                        fullName    = reader.GetString(1);
                        email       = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        isDeveloper = reader.GetBoolean(3);
                        levelRank   = reader.GetInt32(4);
                    }
                }

                if (userId == 0)
                {
                    // Normal CONVERT VARBINARY auth failed.
                    // Path 1: Regular User account created via WinForms (stores PasswordHash/PasswordSalt only).
                    var regularUserResult = await TryRegularUserSha256LoginAsync(username, password, con);
                    if (regularUserResult != null) return regularUserResult;

                    // Path 2: Department accounts use SHA256 hash/salt instead.
                    // Try two sub-paths in order:
                    //   A) UserId IS NOT NULL → account already linked to a User row with hash/salt (login directly)
                    //   B) UserId IS NULL     → never migrated; verify SHA256, create User row, link it
                    _logger.LogInformation("Normal auth failed for '{Username}', trying dept account SHA256 paths", username);

                    var deptResult = await TryDeptAccountLoginAsync(username, password, con);
                    if (deptResult != null) return deptResult;

                    _logger.LogWarning("Authentication failed for user '{Username}' - user not found in database {Database}",
                        username, con.Database);
                    return new AuthResult
                    {
                        Success = false,
                        ErrorMessage = "Invalid username or password."
                    };
                }

                // Step 2: Load employee data (optional — dept accounts won't have this)
                int? employeeId = null;
                string? employeeName = null;
                string? employeePosition = null;
                int? companyId = null;
                string? companyName = null;
                int? branchId = null;
                string? branchName = null;
                int? departmentId = null;
                string? departmentName = null;

                const string empSql = @"
                    SELECT
                        e.EmpId,
                        e.Name,
                        e.Position,
                        e.ComId,
                        c.Name,
                        e.BranchId,
                        b.Name,
                        e.DeptId,
                        d.Name
                    FROM dbo.[User] u
                    INNER JOIN dbo.Employee e ON u.EmpId = e.EmpId
                    LEFT JOIN dbo.Company c ON e.ComId = c.ComId
                    LEFT JOIN dbo.Branch b ON e.BranchId = b.BranchId
                    LEFT JOIN dbo.Department d ON e.DeptId = d.DeptId
                    WHERE u.UserId = @UserId AND e.Active = 1";

                using (var cmd = new SqlCommand(empSql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        employeeId = reader.GetInt32(0);
                        employeeName = reader.GetString(1);
                        employeePosition = reader.IsDBNull(2) ? null : reader.GetString(2);
                        companyId = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                        companyName = reader.IsDBNull(4) ? null : reader.GetString(4);
                        branchId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
                        branchName = reader.IsDBNull(6) ? null : reader.GetString(6);
                        departmentId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                        departmentName = reader.IsDBNull(8) ? null : reader.GetString(8);
                    }
                }

                // Step 3: If no employee, check if this is a Department Account
                bool isDeptAccount = false;
                int? deptAccountId = null;
                int? deptAccountCompanyId = null;
                string? deptAccountCompanyName = null;
                int? deptAccountDepartmentId = null;
                string? deptAccountDepartmentName = null;
                int? deptAccountBranchId = null;
                string? deptAccountBranchName = null;

                if (!employeeId.HasValue)
                {
                    const string daSql = @"
                        SELECT
                            da.Id,
                            da.IsActive,
                            c.ComId,    c.Name AS CompanyName,
                            d.DeptId,   d.Name AS DeptName,
                            b.BranchId, b.Name AS BranchName
                        FROM dbo.DepartmentAccount da
                        LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
                        LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
                        LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
                        WHERE da.UserId = @UserId";

                    using (var cmd = new SqlCommand(daSql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (await reader.ReadAsync() && !reader.IsDBNull(1) && reader.GetBoolean(1))
                        {
                            isDeptAccount          = true;
                            deptAccountId          = reader.GetInt32(0);
                            deptAccountCompanyId   = reader.IsDBNull(2) ? null : reader.GetInt32(2);
                            deptAccountCompanyName = reader.IsDBNull(3) ? null : reader.GetString(3);
                            deptAccountDepartmentId   = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                            deptAccountDepartmentName = reader.IsDBNull(5) ? null : reader.GetString(5);
                            deptAccountBranchId   = reader.IsDBNull(6) ? null : reader.GetInt32(6);
                            deptAccountBranchName = reader.IsDBNull(7) ? null : reader.GetString(7);
                        }
                    }

                    if (!isDeptAccount)
                    {
                        _logger.LogWarning("User '{Username}' (UserId: {UserId}) is not linked to an active employee or department account in database {Database}",
                            username, userId, con.Database);
                        return new AuthResult
                        {
                            Success = false,
                            ErrorMessage = "Your account is not linked to an employee or department. Please contact an administrator to complete your account setup."
                        };
                    }
                }

                // Step 4: Load user roles (same SQL as LoginPage.LoadUserRolesAsync)
                var roles = new List<string>();

                const string rolesSql = @"
                    SELECT r.RoleName
                    FROM dbo.UserRole ur
                    INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                    WHERE ur.UserId = @UserId AND r.IsActive = 1
                    ORDER BY r.RoleName";

                using (var cmd = new SqlCommand(rolesSql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);

                    using var reader = await cmd.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        roles.Add(reader.GetString(0));
                    }
                }

                // Department accounts are always Requesters
                if (isDeptAccount && !roles.Any(r => r.Equals("Requester", StringComparison.OrdinalIgnoreCase)))
                    roles.Add("Requester");

                // If no roles assigned, inject default Requester role (same as WinForms)
                if (roles.Count == 0)
                    roles.Add("Requester");

                return new AuthResult
                {
                    Success = true,
                    UserId = userId,
                    Name = fullName,
                    Email = email,
                    IsDeveloper = isDeveloper,
                    LevelRank = levelRank,
                    EmployeeId = employeeId,
                    EmployeeName = employeeName,
                    EmployeePosition = employeePosition,
                    CompanyId = companyId,
                    CompanyName = companyName,
                    BranchId = branchId,
                    BranchName = branchName,
                    DepartmentId = departmentId,
                    DepartmentName = departmentName,
                    Roles = roles,
                    IsDepartmentAccountSession       = isDeptAccount,
                    DepartmentAccountId              = deptAccountId,
                    DepartmentAccountCompanyId       = deptAccountCompanyId,
                    DepartmentAccountCompanyName     = deptAccountCompanyName,
                    DepartmentAccountDepartmentId    = deptAccountDepartmentId,
                    DepartmentAccountDepartmentName  = deptAccountDepartmentName,
                    DepartmentAccountBranchId        = deptAccountBranchId,
                    DepartmentAccountBranchName      = deptAccountBranchName
                };
            }
            catch (SqlException sqlEx)
            {
                // SQL-specific errors (connection, query, schema issues)
                throw new InvalidOperationException(
                    $"Database error during authentication: {sqlEx.Message} (Error {sqlEx.Number})",
                    sqlEx);
            }
            catch (Exception ex)
            {
                // Other errors
                throw new InvalidOperationException(
                    $"Authentication failed: {ex.Message}",
                    ex);
            }
        }

        /// <summary>
        /// Authenticates a regular User account that was created via the WinForms app.
        /// WinForms-created accounts store PasswordHash/PasswordSalt but leave Password (VARBINARY) NULL,
        /// so the main CONVERT VARBINARY auth path misses them.
        /// </summary>
        private async Task<AuthResult?> TryRegularUserSha256LoginAsync(string username, string password, SqlConnection con)
        {
            const string sql = @"
                SELECT u.UserId, u.Name, ISNULL(u.EmailAddress,'') AS Email,
                       ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                       u.PasswordHash, u.PasswordSalt,
                       CASE
                           WHEN u.IsDeveloper = 1 THEN 999
                           WHEN al_override.LevelRank IS NOT NULL THEN al_override.LevelRank
                           WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position,'')))) IN (
                                    'MANAGER','ASST. MANAGER','JR. ASST. MANAGER','ACTING JR. ASST. MANAGER') THEN 4
                           WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position,'')))) = 'SUPERVISOR' THEN 3
                           WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position,'')))) IN (
                                    'COORDINATOR','ACCOUNT COORDINATOR','ACTING ACCOUNT COORDINATOR',
                                    'ASST. COORDINATOR','LADY COORDINATOR') THEN 2
                           WHEN e.EmpId IS NOT NULL THEN 1
                           ELSE 0
                       END AS LevelRank
                FROM dbo.[User] u
                LEFT JOIN dbo.AccountLevel al_override ON u.LevelId = al_override.LevelId
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                WHERE u.Name = @Name
                  AND u.PasswordHash IS NOT NULL
                  AND u.PasswordSalt IS NOT NULL
                  AND ISNULL(u.IsActive, 1) = 1
                  AND u.Password IS NULL
                  AND NOT EXISTS (SELECT 1 FROM dbo.DepartmentAccount da WHERE da.UserId = u.UserId)";

            int userId = 0;
            string fullName = string.Empty;
            string email = string.Empty;
            bool isDeveloper = false;
            int levelRank = 0;
            byte[]? storedHash = null, storedSalt = null;

            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@Name", username);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    userId      = reader.GetInt32(0);
                    fullName    = reader.GetString(1);
                    email       = reader.GetString(2);
                    isDeveloper = reader.GetBoolean(3);
                    storedHash  = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4);
                    storedSalt  = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5);
                    levelRank   = reader.GetInt32(6);
                }
            }

            if (userId == 0 || storedHash == null || storedSalt == null) return null;
            if (!VerifyPassword(password, storedHash, storedSalt)) return null;

            _logger.LogInformation("User '{Username}' authenticated via SHA256 (WinForms-created account, UserId={UserId})", username, userId);

            // Load employee data
            int? employeeId = null; string? employeeName = null, employeePosition = null;
            int? companyId = null; string? companyName = null;
            int? branchId = null; string? branchName = null;
            int? departmentId = null; string? departmentName = null;

            const string empSql = @"
                SELECT e.EmpId, e.Name, e.Position,
                       e.ComId, c.Name, e.BranchId, b.Name, e.DeptId, d.Name
                FROM dbo.[User] u
                INNER JOIN dbo.Employee e ON u.EmpId = e.EmpId
                LEFT JOIN dbo.Company    c ON e.ComId    = c.ComId
                LEFT JOIN dbo.Branch     b ON e.BranchId = b.BranchId
                LEFT JOIN dbo.Department d ON e.DeptId   = d.DeptId
                WHERE u.UserId = @UserId AND e.Active = 1";

            using (var cmd = new SqlCommand(empSql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    employeeId       = reader.GetInt32(0);
                    employeeName     = reader.GetString(1);
                    employeePosition = reader.IsDBNull(2) ? null : reader.GetString(2);
                    companyId        = reader.IsDBNull(3) ? null : reader.GetInt32(3);
                    companyName      = reader.IsDBNull(4) ? null : reader.GetString(4);
                    branchId         = reader.IsDBNull(5) ? null : reader.GetInt32(5);
                    branchName       = reader.IsDBNull(6) ? null : reader.GetString(6);
                    departmentId     = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                    departmentName   = reader.IsDBNull(8) ? null : reader.GetString(8);
                }
            }

            // Load roles
            var roles = new List<string>();
            const string rolesSql = @"
                SELECT r.RoleName FROM dbo.UserRole ur
                INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                WHERE ur.UserId = @UserId AND r.IsActive = 1 ORDER BY r.RoleName";
            using (var cmd = new SqlCommand(rolesSql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync()) roles.Add(reader.GetString(0));
                if (roles.Count == 0) roles.Add("Requester");
            }

            return new AuthResult
            {
                Success          = true,
                UserId           = userId,
                Name             = fullName,
                Email            = email,
                IsDeveloper      = isDeveloper,
                LevelRank        = levelRank,
                EmployeeId       = employeeId,
                EmployeeName     = employeeName,
                EmployeePosition = employeePosition,
                CompanyId        = companyId,
                CompanyName      = companyName,
                BranchId         = branchId,
                BranchName       = branchName,
                DepartmentId     = departmentId,
                DepartmentName   = departmentName,
                Roles            = roles
            };
        }

        /// <summary>
        /// Handles all department account login scenarios when CONVERT VARBINARY auth fails.
        ///
        /// Path A — UserId IS NOT NULL (account created/set-up via DepartmentAccountsPage "Set Password"):
        ///   dbo.[User].Password is NULL, but PasswordHash/PasswordSalt hold the SHA256 hash.
        ///   Verify SHA256 → load dept account data → return AuthResult (no migration needed).
        ///
        /// Path B — UserId IS NULL (account seeded but never logged in; migrate on first login):
        ///   Verify SHA256 from DepartmentAccount.PasswordHash/Salt → create dbo.[User] row
        ///   (CONVERT VARBINARY so future logins hit Path 1 CONVERT auth) → link UserId → return.
        /// </summary>
        private async Task<AuthResult?> TryDeptAccountLoginAsync(string username, string password, SqlConnection con)
        {
            try
            {
                // Load dept account row (both UserId IS NULL and NOT NULL cases)
                const string selectSql = @"
                    SELECT
                        da.Id,
                        da.UserId,
                        da.PasswordHash,
                        da.PasswordSalt,
                        da.IsActive,
                        c.ComId,    c.Name AS CompanyName,
                        d.DeptId,   d.Name AS DeptName,
                        b.BranchId, b.Name AS BranchName
                    FROM dbo.DepartmentAccount da
                    LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
                    LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
                    LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
                    WHERE da.Username = @Username";

                int accountId;
                int? linkedUserId;
                byte[]? daHash, daSalt;
                bool isActive;
                int? companyId, deptId, branchId;
                string? companyName, deptName, branchName;

                using (var cmd = new SqlCommand(selectSql, con))
                {
                    cmd.Parameters.AddWithValue("@Username", username);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (!await reader.ReadAsync()) return null;

                    accountId    = reader.GetInt32(0);
                    linkedUserId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                    daHash       = reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2);
                    daSalt       = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
                    isActive     = !reader.IsDBNull(4) && reader.GetBoolean(4);
                    companyId    = reader.IsDBNull(5)  ? null : reader.GetInt32(5);
                    companyName  = reader.IsDBNull(6)  ? null : reader.GetString(6);
                    deptId       = reader.IsDBNull(7)  ? null : reader.GetInt32(7);
                    deptName     = reader.IsDBNull(8)  ? null : reader.GetString(8);
                    branchId     = reader.IsDBNull(9)  ? null : reader.GetInt32(9);
                    branchName   = reader.IsDBNull(10) ? null : reader.GetString(10);
                }

                if (!isActive) return null;

                // ── Path A: already has a linked User row ─────────────────────────────
                if (linkedUserId.HasValue)
                {
                    // The User row stores PasswordHash/PasswordSalt (set by DepartmentAccountsPage).
                    // Verify against those.
                    byte[]? userHash = null, userSalt = null;
                    const string userSql = @"
                        SELECT PasswordHash, PasswordSalt, ISNULL(IsActive, 1)
                        FROM dbo.[User]
                        WHERE UserId = @UserId";

                    using (var cmd = new SqlCommand(userSql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", linkedUserId.Value);
                        using var reader = await cmd.ExecuteReaderAsync();
                        if (!await reader.ReadAsync()) return null;
                        userHash = reader.IsDBNull(0) ? null : (byte[])reader.GetValue(0);
                        userSalt = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
                        bool userActive = !reader.IsDBNull(2) && reader.GetBoolean(2);
                        if (!userActive) return null;
                    }

                    if (userHash == null || userSalt == null) return null;
                    if (!VerifyPassword(password, userHash, userSalt)) return null;

                    _logger.LogInformation("Dept account '{Username}' authenticated via SHA256 (Path A, UserId={UserId})",
                        username, linkedUserId.Value);

                    return await BuildDeptAuthResultAsync(con, linkedUserId.Value, username,
                        accountId, companyId, companyName, deptId, deptName, branchId, branchName);
                }

                // ── Path B: UserId IS NULL — first-time login, migrate ─────────────────
                if (daHash == null || daSalt == null) return null;
                if (!VerifyPassword(password, daHash, daSalt)) return null;

                // Credentials match — create dbo.[User] row with CONVERT VARBINARY so
                // subsequent logins via the CONVERT auth path work too.
                using var transaction = con.BeginTransaction();
                try
                {
                    byte[] newSalt = GenerateSalt();
                    byte[] newHash = VerifyPasswordHash(password, newSalt);

                    const string insertUserSql = @"
                        INSERT INTO dbo.[User] (Name, Password, PasswordHash, PasswordSalt, IsActive, DateCreated)
                        VALUES (@Name, CONVERT(VARBINARY(MAX), @Password), @Hash, @Salt, 1, GETDATE());
                        SELECT CAST(SCOPE_IDENTITY() AS INT);";

                    int newUserId;
                    using (var userCmd = new SqlCommand(insertUserSql, con, transaction))
                    {
                        userCmd.Parameters.AddWithValue("@Name", username);
                        userCmd.Parameters.AddWithValue("@Password", password);
                        userCmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, newHash.Length) { Value = newHash });
                        userCmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, newSalt.Length) { Value = newSalt });
                        newUserId = (int)(await userCmd.ExecuteScalarAsync())!;
                    }

                    const string linkSql = "UPDATE dbo.DepartmentAccount SET UserId = @UserId WHERE Id = @Id";
                    using (var linkCmd = new SqlCommand(linkSql, con, transaction))
                    {
                        linkCmd.Parameters.AddWithValue("@UserId", newUserId);
                        linkCmd.Parameters.AddWithValue("@Id", accountId);
                        await linkCmd.ExecuteNonQueryAsync();
                    }

                    transaction.Commit();

                    _logger.LogInformation("Migrated dept account '{Username}' (Id={AccountId}) → UserId={UserId} (Path B)",
                        username, accountId, newUserId);

                    return await BuildDeptAuthResultAsync(con, newUserId, username,
                        accountId, companyId, companyName, deptId, deptName, branchId, branchName);
                }
                catch
                {
                    transaction.Rollback();
                    throw;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "TryDeptAccountLoginAsync failed for '{Username}'", username);
                return null;
            }
        }

        private async Task<AuthResult> BuildDeptAuthResultAsync(
            SqlConnection con, int userId, string username,
            int accountId,
            int? companyId, string? companyName,
            int? deptId,    string? deptName,
            int? branchId,  string? branchName)
        {
            // Load roles
            var roles = new List<string> { "Requester" };
            const string rolesSql = @"
                SELECT r.RoleName FROM dbo.UserRole ur
                INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                WHERE ur.UserId = @UserId AND r.IsActive = 1";
            using (var cmd = new SqlCommand(rolesSql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                roles.Clear();
                while (await reader.ReadAsync()) roles.Add(reader.GetString(0));
                if (roles.Count == 0) roles.Add("Requester");
            }

            return new AuthResult
            {
                Success = true,
                UserId  = userId,
                Name    = username,
                Roles   = roles,
                IsDepartmentAccountSession      = true,
                DepartmentAccountId             = accountId,
                DepartmentAccountCompanyId      = companyId,
                DepartmentAccountCompanyName    = companyName,
                DepartmentAccountDepartmentId   = deptId,
                DepartmentAccountDepartmentName = deptName,
                DepartmentAccountBranchId       = branchId,
                DepartmentAccountBranchName     = branchName
            };
        }

        private static byte[] GenerateSalt()
        {
            byte[] salt = new byte[16];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(salt);
            return salt;
        }

        private static byte[] VerifyPasswordHash(string password, byte[] salt)
        {
            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            byte[] combined      = new byte[passwordBytes.Length + salt.Length];
            Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
            Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(combined);
        }

        private static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
        {
            byte[] passwordBytes  = Encoding.UTF8.GetBytes(password);
            byte[] combinedBytes  = new byte[passwordBytes.Length + storedSalt.Length];
            Buffer.BlockCopy(passwordBytes, 0, combinedBytes, 0, passwordBytes.Length);
            Buffer.BlockCopy(storedSalt,    0, combinedBytes, passwordBytes.Length, storedSalt.Length);

            using var sha256 = SHA256.Create();
            byte[] computed = sha256.ComputeHash(combinedBytes);

            if (computed.Length != storedHash.Length) return false;
            for (int i = 0; i < computed.Length; i++)
                if (computed[i] != storedHash[i]) return false;
            return true;
        }

        public async Task<UserAccountDto?> GetAccountAsync(int userId)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();

            const string sql = @"
                SELECT u.UserId, u.Name, ISNULL(u.EmailAddress,'') AS EmailAddress,
                       ISNULL(u.IsDeveloper,0) AS IsDeveloper,
                       ISNULL(u.IsActive,1) AS IsActive,
                       e.Name AS EmployeeName
                FROM dbo.[User] u
                LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                WHERE u.UserId = @UserId";

            UserAccountDto? dto = null;
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    dto = new UserAccountDto
                    {
                        UserId       = reader.GetInt32(0),
                        Name         = reader.GetString(1),
                        EmailAddress = reader.GetString(2),
                        IsDeveloper  = reader.GetBoolean(3),
                        IsActive     = reader.GetBoolean(4),
                        EmployeeName = reader.IsDBNull(5) ? null : reader.GetString(5)
                    };
                }
            }

            if (dto == null) return null;

            const string rolesSql = @"
                SELECT r.RoleName FROM dbo.UserRole ur
                INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                WHERE ur.UserId = @UserId AND r.IsActive = 1
                ORDER BY r.RoleName";

            using (var cmd = new SqlCommand(rolesSql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    dto.Roles.Add(reader.GetString(0));
            }

            return dto;
        }

        public async Task<bool> UpdateEmailAsync(int userId, string email)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();

            const string sql = "UPDATE dbo.[User] SET EmailAddress = @Email WHERE UserId = @UserId";
            using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.AddWithValue("@Email", email);
            cmd.Parameters.AddWithValue("@UserId", userId);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();

            // Verify current password (CONVERT VARBINARY method used by normal accounts)
            const string verifySql = @"
                SELECT COUNT(1) FROM dbo.[User]
                WHERE UserId = @UserId AND [Password] = CONVERT(VARBINARY(MAX), @Password)";

            bool verified = false;
            using (var cmd = new SqlCommand(verifySql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Password", currentPassword);
                var count = (int)(await cmd.ExecuteScalarAsync())!;
                verified = count > 0;
            }

            // If CONVERT auth fails, try SHA256 hash (dept accounts / migrated accounts)
            if (!verified)
            {
                const string hashSql = @"
                    SELECT PasswordHash, PasswordSalt FROM dbo.[User]
                    WHERE UserId = @UserId AND PasswordHash IS NOT NULL";

                byte[]? storedHash = null, storedSalt = null;
                using (var cmd = new SqlCommand(hashSql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using var reader = await cmd.ExecuteReaderAsync();
                    if (await reader.ReadAsync())
                    {
                        storedHash = reader.IsDBNull(0) ? null : (byte[])reader.GetValue(0);
                        storedSalt = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
                    }
                }

                if (storedHash != null && storedSalt != null)
                    verified = VerifyPassword(currentPassword, storedHash, storedSalt);
            }

            if (!verified) return false;

            // Update to new password (both CONVERT VARBINARY + SHA256 hash)
            byte[] newSalt = GenerateSalt();
            byte[] newHash = VerifyPasswordHash(newPassword, newSalt);

            const string updateSql = @"
                UPDATE dbo.[User]
                SET [Password] = CONVERT(VARBINARY(MAX), @NewPassword),
                    PasswordHash = @Hash,
                    PasswordSalt = @Salt
                WHERE UserId = @UserId";

            using var updateCmd = new SqlCommand(updateSql, con);
            updateCmd.Parameters.AddWithValue("@NewPassword", newPassword);
            updateCmd.Parameters.Add(new SqlParameter("@Hash", System.Data.SqlDbType.VarBinary, newHash.Length) { Value = newHash });
            updateCmd.Parameters.Add(new SqlParameter("@Salt", System.Data.SqlDbType.VarBinary, newSalt.Length) { Value = newSalt });
            updateCmd.Parameters.AddWithValue("@UserId", userId);
            return await updateCmd.ExecuteNonQueryAsync() > 0;
        }
    }
}
