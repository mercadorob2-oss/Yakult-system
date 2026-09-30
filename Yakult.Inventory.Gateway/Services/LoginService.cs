using System.Data;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Security;

namespace Yakult.Inventory.Gateway.Services;

/// <summary>
/// Server-side port of the desktop sign-in (LoginPage + UserRepository
/// .AuthenticateByName_VarBinaryConvertAsync). Keep the three paths in the
/// same order as the desktop:
///   1. dbo.[User] (hash/salt, or legacy CONVERT(VARBINARY) auto-migrated)
///   2. department account already linked to a dbo.[User] row
///   3. legacy department account (UserId IS NULL), linked on first login
/// </summary>
public sealed class LoginService
{
    private readonly SqlConnectionFactory _db;
    private readonly ILogger<LoginService> _logger;

    public LoginService(SqlConnectionFactory db, ILogger<LoginService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<GatewaySession?> SignInAsync(GatewayEnvironment environment, string userName, string password, CancellationToken ct)
    {
        // Each environment has its own users: the password is checked against the chosen database.
        await using var con = await _db.OpenAsync(environment, ct);

        var session = await TryUserLoginAsync(con, userName, password, ct)
                   ?? await TryLinkedDeptAccountLoginAsync(con, userName, password, ct)
                   ?? await TryLegacyDeptAccountLoginAsync(con, userName, password, ct);

        if (session is null)
            return null;

        session.Permissions = await LoadPermissionsAsync(con, session.UserId, ct);
        return session;
    }

    // ── 1. Normal user account ───────────────────────────────────────────────

    private async Task<GatewaySession?> TryUserLoginAsync(SqlConnection con, string name, string password, CancellationToken ct)
    {
        const string fetchSql = @"
            SELECT UserId, Name, EmailAddress,
                   ISNULL(IsDeveloper, 0)  AS IsDeveloper,
                   ISNULL(IsSuperAdmin, 0) AS IsSuperAdmin,
                   PasswordHash, PasswordSalt,
                   CASE WHEN [Password] IS NOT NULL THEN 1 ELSE 0 END AS HasLegacyPassword
            FROM dbo.[User]
            WHERE Name COLLATE Latin1_General_CS_AS = @Name COLLATE Latin1_General_CS_AS
              AND ISNULL(IsActive, 1) = 1;";

        int userId; string fullName; string? email; bool isDeveloper, isSuperAdmin;
        byte[]? storedHash, storedSalt; bool hasLegacyPassword;

        await using (var cmd = new SqlCommand(fetchSql, con))
        {
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = name;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            userId            = reader.GetInt32(0);
            fullName          = reader.GetString(1);
            email             = reader.IsDBNull(2) ? "" : reader.GetString(2);
            isDeveloper       = reader.GetBoolean(3);
            isSuperAdmin      = reader.GetBoolean(4);
            storedHash        = reader.IsDBNull(5) ? null : (byte[])reader[5];
            storedSalt        = reader.IsDBNull(6) ? null : (byte[])reader[6];
            hasLegacyPassword = reader.GetInt32(7) == 1;
        }

        bool valid = false;

        if (storedHash != null && storedSalt != null)
        {
            valid = PasswordHasher.Verify(password, storedHash, storedSalt);
            if (valid)
            {
                // Scrub the legacy plaintext column if it still exists.
                await using var cmd = new SqlCommand(
                    "UPDATE dbo.[User] SET [Password] = NULL WHERE UserId = @UserId AND [Password] IS NOT NULL", con);
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }
        else if (hasLegacyPassword)
        {
            // NVARCHAR on purpose: the legacy column holds CONVERT(VARBINARY, N'...') bytes,
            // which is what the desktop's AddWithValue(string) produced.
            await using (var cmd = new SqlCommand(@"
                SELECT COUNT(1) FROM dbo.[User]
                WHERE UserId = @UserId AND [Password] = CONVERT(VARBINARY(MAX), @Password)", con))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@Password", SqlDbType.NVarChar, -1).Value = password;
                valid = Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) > 0;
            }

            if (valid)
            {
                var salt = PasswordHasher.GenerateSalt();
                var hash = PasswordHasher.Hash(password, salt);
                await using var cmd = new SqlCommand(@"
                    UPDATE dbo.[User]
                    SET PasswordHash = @Hash, PasswordSalt = @Salt, [Password] = NULL
                    WHERE UserId = @UserId", con);
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                cmd.Parameters.Add("@Hash", SqlDbType.VarBinary, hash.Length).Value = hash;
                cmd.Parameters.Add("@Salt", SqlDbType.VarBinary, salt.Length).Value = salt;
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        if (!valid)
            return null;

        var session = new GatewaySession
        {
            UserId       = userId,
            UserName     = fullName,
            Email        = email,
            IsDeveloper  = isDeveloper,
            IsSuperAdmin = isSuperAdmin,
            Roles        = await LoadRolesAsync(con, userId, isDeveloper, ct),
            Employee     = await LoadEmployeeAsync(con, userId, ct),
            DepartmentAccount    = await LoadDepartmentAccountByUserAsync(con, userId, ct),
            NotificationsEnabled = await LoadNotificationsEnabledAsync(con, userId, ct)
        };
        return session;
    }

    // ── 2. Department account linked to a User row ───────────────────────────

    private async Task<GatewaySession?> TryLinkedDeptAccountLoginAsync(SqlConnection con, string username, string password, CancellationToken ct)
    {
        const string sql = @"
            SELECT da.Id, da.IsActive, u.UserId, u.PasswordHash, u.PasswordSalt,
                   c.ComId, d.DeptId, b.BranchId
            FROM dbo.DepartmentAccount da
            INNER JOIN dbo.[User] u ON u.UserId = da.UserId
            LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
            LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
            LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
            WHERE da.Username COLLATE Latin1_General_CS_AS = @Username COLLATE Latin1_General_CS_AS
              AND da.UserId IS NOT NULL";

        try
        {
            await using var cmd = new SqlCommand(sql, con);
            cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 256).Value = username;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            if (!await reader.ReadAsync(ct))
                return null;

            var accountId = reader.GetInt32(0);
            var isActive  = !reader.IsDBNull(1) && reader.GetBoolean(1);
            var userId    = reader.GetInt32(2);
            var hash      = reader.IsDBNull(3) ? null : (byte[])reader.GetValue(3);
            var salt      = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4);
            var account   = new DepartmentAccountInfo
            {
                AccountId    = accountId,
                CompanyId    = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                DepartmentId = reader.IsDBNull(6) ? null : reader.GetInt32(6),
                BranchId     = reader.IsDBNull(7) ? null : reader.GetInt32(7)
            };

            if (!isActive || !PasswordHasher.Verify(password, hash, salt))
                return null;

            return DeptAccountSession(userId, username, account);
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Linked department-account login lookup failed.");
            return null;
        }
    }

    // ── 3. Legacy department account (no User row yet) ──────────────────────

    private async Task<GatewaySession?> TryLegacyDeptAccountLoginAsync(SqlConnection con, string username, string password, CancellationToken ct)
    {
        const string selectSql = @"
            SELECT da.Id, da.PasswordHash, da.PasswordSalt, da.IsActive,
                   c.ComId, d.DeptId, b.BranchId
            FROM dbo.DepartmentAccount da
            LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
            LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
            LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
            WHERE da.Username COLLATE Latin1_General_CS_AS = @Username COLLATE Latin1_General_CS_AS
              AND da.UserId IS NULL";

        try
        {
            DepartmentAccountInfo account;
            byte[]? hash, salt;
            bool isActive;

            await using (var cmd = new SqlCommand(selectSql, con))
            {
                cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 256).Value = username;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                if (!await reader.ReadAsync(ct))
                    return null;

                account = new DepartmentAccountInfo
                {
                    AccountId    = reader.GetInt32(0),
                    CompanyId    = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    DepartmentId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    BranchId     = reader.IsDBNull(6) ? null : reader.GetInt32(6)
                };
                hash     = reader.IsDBNull(1) ? null : (byte[])reader.GetValue(1);
                salt     = reader.IsDBNull(2) ? null : (byte[])reader.GetValue(2);
                isActive = !reader.IsDBNull(3) && reader.GetBoolean(3);
            }

            if (!isActive || hash is null || salt is null || !PasswordHasher.Verify(password, hash, salt))
                return null;

            // Credentials match: create the User row and link it.
            await using var tx = (SqlTransaction)await con.BeginTransactionAsync(ct);
            try
            {
                int newUserId;
                await using (var userCmd = new SqlCommand(@"
                    INSERT INTO dbo.[User] (Name, PasswordHash, PasswordSalt, IsActive, DateCreated)
                    VALUES (@Name, @Hash, @Salt, 1, GETDATE());
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", con, tx))
                {
                    userCmd.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = username;
                    userCmd.Parameters.Add("@Hash", SqlDbType.VarBinary, hash.Length).Value = hash;
                    userCmd.Parameters.Add("@Salt", SqlDbType.VarBinary, salt.Length).Value = salt;
                    newUserId = (int)(await userCmd.ExecuteScalarAsync(ct))!;
                }

                await using (var linkCmd = new SqlCommand(
                    "UPDATE dbo.DepartmentAccount SET UserId = @UserId WHERE Id = @Id", con, tx))
                {
                    linkCmd.Parameters.Add("@UserId", SqlDbType.Int).Value = newUserId;
                    linkCmd.Parameters.Add("@Id", SqlDbType.Int).Value = account.AccountId;
                    await linkCmd.ExecuteNonQueryAsync(ct);
                }

                await tx.CommitAsync(ct);
                return DeptAccountSession(newUserId, username, account);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Legacy department-account login failed.");
            return null;
        }
    }

    private static GatewaySession DeptAccountSession(int userId, string username, DepartmentAccountInfo account) => new()
    {
        UserId            = userId,
        UserName          = username,
        Roles             = new List<string> { "Requester" },
        DepartmentAccount = account,
        IsDepartmentAccountLogin = true
    };

    // ── Session data ─────────────────────────────────────────────────────────

    private static async Task<List<string>> LoadRolesAsync(SqlConnection con, int userId, bool isDeveloper, CancellationToken ct)
    {
        var roles = new List<string>();
        await using (var cmd = new SqlCommand(@"
            SELECT r.RoleName
            FROM dbo.UserRole ur
            INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
            WHERE ur.UserId = @UserId AND r.IsActive = 1
            ORDER BY r.RoleName", con))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                roles.Add(reader.GetString(0));
        }

        if (isDeveloper && !roles.Any(r => r.Equals("Developer", StringComparison.OrdinalIgnoreCase)))
            roles.Add("Developer");

        if (roles.Count == 0)
            roles.Add("Requester");

        return roles;
    }

    private static async Task<EmployeeInfo?> LoadEmployeeAsync(SqlConnection con, int userId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            SELECT e.EmpId, e.Name, e.Position,
                   e.ComId,    c.Name AS CompanyName,
                   e.BranchId, b.Name AS BranchName,
                   e.DeptId,   d.Name AS DeptName
            FROM dbo.[User] u
            INNER JOIN dbo.Employee e ON u.EmpId = e.EmpId
            LEFT  JOIN dbo.Company    c ON e.ComId    = c.ComId
            LEFT  JOIN dbo.Branch     b ON e.BranchId = b.BranchId
            LEFT  JOIN dbo.Department d ON e.DeptId   = d.DeptId
            WHERE u.UserId = @UserId AND e.Active = 1", con);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new EmployeeInfo
        {
            EmployeeId     = reader.GetInt32(0),
            Name           = reader.GetString(1),
            Position       = reader.IsDBNull(2) ? null : reader.GetString(2),
            CompanyId      = reader.IsDBNull(3) ? null : reader.GetInt32(3),
            CompanyName    = reader.IsDBNull(4) ? null : reader.GetString(4),
            BranchId       = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            BranchName     = reader.IsDBNull(6) ? null : reader.GetString(6),
            DepartmentId   = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            DepartmentName = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }

    private static async Task<DepartmentAccountInfo?> LoadDepartmentAccountByUserAsync(SqlConnection con, int userId, CancellationToken ct)
    {
        await using var cmd = new SqlCommand(@"
            SELECT da.Id, c.ComId, d.DeptId, b.BranchId
            FROM dbo.DepartmentAccount da
            LEFT JOIN dbo.Company    c ON (da.ComId IS NOT NULL AND c.ComId = da.ComId) OR (da.ComId IS NULL AND c.Name = da.CompanyName)
            LEFT JOIN dbo.Department d ON (da.DeptId IS NOT NULL AND d.DeptId = da.DeptId) OR (da.DeptId IS NULL AND d.Name = da.DepartmentName)
            LEFT JOIN dbo.Branch     b ON (da.BranchId IS NOT NULL AND b.BranchId = da.BranchId) OR (da.BranchId IS NULL AND b.Name = da.BranchName)
            WHERE da.UserId = @UserId", con);
        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new DepartmentAccountInfo
        {
            AccountId    = reader.GetInt32(0),
            CompanyId    = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            DepartmentId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            BranchId     = reader.IsDBNull(3) ? null : reader.GetInt32(3)
        };
    }

    private async Task<bool> LoadNotificationsEnabledAsync(SqlConnection con, int userId, CancellationToken ct)
    {
        try
        {
            await using var cmd = new SqlCommand(
                "SELECT NotificationsEnabled FROM dbo.[UserNotificationSetting] WHERE UserId = @UserId", con);
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
            var result = await cmd.ExecuteScalarAsync(ct);
            return result is null || result == DBNull.Value || (bool)result;
        }
        catch (SqlException ex)
        {
            _logger.LogWarning(ex, "Notification setting lookup failed; defaulting to enabled.");
            return true;
        }
    }

    private async Task<PermissionSnapshot> LoadPermissionsAsync(SqlConnection con, int userId, CancellationToken ct)
    {
        var snapshot = new PermissionSnapshot();
        try
        {
            await using (var cmd = new SqlCommand(@"
                SELECT r.RoleName, p.PortalKey
                FROM dbo.RolePortalAccess rpa
                INNER JOIN dbo.Role   r ON r.RoleId   = rpa.RoleId
                INNER JOIN dbo.Portal p ON p.PortalId = rpa.PortalId
                WHERE r.IsActive = 1 AND p.IsActive = 1", con))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var role = reader.GetString(0);
                    if (!snapshot.RolePortals.TryGetValue(role, out var keys))
                        snapshot.RolePortals[role] = keys = new List<string>();
                    keys.Add(reader.GetString(1));
                }
            }

            await using (var cmd = new SqlCommand(@"
                SELECT p.PortalKey, upa.IsGranted
                FROM dbo.UserPortalAccess upa
                INNER JOIN dbo.Portal p ON p.PortalId = upa.PortalId
                WHERE p.IsActive = 1 AND upa.UserId = @UserId", con))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    snapshot.UserPortals[reader.GetString(0)] = reader.GetBoolean(1);
            }

            await using (var cmd = new SqlCommand(@"
                SELECT pi.PermissionType, pi.ItemKey, upi.IsGranted
                FROM dbo.UserPermissionItem upi
                INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = upi.PermissionItemId
                WHERE pi.IsActive = 1 AND upi.UserId = @UserId", con))
            {
                cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = userId;
                await using var reader = await cmd.ExecuteReaderAsync(ct);
                while (await reader.ReadAsync(ct))
                    Put(snapshot.UserItems, reader.GetString(0), reader.GetString(1), reader.GetBoolean(2));
            }

            await using (var cmd = new SqlCommand(@"
                SELECT r.RoleName, pi.PermissionType, pi.ItemKey, rpi.IsGranted
                FROM dbo.RolePermissionItem rpi
                INNER JOIN dbo.Role r ON r.RoleId = rpi.RoleId
                INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = rpi.PermissionItemId
                WHERE r.IsActive = 1 AND pi.IsActive = 1", con))
            await using (var reader = await cmd.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    var role = reader.GetString(0);
                    if (!snapshot.RoleItems.TryGetValue(role, out var byType))
                        snapshot.RoleItems[role] = byType = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);
                    Put(byType, reader.GetString(1), reader.GetString(2), reader.GetBoolean(3));
                }
            }
        }
        catch (SqlException ex)
        {
            // Same as the desktop: a failed load leaves the client on its hardcoded fallback.
            _logger.LogWarning(ex, "Permission load failed; client will use its fallback map.");
            return new PermissionSnapshot();
        }

        snapshot.Loaded = true;
        return snapshot;
    }

    private static void Put(Dictionary<string, Dictionary<string, bool>> byType, string type, string key, bool granted)
    {
        if (!byType.TryGetValue(type, out var byKey))
            byType[type] = byKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        byKey[key] = granted;
    }
}
