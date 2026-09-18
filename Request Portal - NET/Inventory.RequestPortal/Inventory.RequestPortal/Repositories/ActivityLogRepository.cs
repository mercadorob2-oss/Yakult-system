using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    /// <summary>
    /// Writes rows to dbo.UserActivityLog from the Request Portal.
    /// Mirrors the fire-and-forget behaviour of ActivityLogger in the WinForms app:
    /// exceptions are caught and written to the ASP.NET Core logger only —
    /// a failed log entry must never crash a user-facing request.
    /// </summary>
    public interface IActivityLogRepository
    {
        void LogActivity(int userId, string actionType, string entityType,
                         int? entityId, string description);
    }

    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly IConnectionStringProvider _connectionStringProvider;
        private readonly ILogger<ActivityLogRepository> _logger;

        public ActivityLogRepository(
            IConnectionStringProvider connectionStringProvider,
            ILogger<ActivityLogRepository> logger)
        {
            _connectionStringProvider = connectionStringProvider;
            _logger = logger;
        }

        public void LogActivity(int userId, string actionType, string entityType,
                                int? entityId, string description)
        {
            if (userId <= 0)
                return;

            try
            {
                const string sql = @"
                    INSERT INTO dbo.UserActivityLog
                        (UserId, ActionType, EntityType, EntityId, Description)
                    VALUES
                        (@UserId, @ActionType, @EntityType, @EntityId, @Description)";

                using var connection = new SqlConnection(_connectionStringProvider.GetConnectionString());
                using var cmd = new SqlCommand(sql, connection);
                cmd.Parameters.AddWithValue("@UserId",     userId);
                cmd.Parameters.AddWithValue("@ActionType", Truncate(actionType, 50));
                cmd.Parameters.AddWithValue("@EntityType", Truncate(entityType, 50));
                cmd.Parameters.AddWithValue("@EntityId",   (object?)entityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description",(object?)Truncate(description, 500) ?? DBNull.Value);
                connection.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                // Logging must never crash the portal.
                _logger.LogWarning("[ActivityLogRepository] Failed to write activity log: {Message}", ex.Message);
            }
        }

        private static string? Truncate(string? value, int maxLength)
            => value is null ? null
             : value.Length <= maxLength ? value
             : value[..maxLength];
    }
}
