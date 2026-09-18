using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Session;
namespace Yakult.Inventory.App.Data
{
    public class UserRepository
    {
        private readonly string _connectionString;

        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>
        /// AUTH OPTION A (your current setup):
        /// Password column is VARBINARY but stores plaintext converted via CONVERT(VARBINARY, 'text').
        /// Example seed:
        /// INSERT INTO [User](Name, EmailAddress, [Password])
        /// VALUES (N'it_user', N'it@yakult.local', CONVERT(VARBINARY(MAX), N'12345'));
        /// </summary>
        public async Task<(int userId, string name, string email, bool isDeveloper, bool isSuperAdmin)> AuthenticateByName_VarBinaryConvertAsync(string name, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                // Fetch the user row first; verify password in C# so we can auto-migrate legacy accounts.
                const string fetchSql = @"
                    SELECT UserId, Name, EmailAddress,
                           ISNULL(IsDeveloper, 0)  AS IsDeveloper,
                           ISNULL(IsSuperAdmin, 0) AS IsSuperAdmin,
                           PasswordHash, PasswordSalt,
                           CASE WHEN [Password] IS NOT NULL THEN 1 ELSE 0 END AS HasLegacyPassword
                    FROM dbo.[User]
                    WHERE Name COLLATE Latin1_General_CS_AS = @Name COLLATE Latin1_General_CS_AS
                      AND ISNULL(IsActive, 1) = 1;";

                int userId; string fullName, email; bool isDeveloper, isSuperAdmin;
                byte[] storedHash, storedSalt; bool hasLegacyPassword;

                using (var cmd = new SqlCommand(fetchSql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return (0, "", "", false, false);

                        userId            = reader.GetInt32(0);
                        fullName          = reader.GetString(1);
                        email             = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        isDeveloper       = reader.GetBoolean(3);
                        isSuperAdmin      = reader.GetBoolean(4);
                        storedHash        = reader.IsDBNull(5) ? null : (byte[])reader[5];
                        storedSalt        = reader.IsDBNull(6) ? null : (byte[])reader[6];
                        hasLegacyPassword = reader.GetInt32(7) == 1;
                    }
                }

                bool passwordValid = false;

                if (storedHash != null && storedSalt != null)
                {
                    // Modern path: verify with hash/salt.
                    passwordValid = PasswordHelper.VerifyPassword(password, storedHash, storedSalt);
                    if (passwordValid)
                    {
                        // Scrub the legacy plaintext column if it still exists.
                        const string clearSql = @"
                            UPDATE dbo.[User] SET [Password] = NULL
                            WHERE UserId = @UserId AND [Password] IS NOT NULL";
                        using (var cmd = new SqlCommand(clearSql, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }
                else if (hasLegacyPassword)
                {
                    // Legacy path: verify via CONVERT(VARBINARY).
                    const string legacySql = @"
                        SELECT COUNT(1) FROM dbo.[User]
                        WHERE UserId = @UserId
                          AND [Password] = CONVERT(VARBINARY(MAX), @Password)";
                    using (var cmd = new SqlCommand(legacySql, con))
                    {
                        cmd.Parameters.AddWithValue("@UserId", userId);
                        cmd.Parameters.AddWithValue("@Password", password);
                        passwordValid = Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
                    }

                    if (passwordValid)
                    {
                        // Migrate: save hash/salt, clear legacy Password.
                        byte[] newSalt = PasswordHelper.GenerateSalt();
                        byte[] newHash = PasswordHelper.HashPassword(password, newSalt);
                        const string migrateSql = @"
                            UPDATE dbo.[User]
                            SET PasswordHash = @Hash, PasswordSalt = @Salt, [Password] = NULL
                            WHERE UserId = @UserId";
                        using (var cmd = new SqlCommand(migrateSql, con))
                        {
                            cmd.Parameters.AddWithValue("@UserId", userId);
                            cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, newHash.Length) { Value = newHash });
                            cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, newSalt.Length) { Value = newSalt });
                            await cmd.ExecuteNonQueryAsync();
                        }
                    }
                }

