using System;
using System.Data.SqlClient;
using System.Diagnostics;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Reads and writes dbo.UserNotificationSetting — per-user notification preferences.
    /// Mirrors the pattern used by NotificationRepository (synchronous, raw ADO.NET).
    /// </summary>
    public class UserNotificationSettingRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        /// <summary>
        /// Returns whether notifications are enabled for the given user.
        /// Defaults to true if no row exists yet.
        /// </summary>
        public bool GetNotificationsEnabled(int userId)
        {
            const string sql = @"
                SELECT NotificationsEnabled
                FROM dbo.[UserNotificationSetting]
                WHERE UserId = @UserId";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                con.Open();
                var result = cmd.ExecuteScalar();
                return result == null || result == DBNull.Value || (bool)result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserNotificationSettingRepository] GetNotificationsEnabled failed: {ex.Message}");
                return true; // safe default
            }
        }

        /// <summary>
        /// Creates or updates the notification preference for the given user.
        /// </summary>
        public void Upsert(int userId, bool notificationsEnabled)
        {
            const string sql = @"
                MERGE dbo.[UserNotificationSetting] AS target
                USING (SELECT @UserId AS UserId) AS source ON target.UserId = source.UserId
                WHEN MATCHED THEN
                    UPDATE SET NotificationsEnabled = @Enabled,
                               UpdatedAt            = SYSUTCDATETIME(),
                               UpdatedByUserId      = @UserId
                WHEN NOT MATCHED THEN
                    INSERT (UserId, NotificationsEnabled, UpdatedAt, UpdatedByUserId)
                    VALUES (@UserId, @Enabled, SYSUTCDATETIME(), @UserId);";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId",  userId);
                cmd.Parameters.AddWithValue("@Enabled", notificationsEnabled);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UserNotificationSettingRepository] Upsert failed: {ex.Message}");
            }
        }
    }
}
