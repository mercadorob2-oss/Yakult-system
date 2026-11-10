using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Security
{
    public interface IDeveloperConnectionAuthorizer
    {
        Task<bool> VerifyAsync(string username, string password);
    }

    public sealed class DeveloperConnectionAuthorizer : IDeveloperConnectionAuthorizer
    {
        private readonly string _connectionString;
        public DeveloperConnectionAuthorizer(string connectionString) { _connectionString = connectionString; }

        public async Task<bool> VerifyAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) || username.Length > 256 || password.Length > 4000) return false;
            var builder = new SqlConnectionStringBuilder(_connectionString) { ConnectTimeout = 8 };
            using (var connection = new SqlConnection(builder.ConnectionString))
            {
                await connection.OpenAsync();
                const string sql = @"
                    SELECT TOP (2) ISNULL(IsActive, 1), ISNULL(IsDeveloper, 0), PasswordHash, PasswordSalt,
                           CASE WHEN [Password] = CONVERT(VARBINARY(MAX), @Password) THEN 1 ELSE 0 END
                    FROM dbo.[User]
                    WHERE Name COLLATE Latin1_General_CS_AS = @Name COLLATE Latin1_General_CS_AS
                      AND ISNULL(IsActive, 1) = 1 AND ISNULL(IsDeveloper, 0) = 1;";
                using (var command = new SqlCommand(sql, connection) { CommandTimeout = 8 })
                {
                    command.Parameters.Add("@Name", SqlDbType.NVarChar, 256).Value = username.Trim();
                    command.Parameters.Add("@Password", SqlDbType.NVarChar, 4000).Value = password;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync()) return false;
                        var valid = VerifyStoredDeveloper(reader.GetBoolean(0), reader.GetBoolean(1),
                            reader.IsDBNull(2) ? null : (byte[])reader[2], reader.IsDBNull(3) ? null : (byte[])reader[3], reader.GetInt32(4) == 1, password);
                        return valid && !await reader.ReadAsync();
                    }
                }
            }
        }

        public static bool VerifyStoredDeveloper(bool active, bool developer, byte[] hash, byte[] salt, bool legacyPasswordMatches, string password)
        {
            if (!active || !developer || string.IsNullOrWhiteSpace(password)) return false;
            if (hash != null || salt != null) return PasswordHelper.VerifyPassword(password, hash, salt);
            return legacyPasswordMatches;
        }
    }
}