                return passwordValid
                    ? (userId, fullName, email, isDeveloper, isSuperAdmin)
                    : (0, "", "", false, false);
            }
        }

        /// <summary>
        /// AUTH OPTION B (recommended): store SHA-256 hash bytes in VARBINARY, compare hashed input.
        /// Example seed:
        /// INSERT INTO [User](Name, EmailAddress, [Password])
        /// VALUES (N'it_user', N'it@yakult.local', @HashBytes);
        /// (where @HashBytes is SHA-256 of '12345')
        /// </summary>
        public async Task<(int userId, string name, string email, bool isDeveloper)> AuthenticateByName_HashAsync(string name, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                byte[] pwdHash = HashPasswordSha256(password);

                const string sql = @"
                    SELECT UserId, Name, EmailAddress, ISNULL(IsDeveloper, 0) AS IsDeveloper
                    FROM [User]
                    WHERE Name = @Name
                      AND [Password] = @PwdHash;";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name);

                    var pHash = new SqlParameter("@PwdHash", SqlDbType.VarBinary, pwdHash.Length);
                    pHash.Value = pwdHash;
                    cmd.Parameters.Add(pHash);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            int userId = reader.GetInt32(0);
                            string fullName = reader.GetString(1);
                            string email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            bool isDeveloper = reader.GetBoolean(3);
                            return (userId, fullName, email, isDeveloper);
                        }
                    }
                }
            }

            return (0, "", "", false);
        }

        // Helper for seeding / switching to hashed passwords
        public static byte[] HashPasswordSha256(string password)
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            }
        }

        /// <summary>
        /// Optional: create a user (plain→varbinary style).
        /// For hashed storage, pass HashPasswordSha256(password) and use the hashed insert below.
        /// </summary>
        public async Task<int> CreateUser_PlainVarbinaryAsync(string name, string email, string plainPassword)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    INSERT INTO [User](Name, EmailAddress, [Password])
                    VALUES (@Name, @Email, CONVERT(VARBINARY(MAX), @Password));
                    SELECT SCOPE_IDENTITY();";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@Password", plainPassword);

                    object result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }

        /// <summary>
        /// Optional: create a user (hashed→varbinary style).
        /// </summary>
        public async Task<int> CreateUser_HashedAsync(string name, string email, string plainPassword)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                byte[] hash = HashPasswordSha256(plainPassword);

                const string sql = @"
                    INSERT INTO [User](Name, EmailAddress, [Password])
                    VALUES (@Name, @Email, @PwdHash);
                    SELECT SCOPE_IDENTITY();";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);

                    var pHash = new SqlParameter("@PwdHash", SqlDbType.VarBinary, hash.Length);
                    pHash.Value = hash;
                    cmd.Parameters.Add(pHash);

                    object result = await cmd.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }
        public async Task<bool> IsUserExistsAsync(string name, string email)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
            SELECT COUNT(*)
            FROM [User]
            WHERE Name = @Name
               OR (@Email IS NOT NULL AND EmailAddress = @Email)";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Name", name);
                    cmd.Parameters.AddWithValue("@Email", (object)email ?? DBNull.Value);

                    var result = await cmd.ExecuteScalarAsync();
                    int count = Convert.ToInt32(result);

                    return count > 0;
                }
            }
        }

        /// <summary>
        /// Resets a user's password (Admin function).
        /// Uses plain varbinary storage (matching current authentication method).
        /// </summary>
        public async Task<bool> ResetPasswordAsync(int userId, string newPassword)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                byte[] salt = PasswordHelper.GenerateSalt();
                byte[] hash = PasswordHelper.HashPassword(newPassword, salt);

                const string sql = @"
                    UPDATE dbo.[User]
                    SET PasswordHash = @Hash, PasswordSalt = @Salt, [Password] = NULL,
                        IsTemporaryPassword = 1, MustChangePassword = 1
                    WHERE UserId = @UserId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.Add(new SqlParameter("@Hash", SqlDbType.VarBinary, hash.Length) { Value = hash });
                    cmd.Parameters.Add(new SqlParameter("@Salt", SqlDbType.VarBinary, salt.Length) { Value = salt });

                    int rowsAffected = await cmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        /// <summary>
        /// Changes a user's password after verifying current password.
        /// Used by logged-in users to change their own password.
        /// </summary>
        public async Task<bool> ChangePasswordAsync(int userId, string currentPassword, string newPassword)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                byte[] storedHash = null;
                byte[] storedSalt = null;

                const string loadSql = @"
                    SELECT PasswordHash, PasswordSalt
                    FROM dbo.[User]
                    WHERE UserId = @UserId";

                using (var cmd = new SqlCommand(loadSql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return false;

                        storedHash = reader.IsDBNull(0) ? null : (byte[])reader[0];
                        storedSalt = reader.IsDBNull(1) ? null : (byte[])reader[1];
                    }
                }

                bool passwordValid;
                if (storedHash != null && storedSalt != null)
                {
                    passwordValid = PasswordHelper.VerifyPassword(currentPassword, storedHash, storedSalt);
                }
                else
                {
                    const string verifyLegacySql = @"
                        SELECT COUNT(*)
                        FROM dbo.[User]
                        WHERE UserId = @UserId
                          AND [Password] = CONVERT(VARBINARY(MAX), @CurrentPassword)";

                    using (var verifyCmd = new SqlCommand(verifyLegacySql, con))
                    {
                        verifyCmd.Parameters.AddWithValue("@UserId", userId);
                        verifyCmd.Parameters.AddWithValue("@CurrentPassword", currentPassword);
                        int count = Convert.ToInt32(await verifyCmd.ExecuteScalarAsync());
                        passwordValid = count > 0;
                    }
                }

                if (!passwordValid)
                    return false;

                byte[] newSalt = PasswordHelper.GenerateSalt();
                byte[] newHash = PasswordHelper.HashPassword(newPassword, newSalt);

                bool hasPasswordChangedAt = await UserColumnExistsAsync(con, "PasswordChangedAt");
                bool hasPasswordChangedBy = await UserColumnExistsAsync(con, "PasswordChangedBy");

                var setParts = new List<string>
                {
                    "PasswordHash = @PasswordHash",
                    "PasswordSalt = @PasswordSalt",
                    "[Password] = CONVERT(VARBINARY(MAX), @NewPassword)",
                    "MustChangePassword = 0",
                    "IsTemporaryPassword = 0"
                };

                if (hasPasswordChangedAt)
                    setParts.Add("PasswordChangedAt = SYSDATETIME()");
                if (hasPasswordChangedBy)
                    setParts.Add("PasswordChangedBy = @PasswordChangedBy");

                string updateSql = $@"
                    UPDATE dbo.[User]
                    SET {string.Join(",\n                        ", setParts)}
                    WHERE UserId = @UserId";

                using (var updateCmd = new SqlCommand(updateSql, con))
                {
                    updateCmd.Parameters.AddWithValue("@UserId", userId);
                    updateCmd.Parameters.AddWithValue("@NewPassword", newPassword);
                    updateCmd.Parameters.AddWithValue("@PasswordHash", newHash);
                    updateCmd.Parameters.AddWithValue("@PasswordSalt", newSalt);

                    if (hasPasswordChangedBy)
                        updateCmd.Parameters.AddWithValue("@PasswordChangedBy", AppSession.CurrentUserId > 0 ? (object)AppSession.CurrentUserId : DBNull.Value);

                    int rowsAffected = await updateCmd.ExecuteNonQueryAsync();
                    return rowsAffected > 0;
                }
            }
        }

        public async Task<UserAccountDto> GetUserAccountByUserIdAsync(int userId)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT
                        u.UserId,
                        u.Name,
                        u.EmailAddress,
                        u.IsActive,
                        ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                        u.EmpId,
                        e.Name AS EmployeeName,
                        u.IsTemporaryPassword,
                        u.MustChangePassword,
                        u.LastLoginDate,
                        u.DateCreated
                    FROM dbo.[User] u
                    LEFT JOIN dbo.Employee e ON u.EmpId = e.EmpId
                    WHERE u.UserId = @UserId";

                UserAccountDto account = null;

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return null;

                        account = new UserAccountDto
                        {
                            UserId = reader.GetInt32(0),
                            Name = reader.GetString(1),
                            EmailAddress = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            IsActive = reader.GetBoolean(3),
                            IsDeveloper = reader.GetBoolean(4),
                            EmpId = reader.IsDBNull(5) ? (int?)null : reader.GetInt32(5),
                            EmployeeName = reader.IsDBNull(6) ? null : reader.GetString(6),
                            IsTemporaryPassword = !reader.IsDBNull(7) && reader.GetBoolean(7),
                            MustChangePassword = !reader.IsDBNull(8) && reader.GetBoolean(8),
                            LastLoginDate = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9),
                            DateCreated = reader.GetDateTime(10)
                        };
                    }
                }

                var roles = new List<string>();
                const string rolesSql = @"
                    SELECT r.RoleName
                    FROM dbo.UserRole ur
                    INNER JOIN dbo.Role r ON ur.RoleId = r.RoleId
                    WHERE ur.UserId = @UserId AND r.IsActive = 1
                    ORDER BY r.RoleName";

                using (var rolesCmd = new SqlCommand(rolesSql, con))
                {
                    rolesCmd.Parameters.AddWithValue("@UserId", userId);
                    using (var rolesReader = await rolesCmd.ExecuteReaderAsync())
                    {
                        while (await rolesReader.ReadAsync())
                        {
                            roles.Add(rolesReader.GetString(0));
                        }
                    }
                }

                account.Roles = roles;
                return account;
            }
        }

        public async Task<bool> UpdateUserEmailAsync(int userId, string emailAddress)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    UPDATE dbo.[User]
                    SET EmailAddress = @EmailAddress
                    WHERE UserId = @UserId";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@UserId", userId);
                    cmd.Parameters.AddWithValue("@EmailAddress", (object)emailAddress ?? DBNull.Value);
                    int rows = await cmd.ExecuteNonQueryAsync();
                    return rows > 0;
                }
            }
        }

        private static async Task<bool> UserColumnExistsAsync(SqlConnection con, string columnName)
        {
            const string sql = "SELECT COL_LENGTH('dbo.[User]', @ColumnName)";
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@ColumnName", columnName);
                object result = await cmd.ExecuteScalarAsync();
                return result != null && result != DBNull.Value;
            }
        }

        /// <summary>
        /// Gets all users for admin management.
        /// </summary>
        public async Task<List<UserDto>> GetAllUsersAsync()
        {
            var users = new List<UserDto>();

            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                const string sql = @"
                    SELECT UserId, Name, EmailAddress, DateCreated, ISNULL(IsDeveloper, 0) AS IsDeveloper
                    FROM [User]
                    ORDER BY Name";

                using (var cmd = new SqlCommand(sql, con))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            users.Add(new UserDto
                            {
                                UserId = reader.GetInt32(0),
                                Name = reader.GetString(1),
                                EmailAddress = reader.IsDBNull(2) ? "" : reader.GetString(2),
                                DateCreated = reader.GetDateTime(3),
                                IsDeveloper = reader.GetBoolean(4)
                            });
                        }
                    }
                }
            }

            return users;
        }

        /// <summary>
        /// NEW: Authenticate using PasswordHash and PasswordSalt (NEW SYSTEM)
        /// Also loads employee data and roles
        /// </summary>
        public async Task<AuthResult> AuthenticateWithHashAndSalt(string username, string password)
        {
            using (var con = new SqlConnection(_connectionString))
            {
                await con.OpenAsync();

                // Load user data with employee info and active status
                const string sql = @"
                    SELECT
                        u.UserId,
                        u.Name,
                        u.EmailAddress,
                        ISNULL(u.IsDeveloper, 0) AS IsDeveloper,
                        u.PasswordHash,
                        u.PasswordSalt,
                        u.IsActive,
                        u.MustChangePassword,
                        u.EmpId,
                        e.Name AS EmployeeName,
                        e.Position AS EmployeePosition
                    FROM [User] u
                    LEFT JOIN Employee e ON u.EmpId = e.EmpId AND e.Active = 1
                    WHERE u.Name = @Username
                      AND u.IsActive = 1;";

                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@Username", username);

                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return new AuthResult { Success = false, ErrorMessage = "Invalid username or password" };
                        }

                        int userId = reader.GetInt32(0);
                        string name = reader.GetString(1);
                        string email = reader.IsDBNull(2) ? "" : reader.GetString(2);
                        bool isDeveloper = reader.GetBoolean(3);
                        byte[] storedHash = reader.IsDBNull(4) ? null : (byte[])reader[4];
                        byte[] storedSalt = reader.IsDBNull(5) ? null : (byte[])reader[5];
                        bool isActive = reader.GetBoolean(6);
                        bool mustChangePassword = reader.GetBoolean(7);
                        int? empId = reader.IsDBNull(8) ? (int?)null : reader.GetInt32(8);
                        string empName = reader.IsDBNull(9) ? null : reader.GetString(9);
                        string empPosition = reader.IsDBNull(10) ? null : reader.GetString(10);

                        // Verify password
                        if (storedHash == null || storedSalt == null)
                        {
                            return new AuthResult { Success = false, ErrorMessage = "Account not properly configured. Please contact administrator." };
                        }

                        bool passwordValid = Helpers.PasswordHelper.VerifyPassword(password, storedHash, storedSalt);
                        if (!passwordValid)
                        {
                            return new AuthResult { Success = false, ErrorMessage = "Invalid username or password" };
                        }

                        // Load user roles
                        reader.Close();
                        var roles = new List<string>();
                        const string rolesSql = @"
                            SELECT r.RoleName
                            FROM UserRole ur
                            INNER JOIN Role r ON ur.RoleId = r.RoleId
                            WHERE ur.UserId = @UserId AND r.IsActive = 1;";

                        using (var rolesCmd = new SqlCommand(rolesSql, con))
                        {
                            rolesCmd.Parameters.AddWithValue("@UserId", userId);
                            using (var rolesReader = await rolesCmd.ExecuteReaderAsync())
                            {
                                while (await rolesReader.ReadAsync())
                                {
                                    roles.Add(rolesReader.GetString(0));
                                }
                            }
                        }

                        // Update last login date
                        const string updateLoginSql = "UPDATE [User] SET LastLoginDate = GETDATE() WHERE UserId = @UserId";
                        using (var updateCmd = new SqlCommand(updateLoginSql, con))
                        {
                            updateCmd.Parameters.AddWithValue("@UserId", userId);
                            await updateCmd.ExecuteNonQueryAsync();
                        }

                        return new AuthResult
                        {
                            Success = true,
                            UserId = userId,
                            Name = name,
                            Email = email,
                            IsDeveloper = isDeveloper,
                            MustChangePassword = mustChangePassword,
                            EmployeeId = empId,
                            EmployeeName = empName,
                            EmployeePosition = empPosition,
                            Roles = roles
                        };
                    }
                }
            }
        }
    }

    /// <summary>
    /// DTO for User data
    /// </summary>
    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; }
        public string EmailAddress { get; set; }
        public DateTime DateCreated { get; set; }
        public bool IsDeveloper { get; set; }
    }

    /// <summary>
    /// Result from authentication attempt
    /// </summary>
    public class AuthResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public int UserId { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public bool IsDeveloper { get; set; }
        public bool MustChangePassword { get; set; }
        public int? EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeePosition { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}
