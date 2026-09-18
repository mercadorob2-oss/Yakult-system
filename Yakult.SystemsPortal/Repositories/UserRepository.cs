using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

/// <summary>
/// Repository for user authentication against the Yakult database.
/// Ported from Inventory.RequestPortal.Repositories.UserRepository.
/// Supports 3-tier auth: CONVERT VARBINARY, SHA256 hash/salt, and department accounts.
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogger<UserRepository> _logger;

    public UserRepository(IConnectionStringProvider connectionStringProvider, ILogger<UserRepository> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _logger = logger;
    }

    public async Task<AuthResult> AuthenticateAsync(string username, string password)
    {
        try
        {
            using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
            await con.OpenAsync();

            _logger.LogInformation("Authenticating user '{Username}' against database: {Database} on {Server}",
                username, con.Database, con.DataSource);

            // Step 1: Try CONVERT VARBINARY auth (legacy and current standard)
            const string authSql = @"
                SELECT u.UserId, u.Name, u.EmailAddress,
                       ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                       CASE
                           WHEN u.IsDeveloper = 1 THEN 999
                           WHEN al_override.LevelRank IS NOT NULL THEN al_override.LevelRank
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
                LEFT JOIN dbo.Employee     e           ON u.EmpId = e.EmpId
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
                    userId = reader.GetInt32(0);
                    fullName = reader.GetString(1);
                    email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                    isDeveloper = reader.GetBoolean(3);
                    levelRank = reader.GetInt32(4);
                }
            }

            if (userId == 0)
            {
                // Path 1: Regular User account with SHA256 hash/salt only
                var regularUserResult = await TryRegularUserSha256LoginAsync(username, password, con);
                if (regularUserResult != null) return regularUserResult;

                // Path 2: Department accounts
                _logger.LogInformation("Normal auth failed for '{Username}', trying dept account SHA256 paths", username);
                var deptResult = await TryDeptAccountLoginAsync(username, password, con);
                if (deptResult != null) return deptResult;

                _logger.LogWarning("Authentication failed for user '{Username}' - not found in database {Database}",
                    username, con.Database);
                return new AuthResult
                {
                    Success = false,
                    ErrorMessage = "Invalid username or password."
                };
            }

            // Load employee data and roles for normal user
            return await BuildAuthResultAsync(con, userId, fullName, email, isDeveloper, levelRank, isDeptAccount: false);
        }
        catch (SqlException sqlEx)
        {
            throw new InvalidOperationException(
                $"Database error during authentication: {sqlEx.Message} (Error {sqlEx.Number})", sqlEx);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Authentication failed: {ex.Message}", ex);
        }
    }

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
                userId = reader.GetInt32(0);
                fullName = reader.GetString(1);
                email = reader.GetString(2);
                isDeveloper = reader.GetBoolean(3);
                storedHash = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4);
                storedSalt = reader.IsDBNull(5) ? null : (byte[])reader.GetValue(5);
                levelRank = reader.GetInt32(6);
            }
        }

        if (userId == 0 || storedHash == null || storedSalt == null) return null;
        if (!VerifyPassword(password, storedHash, storedSalt)) return null;

        _logger.LogInformation("User '{Username}' authenticated via SHA256 (WinForms-created account, UserId={UserId})", username, userId);

        return await BuildAuthResultAsync(con, userId, fullName, email, isDeveloper, levelRank, isDeptAccount: false);
    }

    private async Task<AuthResult?> TryDeptAccountLoginAsync(string username, string password, SqlConnection con)
    {
        try
        {
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
                LEFT JOIN dbo.Company    c ON c.Name = da.CompanyName
                LEFT JOIN dbo.Department d ON d.Name = da.DepartmentName
                LEFT JOIN dbo.Branch     b ON b.Name = da.BranchName
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

                accountId = reader.GetInt32(0);
                linkedUserId = reader.IsDBNull(1) ? null : reader.GetInt32(1);
                daHash = reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2);
                daSalt = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
                isActive = !reader.IsDBNull(4) && reader.GetBoolean(4);
                companyId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
                companyName = reader.IsDBNull(6) ? null : reader.GetString(6);
                deptId = reader.IsDBNull(7) ? null : reader.GetInt32(7);
                deptName = reader.IsDBNull(8) ? null : reader.GetString(8);
                branchId = reader.IsDBNull(9) ? null : reader.GetInt32(9);
                branchName = reader.IsDBNull(10) ? null : reader.GetString(10);
            }

            if (!isActive) return null;

            // Path A: already has a linked User row
            if (linkedUserId.HasValue)
            {
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

            // Path B: UserId IS NULL - first-time login, migrate
            if (daHash == null || daSalt == null) return null;
            if (!VerifyPassword(password, daHash, daSalt)) return null;

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

                _logger.LogInformation("Migrated dept account '{Username}' (Id={AccountId}) -> UserId={UserId} (Path B)",
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

    private async Task<AuthResult> BuildAuthResultAsync(SqlConnection con, int userId, string fullName, string email,
        bool isDeveloper, int levelRank, bool isDeptAccount)
    {
        int? employeeId = null;
        string? employeeName = null;
        string? employeePosition = null;
        int? companyId = null;
        string? companyName = null;
        int? branchId = null;
        string? branchName = null;
        int? departmentId = null;
        string? departmentName = null;

        if (!isDeptAccount)
        {
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
        }

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

        if (isDeptAccount && !roles.Any(r => r.Equals("Requester", StringComparison.OrdinalIgnoreCase)))
            roles.Add("Requester");

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
            Roles = roles
        };
    }

    private async Task<AuthResult> BuildDeptAuthResultAsync(
        SqlConnection con, int userId, string username,
        int accountId,
        int? companyId, string? companyName,
        int? deptId, string? deptName,
        int? branchId, string? branchName)
    {
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
            UserId = userId,
            Name = username,
            Roles = roles,
            IsDepartmentAccountSession = true,
            DepartmentAccountId = accountId,
            DepartmentAccountCompanyId = companyId,
            DepartmentAccountCompanyName = companyName,
            DepartmentAccountDepartmentId = deptId,
            DepartmentAccountDepartmentName = deptName,
            DepartmentAccountBranchId = branchId,
            DepartmentAccountBranchName = branchName
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
        byte[] combined = new byte[passwordBytes.Length + salt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
        using var sha256 = SHA256.Create();
        return sha256.ComputeHash(combined);
    }

    private static bool VerifyPassword(string password, byte[] storedHash, byte[] storedSalt)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] combinedBytes = new byte[passwordBytes.Length + storedSalt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combinedBytes, 0, passwordBytes.Length);
        Buffer.BlockCopy(storedSalt, 0, combinedBytes, passwordBytes.Length, storedSalt.Length);

        using var sha256 = SHA256.Create();
        byte[] computed = sha256.ComputeHash(combinedBytes);

        if (computed.Length != storedHash.Length) return false;
        for (int i = 0; i < computed.Length; i++)
            if (computed[i] != storedHash[i]) return false;
        return true;
    }
}
