using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Repositories;

/// <summary>
/// Repository backing the Admin "Account Requests" page: reviewing employee
/// self-service account requests and managing existing user accounts (roles,
/// active status, password resets).
/// </summary>
public sealed class AccountAdministrationRepository : IAccountAdministrationRepository
{
    private const string RequesterRoleName = "Requester";

    private readonly IConnectionStringProvider _connectionStringProvider;
    private readonly ILogger<AccountAdministrationRepository> _logger;

    public AccountAdministrationRepository(
        IConnectionStringProvider connectionStringProvider,
        ILogger<AccountAdministrationRepository> logger)
    {
        _connectionStringProvider = connectionStringProvider;
        _logger = logger;
    }

    public async Task<AccountRequestsViewModel> GetAccountAdministrationDataAsync()
    {
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        await EnsureRequesterRoleSeededAsync(con, transaction: null);

        var requests = await LoadRequestsAsync(con);
        var users = await LoadUsersAsync(con);
        var roles = await LoadRoleNamesAsync(con);

        return new AccountRequestsViewModel
        {
            Requests = requests,
            Users = users,
            Roles = roles
        };
    }

    public async Task ApproveAccountRequestAsync(int accountRequestId, int? reviewedByUserId, string? remarks)
    {
        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.Serializable);

