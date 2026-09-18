using Microsoft.Data.SqlClient;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Services;

namespace Inventory.RequestPortal.Repositories
{
    public interface INotificationRepository
    {
        Task CreateAsync(NotificationCreateDto dto);
        Task<List<NotificationDto>> GetByUserAsync(int userId, int limit = 50);
        Task<int> GetUnreadCountAsync(int userId);
        Task MarkAsReadAsync(int notificationId, int userId);
        Task MarkAllAsReadAsync(int userId);
        Task ClearAllAsync(int userId);
        Task<int?> GetUserIdByAuthorizationIdAsync(int authorizationId);
        Task<int?> GetUserIdByApprovalIdAsync(int approvalId);
        Task<int?> GetRecentPortalReqIdByApprovalIdAsync(int approvalId);
        Task<string?> GetSetCodeByReqIdAsync(int reqId);
        Task<string?> GetRecentPortalSetCodeByApprovalIdAsync(int approvalId);
        Task<string?> GetSetCodeByAuthorizationIdAsync(int authorizationId);
        Task<(int? UserId, int? AuthorizationId)> GetUserAndAuthIdForFulfillmentAsync(int empId, Guid? sessionId);
    }

    public class NotificationRepository : INotificationRepository
    {
        private readonly IConnectionStringProvider _conn;
        private readonly ILogger<NotificationRepository> _logger;

        public NotificationRepository(
            IConnectionStringProvider conn,
            ILogger<NotificationRepository> logger)
        {
            _conn   = conn;
            _logger = logger;
        }

