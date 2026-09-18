using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Repository for reading and writing global system settings stored in dbo.SystemSetting.
    /// Falls back gracefully when the table doesn't exist yet (pre-migration).
    /// </summary>
    public class SystemSettingRepository
    {
        private const string SmtpEnabledKey = "SmtpEnabled";

        /// <summary>
        /// Returns true if SMTP sending is globally enabled, false if it is suppressed.
        /// Defaults to true (enabled) if the table or row does not yet exist.
        /// </summary>
        public async Task<bool> GetSmtpEnabledAsync()
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    if (!await TableExistsAsync(con, "dbo.SystemSetting"))
                        return true;

                    using (var cmd = new SqlCommand(
                        "SELECT TOP 1 SettingValue FROM dbo.SystemSetting WHERE SettingKey = @Key", con))
                    {
                        cmd.Parameters.AddWithValue("@Key", SmtpEnabledKey);
                        var result = await cmd.ExecuteScalarAsync();

                        if (result == null || result == DBNull.Value)
                            return true; // Row missing → default enabled

                        return result.ToString() == "1";
                    }
                }
            }
            catch
            {
                // Never block email dispatch due to a settings read failure.
                return true;
            }
        }

        /// <summary>
        /// Persists the global SMTP enabled/disabled state.
        /// Uses UPSERT so the row is created automatically if it was never seeded.
        /// </summary>
        public async Task SetSmtpEnabledAsync(bool enabled, int modifiedByUserId)
        {
            using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
            {
                await con.OpenAsync();

                if (!await TableExistsAsync(con, "dbo.SystemSetting"))
                    throw new InvalidOperationException(
                        "Table dbo.SystemSetting not found. Please run Migration_SystemSetting_CreateAndSeedSmtpToggle.sql first.");

                using (var cmd = new SqlCommand(@"
                    IF EXISTS (SELECT 1 FROM dbo.SystemSetting WHERE SettingKey = @Key)
                        UPDATE dbo.SystemSetting
                        SET    SettingValue      = @Value,
                               DateModified      = @DateModified,
                               ModifiedByUserId  = @ModifiedByUserId
                        WHERE  SettingKey = @Key
                    ELSE
                        INSERT INTO dbo.SystemSetting
                               (SettingKey, SettingValue, Description, ModifiedByUserId)
                        VALUES (@Key, @Value,
                                N'Global SMTP send toggle. ''1'' = outgoing email enabled; ''0'' = all SMTP dispatch suppressed system-wide.',
                                @ModifiedByUserId)", con))
                {
                    cmd.Parameters.AddWithValue("@Key", SmtpEnabledKey);
                    cmd.Parameters.AddWithValue("@Value", enabled ? "1" : "0");
                    cmd.Parameters.AddWithValue("@DateModified", DateTime.UtcNow);
                    cmd.Parameters.AddWithValue("@ModifiedByUserId", modifiedByUserId);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static async Task<bool> TableExistsAsync(SqlConnection con, string fullTableName)
        {
            string schemaName = "dbo";
            string tableName = fullTableName;

            if (!string.IsNullOrWhiteSpace(fullTableName) && fullTableName.Contains("."))
            {
                var parts = fullTableName.Split(new[] { '.' }, 2);
                if (parts.Length == 2) { schemaName = parts[0]; tableName = parts[1]; }
            }

            using (var cmd = new SqlCommand(@"
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM   sys.objects  o
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    WHERE  s.name = @SchemaName
                      AND  o.name = @TableName
                      AND  o.type = 'U'
                ) THEN 1 ELSE 0 END", con))
            {
                cmd.Parameters.AddWithValue("@SchemaName", schemaName);
                cmd.Parameters.AddWithValue("@TableName", tableName);
                var result = await cmd.ExecuteScalarAsync();
                return result != null && Convert.ToInt32(result) == 1;
            }
        }
    }
}