        try
        {
            int empId;
            string requestedUsername;
            string workEmail;
            byte[]? passwordHash;
            byte[]? passwordSalt;

            const string selectSql = @"
                SELECT EmpId, RequestedUsername, WorkEmail, PasswordHash, PasswordSalt, Status
                FROM dbo.AccountRequest WITH (UPDLOCK, ROWLOCK)
                WHERE AccountRequestId = @AccountRequestId;";

            using (var cmd = new SqlCommand(selectSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@AccountRequestId", accountRequestId);
                using var reader = await cmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                    throw new InvalidOperationException("The account request could not be found.");

                var status = reader.GetString(5);
                if (!string.Equals(status, "Pending", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("This account request has already been reviewed.");

                empId = reader.GetInt32(0);
                requestedUsername = reader.GetString(1);
                workEmail = reader.GetString(2);
                passwordHash = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
                passwordSalt = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4);
            }

            const string duplicateSql = @"
                SELECT CASE
                    WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE EmpId = @EmpId) THEN 'employee'
                    WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE LOWER(Name) = LOWER(@Username)) THEN 'username'
                    WHEN EXISTS (SELECT 1 FROM dbo.[User] WHERE LOWER(EmailAddress) = LOWER(@WorkEmail)) THEN 'email'
                    ELSE ''
                END;";
            string duplicate;
            using (var cmd = new SqlCommand(duplicateSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@EmpId", empId);
                cmd.Parameters.AddWithValue("@Username", requestedUsername);
                cmd.Parameters.AddWithValue("@WorkEmail", workEmail);
                duplicate = Convert.ToString(await cmd.ExecuteScalarAsync()) ?? string.Empty;
            }

            if (!string.IsNullOrEmpty(duplicate))
                throw new InvalidOperationException($"Unable to approve: the {duplicate} is already registered to an existing account.");

            var requesterRoleId = await EnsureRequesterRoleSeededAsync(con, tx);

            const string insertUserSql = @"
                INSERT INTO dbo.[User] (Name, EmailAddress, EmpId, PasswordHash, PasswordSalt, IsActive, MustChangePassword, DateCreated)
                VALUES (@Name, @EmailAddress, @EmpId, @PasswordHash, @PasswordSalt, 1, 0, SYSUTCDATETIME());
                SELECT CAST(SCOPE_IDENTITY() AS INT);";
            int newUserId;
            using (var cmd = new SqlCommand(insertUserSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@Name", requestedUsername);
                cmd.Parameters.AddWithValue("@EmailAddress", workEmail);
                cmd.Parameters.AddWithValue("@EmpId", empId);
                if (passwordHash != null && passwordSalt != null)
                {
                    cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, passwordHash.Length).Value = passwordHash;
                    cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, passwordSalt.Length).Value = passwordSalt;
                }
                else
                {
                    cmd.Parameters.Add("@PasswordHash", SqlDbType.VarBinary, 0).Value = DBNull.Value;
                    cmd.Parameters.Add("@PasswordSalt", SqlDbType.VarBinary, 0).Value = DBNull.Value;
                }
                newUserId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            }

            const string insertRoleSql = @"
                INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned) VALUES (@UserId, @RoleId, GETDATE());";
            using (var cmd = new SqlCommand(insertRoleSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", newUserId);
                cmd.Parameters.AddWithValue("@RoleId", requesterRoleId);
                await cmd.ExecuteNonQueryAsync();
            }

            const string updateRequestSql = @"
                UPDATE dbo.AccountRequest
                SET Status = 'Approved', ReviewedAt = SYSUTCDATETIME(), ReviewedByUserId = @ReviewedByUserId,
                    ReviewRemarks = @Remarks, CreatedUserId = @CreatedUserId
                WHERE AccountRequestId = @AccountRequestId;";
            using (var cmd = new SqlCommand(updateRequestSql, con, tx))
            {
                cmd.Parameters.AddWithValue("@ReviewedByUserId", reviewedByUserId.HasValue ? reviewedByUserId.Value : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Remarks", string.IsNullOrWhiteSpace(remarks) ? (object)DBNull.Value : remarks.Trim());
                cmd.Parameters.AddWithValue("@CreatedUserId", newUserId);
                cmd.Parameters.AddWithValue("@AccountRequestId", accountRequestId);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            _logger.LogInformation("Account request {AccountRequestId} approved; created UserId {UserId}.", accountRequestId, newUserId);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task RejectAccountRequestAsync(int accountRequestId, int? reviewedByUserId, string remarks)
    {
        if (string.IsNullOrWhiteSpace(remarks))
            throw new ArgumentException("A rejection reason is required.", nameof(remarks));

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        const string sql = @"
            UPDATE dbo.AccountRequest
            SET Status = 'Rejected', ReviewedAt = SYSUTCDATETIME(), ReviewedByUserId = @ReviewedByUserId, ReviewRemarks = @Remarks
            WHERE AccountRequestId = @AccountRequestId AND Status = 'Pending';";
        using var cmd = new SqlCommand(sql, con);
        cmd.Parameters.AddWithValue("@ReviewedByUserId", reviewedByUserId.HasValue ? reviewedByUserId.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@Remarks", remarks.Trim());
        cmd.Parameters.AddWithValue("@AccountRequestId", accountRequestId);
        var rows = await cmd.ExecuteNonQueryAsync();

        if (rows == 0)
            throw new InvalidOperationException("This account request could not be rejected. It may have already been reviewed.");

        _logger.LogInformation("Account request {AccountRequestId} rejected.", accountRequestId);
    }

    public async Task UpdateManagedUserAsync(UpdateManagedUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
            throw new ArgumentException("A role must be selected.", nameof(request));

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();
        using var tx = (SqlTransaction)await con.BeginTransactionAsync(IsolationLevel.ReadCommitted);

        try
        {
            bool isDeveloper;
            using (var cmd = new SqlCommand("SELECT ISNULL(IsDeveloper, 0) FROM dbo.[User] WHERE UserId = @UserId;", con, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                var result = await cmd.ExecuteScalarAsync();
                if (result == null)
                    throw new InvalidOperationException("The user account could not be found.");
                isDeveloper = Convert.ToBoolean(result);
            }

            if (isDeveloper)
                throw new InvalidOperationException("Developer accounts are protected and cannot be modified here.");

            int? roleId;
            using (var cmd = new SqlCommand("SELECT TOP (1) RoleId FROM dbo.Role WHERE RoleName = @RoleName AND IsActive = 1;", con, tx))
            {
                cmd.Parameters.AddWithValue("@RoleName", request.RoleName.Trim());
                var result = await cmd.ExecuteScalarAsync();
                roleId = result == null ? null : Convert.ToInt32(result);
            }

            if (roleId == null)
                throw new InvalidOperationException($"The role '{request.RoleName}' could not be found.");

            using (var cmd = new SqlCommand("UPDATE dbo.[User] SET IsActive = @IsActive WHERE UserId = @UserId;", con, tx))
            {
                cmd.Parameters.AddWithValue("@IsActive", request.IsActive);
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand("DELETE FROM dbo.UserRole WHERE UserId = @UserId;", con, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                await cmd.ExecuteNonQueryAsync();
            }

            using (var cmd = new SqlCommand(
                "INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned) VALUES (@UserId, @RoleId, GETDATE());", con, tx))
            {
                cmd.Parameters.AddWithValue("@UserId", request.UserId);
                cmd.Parameters.AddWithValue("@RoleId", roleId.Value);
                await cmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
            _logger.LogInformation("Updated managed UserId {UserId}: IsActive={IsActive}, Role={RoleName}.",
                request.UserId, request.IsActive, request.RoleName);
        }
        catch
        {
            await tx.RollbackAsync();
            throw;
        }
    }

    public async Task<string?> ResetManagedUserPasswordAsync(ResetManagedUserPasswordRequest request)
    {
        var mode = (request.Mode ?? "temporary").Trim().ToLowerInvariant();
        if (mode != "temporary" && mode != "manual")
            throw new ArgumentException("Password reset mode must be 'temporary' or 'manual'.", nameof(request));

        string password;
        if (mode == "manual")
        {
            password = request.Password ?? string.Empty;
            if (password.Length < 8 || password.Length > 128)
                throw new ArgumentException("Password must contain between 8 and 128 characters.", nameof(request));
        }
        else
        {
            password = GenerateTemporaryPassword();
        }

        using var con = new SqlConnection(_connectionStringProvider.GetConnectionString());
        await con.OpenAsync();

        bool isDeveloper;
        bool isActive;
        using (var cmd = new SqlCommand("SELECT ISNULL(IsDeveloper, 0), ISNULL(IsActive, 1) FROM dbo.[User] WHERE UserId = @UserId;", con))
        {
            cmd.Parameters.AddWithValue("@UserId", request.UserId);
            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                throw new InvalidOperationException("The user account could not be found.");
            isDeveloper = reader.GetBoolean(0);
            isActive = reader.GetBoolean(1);
        }

        if (isDeveloper)
            throw new InvalidOperationException("Developer accounts are protected and cannot be modified here.");
        if (!isActive)
            throw new InvalidOperationException("Cannot reset the password for an inactive account.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        var combined = new byte[passwordBytes.Length + salt.Length];
        Buffer.BlockCopy(passwordBytes, 0, combined, 0, passwordBytes.Length);
        Buffer.BlockCopy(salt, 0, combined, passwordBytes.Length, salt.Length);
        var hash = SHA256.HashData(combined);

        const string sql = @"
            UPDATE dbo.[User]
            SET PasswordHash = @Hash, PasswordSalt = @Salt, [Password] = NULL,
                IsTemporaryPassword = @IsTemporary, MustChangePassword = @IsTemporary
            WHERE UserId = @UserId;";
        using var updateCmd = new SqlCommand(sql, con);
        updateCmd.Parameters.Add("@Hash", SqlDbType.VarBinary, hash.Length).Value = hash;
        updateCmd.Parameters.Add("@Salt", SqlDbType.VarBinary, salt.Length).Value = salt;
        updateCmd.Parameters.AddWithValue("@IsTemporary", mode == "temporary");
        updateCmd.Parameters.AddWithValue("@UserId", request.UserId);
        await updateCmd.ExecuteNonQueryAsync();

        _logger.LogInformation("Password reset ({Mode}) for UserId {UserId}.", mode, request.UserId);

        return mode == "temporary" ? password : null;
    }

    private async Task<List<AccountRequestListItem>> LoadRequestsAsync(SqlConnection con)
    {
        const string sql = @"
            SELECT
                ar.AccountRequestId, ar.EmpId, e.EmployeeNumber, e.Name AS EmployeeName,
                ar.WorkEmail, ar.RequestedUsername,
                ISNULL(c.Name, '') AS CompanyName, ISNULL(b.Name, '') AS BranchName,
                ISNULL(d.Name, '') AS DepartmentName, ISNULL(e.Position, '') AS Position,
                ar.Status, ar.SubmittedAt, ar.ReviewedAt, reviewer.Name AS ReviewedByName, ar.ReviewRemarks
            FROM dbo.AccountRequest ar
            INNER JOIN dbo.Employee e ON e.EmpId = ar.EmpId
            LEFT JOIN dbo.Company c ON c.ComId = e.ComId
            LEFT JOIN dbo.Branch b ON b.BranchId = e.BranchId
            LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
            LEFT JOIN dbo.[User] reviewer ON reviewer.UserId = ar.ReviewedByUserId
            ORDER BY ar.SubmittedAt DESC;";

        var results = new List<AccountRequestListItem>();
        using var cmd = new SqlCommand(sql, con);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new AccountRequestListItem
            {
                AccountRequestId = reader.GetInt32(0),
                EmployeeId = reader.GetInt32(1),
                EmployeeNumber = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                EmployeeName = reader.GetString(3),
                WorkEmail = reader.GetString(4),
                Username = reader.GetString(5),
                Company = reader.GetString(6),
                Branch = reader.GetString(7),
                Department = reader.GetString(8),
                Position = reader.GetString(9),
                Status = reader.GetString(10),
                SubmittedAt = DateTime.SpecifyKind(reader.GetDateTime(11), DateTimeKind.Utc),
                ReviewedAt = reader.IsDBNull(12) ? null : DateTime.SpecifyKind(reader.GetDateTime(12), DateTimeKind.Utc),
                ReviewedBy = reader.IsDBNull(13) ? null : reader.GetString(13),
                ReviewRemarks = reader.IsDBNull(14) ? null : reader.GetString(14)
            });
        }

        return results;
    }

    private async Task<List<ManagedUserListItem>> LoadUsersAsync(SqlConnection con)
    {
        const string sql = @"
            SELECT
                u.UserId, u.Name, ISNULL(u.EmailAddress, '') AS Email, ISNULL(e.Name, '') AS EmployeeName,
                ISNULL(u.IsActive, 1) AS IsActive, ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                ISNULL(u.MustChangePassword, 0) AS MustChangePassword,
                ISNULL((
                    SELECT STRING_AGG(r.RoleName, ', ')
                    FROM dbo.UserRole ur
                    INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
                    WHERE ur.UserId = u.UserId AND r.IsActive = 1
                ), '') AS Roles
            FROM dbo.[User] u
            LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
            ORDER BY u.Name;";

        var results = new List<ManagedUserListItem>();
        using var cmd = new SqlCommand(sql, con);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(new ManagedUserListItem
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                EmployeeName = reader.GetString(3),
                IsActive = reader.GetBoolean(4),
                IsDeveloper = reader.GetBoolean(5),
                MustChangePassword = reader.GetBoolean(6),
                Roles = reader.GetString(7)
            });
        }

        return results;
    }

    private async Task<List<string>> LoadRoleNamesAsync(SqlConnection con)
    {
        var roles = new List<string>();
        using var cmd = new SqlCommand("SELECT RoleName FROM dbo.Role WHERE IsActive = 1 ORDER BY RoleName;", con);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            roles.Add(reader.GetString(0));

        if (roles.Count == 0)
            roles.Add(RequesterRoleName);

        return roles;
    }

    /// <summary>
    /// Ensures the baseline "Requester" role exists (mirrors the idempotent
    /// seed pattern used by other portal migrations) and returns its RoleId.
    /// </summary>
    private async Task<int> EnsureRequesterRoleSeededAsync(SqlConnection con, SqlTransaction? transaction)
    {
        const string selectSql = "SELECT RoleId FROM dbo.Role WHERE RoleName = @RoleName;";
        using (var cmd = new SqlCommand(selectSql, con, transaction))
        {
            cmd.Parameters.AddWithValue("@RoleName", RequesterRoleName);
            var result = await cmd.ExecuteScalarAsync();
            if (result != null)
                return Convert.ToInt32(result);
        }

        const string insertSql = @"
            INSERT INTO dbo.Role (RoleName, Description, IsActive, DateCreated)
            VALUES (@RoleName, 'Default access for approved employee accounts.', 1, GETDATE());
            SELECT CAST(SCOPE_IDENTITY() AS INT);";
        using var insertCmd = new SqlCommand(insertSql, con, transaction);
        insertCmd.Parameters.AddWithValue("@RoleName", RequesterRoleName);
        return Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
    }

    private static string GenerateTemporaryPassword()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%";
        var bytes = RandomNumberGenerator.GetBytes(14);
        var builder = new StringBuilder(14);
        foreach (var b in bytes)
            builder.Append(chars[b % chars.Length]);
        return builder.ToString();
    }
}
