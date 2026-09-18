using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    public interface IUserNotificationSettingRepository
    {
        Task<bool> GetNotificationsEnabledAsync(int userId);
        Task UpsertAsync(int userId, bool notificationsEnabled);
    }

    public class UserNotificationSettingRepository : IUserNotificationSettingRepository
    {
        private readonly IConnectionStringProvider _conn;
        private readonly ILogger<UserNotificationSettingRepository> _logger;

        public UserNotificationSettingRepository(
            IConnectionStringProvider conn,
            ILogger<UserNotificationSettingRepository> logger)
        {
            _conn   = conn;
            _logger = logger;
        }

        public async Task<bool> GetNotificationsEnabledAsync(int userId)
        {
            const string sql = @"
                SELECT NotificationsEnabled
                FROM dbo.[UserNotificationSetting]
                WHERE UserId = @UserId";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                // If no row exists yet, default to enabled.
                return result == null || result == DBNull.Value || (bool)result;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[UserNotificationSettingRepository] GetNotificationsEnabledAsync failed: {Message}", ex.Message);
                return true; // safe default
            }
        }

        public async Task UpsertAsync(int userId, bool notificationsEnabled)
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
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId",  userId);
                cmd.Parameters.AddWithValue("@Enabled", notificationsEnabled);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[UserNotificationSettingRepository] UpsertAsync failed: {Message}", ex.Message);
            }
        }
    }
}