        public async Task CreateAsync(NotificationCreateDto dto)
        {
            // PortalId is resolved from PortalKey in-statement — no extra round trip.
            // Left NULL if the portal row is somehow missing (FK allows NULL); the
            // read-side ownership filter still surfaces it via the type fallback.
            const string sql = @"
                INSERT INTO dbo.[Notification]
                    (UserId, Title, Message, NotificationType, ReferenceId, PortalId)
                VALUES
                    (@UserId, @Title, @Message, @NotificationType, @ReferenceId,
                     (SELECT PortalId FROM dbo.Portal WHERE PortalKey = @PortalKey))";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId",           dto.UserId);
                cmd.Parameters.AddWithValue("@Title",            Truncate(dto.Title,   200));
                cmd.Parameters.AddWithValue("@Message",          Truncate(dto.Message, 1000));
                cmd.Parameters.AddWithValue("@NotificationType", Truncate(dto.NotificationType, 100));
                cmd.Parameters.AddWithValue("@ReferenceId",      (object?)dto.ReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PortalKey",        string.IsNullOrWhiteSpace(dto.PortalKey) ? RequesterPortalKey : dto.PortalKey);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] CreateAsync failed: {Message}", ex.Message);
            }
        }

        public async Task<List<NotificationDto>> GetByUserAsync(int userId, int limit = 50)
        {
            var list = new List<NotificationDto>();
            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = @"
                    SELECT TOP (@Limit)
                        NotificationId, UserId, Title, Message,
                        NotificationType, ReferenceId, IsRead, CreatedDate
                    FROM dbo.[Notification]
                    WHERE UserId = @UserId" + RequestPortalOwnershipFilter(cmd) + @"
                    ORDER BY CreatedDate DESC";
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Limit",  limit);
                await con.OpenAsync();
                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    list.Add(MapRow(r));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetByUserAsync failed: {Message}", ex.Message);
            }
            return list;
        }

        public async Task<int> GetUnreadCountAsync(int userId)
        {
            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = @"
                    SELECT COUNT(*)
                    FROM dbo.[Notification]
                    WHERE UserId = @UserId AND IsRead = 0" + RequestPortalOwnershipFilter(cmd);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result is int count ? count : 0;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetUnreadCountAsync failed: {Message}", ex.Message);
                return 0;
            }
        }

        public async Task MarkAsReadAsync(int notificationId, int userId)
        {
            const string sql = @"
                UPDATE dbo.[Notification]
                SET IsRead = 1
                WHERE NotificationId = @NotificationId AND UserId = @UserId";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@NotificationId", notificationId);
                cmd.Parameters.AddWithValue("@UserId",         userId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] MarkAsReadAsync failed: {Message}", ex.Message);
            }
        }

        public async Task MarkAllAsReadAsync(int userId)
        {
            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = @"
                    UPDATE dbo.[Notification]
                    SET IsRead = 1
                    WHERE UserId = @UserId AND IsRead = 0" + RequestPortalOwnershipFilter(cmd);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] MarkAllAsReadAsync failed: {Message}", ex.Message);
            }
        }

        public async Task ClearAllAsync(int userId)
        {
            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = "DELETE FROM dbo.[Notification] WHERE UserId = @UserId"
                    + RequestPortalOwnershipFilter(cmd);
                cmd.Parameters.AddWithValue("@UserId", userId);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] ClearAllAsync failed: {Message}", ex.Message);
            }
        }

        public async Task<int?> GetUserIdByAuthorizationIdAsync(int authorizationId)
        {
            // Primary: CartridgeAuthorization.EmployeeId → User.EmpId
            const string sqlPrimary = @"
                SELECT u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.AuthorizationId = @AuthorizationId";

            // Fallback: CartridgeAuthorization.EmployeeId → EmployeeEmail → EmailAddress → User.EmailAddress
            // Handles the case where User.EmpId is not set but the user's login email matches an employee email.
            const string sqlFallback = @"
                SELECT TOP 1 u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.EmployeeEmail ee ON ee.EmpId = ca.EmployeeId AND ee.IsActive = 1
                INNER JOIN dbo.EmailAddress   ea ON ea.EmailId = ee.EmailId AND ea.IsActive = 1
                INNER JOIN dbo.[User]         u  ON u.EmailAddress = ea.EmailAddress
                WHERE ca.AuthorizationId = @AuthorizationId
                ORDER BY ee.IsPrimary DESC, u.UserId ASC";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                await con.OpenAsync();

                using (var cmd = new SqlCommand(sqlPrimary, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result is int id)
                    {
                        _logger.LogDebug("[NotificationRepository] GetUserIdByAuthorizationIdAsync: AuthId={AuthId} → UserId={UserId} (primary path)", authorizationId, id);
                        return id;
                    }
                }

                _logger.LogWarning("[NotificationRepository] GetUserIdByAuthorizationIdAsync: primary path (User.EmpId join) returned no result for AuthId={AuthId} — trying email fallback", authorizationId);

                using (var cmd = new SqlCommand(sqlFallback, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result is int id)
                    {
                        _logger.LogInformation("[NotificationRepository] GetUserIdByAuthorizationIdAsync: AuthId={AuthId} → UserId={UserId} (email fallback path)", authorizationId, id);
                        return id;
                    }
                }

                _logger.LogWarning("[NotificationRepository] GetUserIdByAuthorizationIdAsync: no User found via primary or email fallback for AuthId={AuthId} — Authorization Status notification will not be delivered", authorizationId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetUserIdByAuthorizationIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<int?> GetUserIdByApprovalIdAsync(int approvalId)
        {
            // Primary: CartridgeApproval.EmpId → User.EmpId (direct link, works when employee has a linked User account)
            const string sqlPrimary = @"
                SELECT u.UserId
                FROM dbo.CartridgeApproval ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmpId
                WHERE ca.ApprovalId = @ApprovalId";

            // Fallback: CartridgeApproval.EmpId → EmployeeEmail → EmailAddress → User.EmailAddress
            // Handles the case where User.EmpId has not been set but the user's login email matches an employee email.
            const string sqlFallback = @"
                SELECT TOP 1 u.UserId
                FROM dbo.CartridgeApproval ca
                INNER JOIN dbo.EmployeeEmail ee ON ee.EmpId = ca.EmpId AND ee.IsActive = 1
                INNER JOIN dbo.EmailAddress   ea ON ea.EmailId = ee.EmailId AND ea.IsActive = 1
                INNER JOIN dbo.[User]         u  ON u.EmailAddress = ea.EmailAddress
                WHERE ca.ApprovalId = @ApprovalId
                ORDER BY ee.IsPrimary DESC, u.UserId ASC";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                await con.OpenAsync();

                using (var cmd = new SqlCommand(sqlPrimary, con))
                {
                    cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result is int id)
                    {
                        _logger.LogDebug("[NotificationRepository] GetUserIdByApprovalIdAsync: ApprovalId={ApprovalId} → UserId={UserId} (primary path)", approvalId, id);
                        return id;
                    }
                }

                _logger.LogWarning("[NotificationRepository] GetUserIdByApprovalIdAsync: primary path (User.EmpId join) returned no result for ApprovalId={ApprovalId} — trying email fallback", approvalId);

                using (var cmd = new SqlCommand(sqlFallback, con))
                {
                    cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                    var result = await cmd.ExecuteScalarAsync();
                    if (result is int id)
                    {
                        _logger.LogInformation("[NotificationRepository] GetUserIdByApprovalIdAsync: ApprovalId={ApprovalId} → UserId={UserId} (email fallback path)", approvalId, id);
                        return id;
                    }
                }

                _logger.LogWarning("[NotificationRepository] GetUserIdByApprovalIdAsync: no User found via primary or email fallback for ApprovalId={ApprovalId} — Request Status notification will not be delivered", approvalId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetUserIdByApprovalIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<int?> GetRecentPortalReqIdByApprovalIdAsync(int approvalId)
        {
            const string sql = @"
                SELECT TOP 1 r.ReqId
                FROM dbo.CartridgeApproval ca
                INNER JOIN dbo.Request r ON r.EmpId = ca.EmpId
                WHERE ca.ApprovalId = @ApprovalId
                  AND r.Description LIKE '[[]PORTAL%'
                ORDER BY r.DateCreated DESC";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result is int id ? id : null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetRecentPortalReqIdByApprovalIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<string?> GetSetCodeByReqIdAsync(int reqId)
        {
            // Returns the SetCode for a given ReqId (used for REQUEST_FULFILLED-type notifications).
            const string sql = @"
                SELECT TOP 1 s.SetCode
                FROM dbo.Request r
                LEFT JOIN dbo.[Set] s ON s.SetId = r.SetId
                WHERE r.ReqId = @ReqId
                  AND s.SetCode IS NOT NULL";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@ReqId", reqId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result as string;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetSetCodeByReqIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<string?> GetRecentPortalSetCodeByApprovalIdAsync(int approvalId)
        {
            // Resolves an ApprovalId to the SetCode of the employee's most recent portal request.
            // CartridgeApproval shares EmpId with dbo.Request; the newest portal request is the one
            // that triggered the approval flow.
            const string sql = @"
                SELECT TOP 1 s.SetCode
                FROM dbo.CartridgeApproval ca
                INNER JOIN dbo.Request r  ON r.EmpId  = ca.EmpId
                INNER JOIN dbo.[Set]    s ON s.SetId  = r.SetId
                WHERE ca.ApprovalId = @ApprovalId
                  AND r.Description LIKE '[[]PORTAL%'
                  AND s.SetCode IS NOT NULL
                ORDER BY r.DateCreated DESC";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result as string;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetRecentPortalSetCodeByApprovalIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        public async Task<string?> GetSetCodeByAuthorizationIdAsync(int authorizationId)
        {
            // Resolves a CartridgeAuthorization.AuthorizationId (stored by the desktop app on
            // fulfillment notifications) to the SetCode of the matching portal request.
            // Join through SubmissionSessionId so each authorization maps to its exact session,
            // not just the most-recent request for that employee.
            const string sql = @"
                SELECT TOP 1 s.SetCode
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Request r ON r.SubmissionSessionId = ca.SubmissionSessionId
                INNER JOIN dbo.[Set]   s ON s.SetId = r.SetId
                WHERE ca.AuthorizationId = @AuthorizationId
                  AND s.SetCode IS NOT NULL";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                await con.OpenAsync();
                var result = await cmd.ExecuteScalarAsync();
                return result as string;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetSetCodeByAuthorizationIdAsync failed: {Message}", ex.Message);
                return null;
            }
        }

        /// <summary>
        /// PORTED FROM: Yakult.Inventory.App/Repositories/NotificationRepository.cs
        /// Resolves the portal UserId + CartridgeAuthorization AuthorizationId needed to notify a
        /// requester after cartridge exchange fulfillment (the fulfillment commit only has
        /// EmpId/SubmissionSessionId in hand, not the portal UserId).
        /// </summary>
        public async Task<(int? UserId, int? AuthorizationId)> GetUserAndAuthIdForFulfillmentAsync(int empId, Guid? sessionId)
        {
            const string sqlSession = @"
                SELECT TOP 1 ca.AuthorizationId, u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.SubmissionSessionId = @SessionId
                  AND ca.Status = 'Approved'";

            const string sqlEmpId = @"
                SELECT TOP 1 ca.AuthorizationId, u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.EmployeeId = @EmpId
                  AND ca.Status = 'Approved'
                ORDER BY ca.CreatedDate DESC";

            try
            {
                using var con = new SqlConnection(_conn.GetConnectionString());
                await con.OpenAsync();

                if (sessionId.HasValue)
                {
                    using var cmd = new SqlCommand(sqlSession, con);
                    cmd.Parameters.AddWithValue("@SessionId", sessionId.Value);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                        return (r.GetInt32(1), r.GetInt32(0));
                }

                using (var cmd = new SqlCommand(sqlEmpId, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    using var r = await cmd.ExecuteReaderAsync();
                    if (await r.ReadAsync())
                        return (r.GetInt32(1), r.GetInt32(0));
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[NotificationRepository] GetUserAndAuthIdForFulfillmentAsync failed: {Message}", ex.Message);
            }

            return (null, null);
        }

        private static NotificationDto MapRow(SqlDataReader r) => new()
        {
            NotificationId   = r.GetInt32(r.GetOrdinal("NotificationId")),
            UserId           = r.GetInt32(r.GetOrdinal("UserId")),
            Title            = r.GetString(r.GetOrdinal("Title")),
            Message          = r.GetString(r.GetOrdinal("Message")),
            NotificationType = r.GetString(r.GetOrdinal("NotificationType")),
            ReferenceId      = r.IsDBNull(r.GetOrdinal("ReferenceId")) ? null : r.GetInt32(r.GetOrdinal("ReferenceId")),
            IsRead           = r.GetBoolean(r.GetOrdinal("IsRead")),
            CreatedDate      = r.GetDateTime(r.GetOrdinal("CreatedDate")),
        };

        private static string Truncate(string value, int max)
            => value.Length <= max ? value : value[..max];

        // dbo.Portal.PortalKey for the web Request Portal — the portal every row this
        // repository writes belongs to, and the one its bell reads.
        private const string RequesterPortalKey = "RequesterPortal";

        /// <summary>
        /// Confines a bell read / bulk-op to the notifications this portal owns, keyed on the
        /// <c>dbo.Notification.PortalId</c> FK (Migration_Notification_AddPortalId.sql /
        /// _Finalize.sql). Ownership is the column, not the type string, so a NEW notification
        /// type needs no change here. Appends " AND PortalId = (...)" and binds @PortalKey.
        /// </summary>
        private static string RequestPortalOwnershipFilter(SqlCommand cmd)
        {
            cmd.Parameters.AddWithValue("@PortalKey", RequesterPortalKey);
            return " AND PortalId = (SELECT PortalId FROM dbo.Portal WHERE PortalKey = @PortalKey)";
        }
    }
}
