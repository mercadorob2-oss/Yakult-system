using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Diagnostics;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Reads and writes dbo.Notification — the same table used by the Request Portal web app.
    /// Both applications share a single notification record per user; this class provides the
    /// WinForms-side contracts so the desktop app can consume and display those notifications.
    ///
    /// The UI layer is not implemented here. This repository exposes the data so that
    /// a future Windows Toast Notification integration only needs to call these methods
    /// without touching the schema or business logic.
    /// </summary>
    public class NotificationRepository
    {
        private string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        public void Create(NotificationCreateDto dto)
        {
            // PortalId (dbo.Notification ownership) is resolved from PortalKey in-statement —
            // explicit dto.PortalKey wins, otherwise it's derived from the notification type.
            const string sql = @"
                INSERT INTO dbo.[Notification]
                    (UserId, Title, Message, NotificationType, ReferenceId, ActorUserId, DetailsJson, PortalId)
                VALUES
                    (@UserId, @Title, @Message, @NotificationType, @ReferenceId, @ActorUserId, @DetailsJson,
                     (SELECT PortalId FROM dbo.Portal WHERE PortalKey = @PortalKey))";

            var portalKey = string.IsNullOrWhiteSpace(dto.PortalKey)
                ? NotificationType.PortalKeyFor(dto.NotificationType)
                : dto.PortalKey;

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId",           dto.UserId);
                cmd.Parameters.AddWithValue("@Title",            Truncate(dto.Title,   200));
                cmd.Parameters.AddWithValue("@Message",          Truncate(dto.Message, 1000));
                cmd.Parameters.AddWithValue("@NotificationType", Truncate(dto.NotificationType, 100));
                cmd.Parameters.AddWithValue("@ReferenceId",      (object)dto.ReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ActorUserId",      (object)dto.ActorUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DetailsJson",      (object)dto.DetailsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PortalKey",        portalKey);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] Create failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Fans a single notification out to every active user who currently has access to the
        /// given portal — role-based (dbo.UserRole → dbo.RolePortalAccess) OR an explicit
        /// per-user grant in dbo.UserPortalAccess, with an explicit per-user deny (IsGranted = 0)
        /// overriding role access. Mirrors the effective-access logic in
        /// Pages\Admin\Security\UserPortalAccessPage.cs. One set-based INSERT; best-effort.
        ///
        /// The actor (template.ActorUserId) DOES get a row — so the event shows in their own feed
        /// as a log entry — but it is inserted already-read, so it neither lights their bell badge
        /// nor toasts them right after they performed the action. Everyone else gets it unread.
        /// The template's UserId is ignored — recipients come from the audience query.
        /// PortalId is resolved from <paramref name="portalKey"/> and also used as the audience filter.
        /// </summary>
        public void CreateForPortalAudience(string portalKey, NotificationCreateDto template)
        {
            const string sql = @"
                INSERT INTO dbo.[Notification]
                    (UserId, Title, Message, NotificationType, ReferenceId, ActorUserId, DetailsJson, IsRead, PortalId)
                SELECT
                    u.UserId, @Title, @Message, @NotificationType, @ReferenceId, @ActorUserId, @DetailsJson,
                    CASE WHEN @ActorUserId IS NOT NULL AND u.UserId = @ActorUserId THEN 1 ELSE 0 END,
                    (SELECT PortalId FROM dbo.Portal WHERE PortalKey = @PortalKey)
                FROM dbo.[User] u
                WHERE u.IsActive = 1
                  AND (
                        EXISTS (
                            SELECT 1 FROM dbo.UserPortalAccess upa
                            INNER JOIN dbo.Portal p ON p.PortalId = upa.PortalId
                            WHERE upa.UserId = u.UserId AND p.PortalKey = @PortalKey AND upa.IsGranted = 1)
                        OR (
                            NOT EXISTS (
                                SELECT 1 FROM dbo.UserPortalAccess upa
                                INNER JOIN dbo.Portal p ON p.PortalId = upa.PortalId
                                WHERE upa.UserId = u.UserId AND p.PortalKey = @PortalKey AND upa.IsGranted = 0)
                            AND EXISTS (
                                SELECT 1 FROM dbo.UserRole ur
                                INNER JOIN dbo.RolePortalAccess rpa ON rpa.RoleId = ur.RoleId
                                INNER JOIN dbo.Portal p ON p.PortalId = rpa.PortalId
                                WHERE ur.UserId = u.UserId AND p.PortalKey = @PortalKey)
                          )
                      )";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@Title",            Truncate(template.Title,   200));
                cmd.Parameters.AddWithValue("@Message",          Truncate(template.Message, 1000));
                cmd.Parameters.AddWithValue("@NotificationType", Truncate(template.NotificationType, 100));
                cmd.Parameters.AddWithValue("@ReferenceId",      (object)template.ReferenceId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@ActorUserId",      (object)template.ActorUserId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@DetailsJson",      (object)template.DetailsJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@PortalKey",        portalKey);
                con.Open();
                int rows = cmd.ExecuteNonQuery();
                Core.Logger.LogInfo($"[NotificationRepository] CreateForPortalAudience('{portalKey}', {template.NotificationType}) -> {rows} recipient(s)");
                if (rows == 0)
                    Core.Logger.LogWarning($"[NotificationRepository] CreateForPortalAudience wrote 0 rows — no active user has '{portalKey}' access (check dbo.UserRole/RolePortalAccess/UserPortalAccess).");
            }
            catch (Exception ex)
            {
                Core.Logger.LogError("[NotificationRepository] CreateForPortalAudience failed", ex);
            }
        }

        public List<NotificationDto> GetByUser(int userId, int limit = 50)
        {
            const string sql = @"
                SELECT TOP (@Limit)
                    NotificationId, UserId, Title, Message,
                    NotificationType, ReferenceId, ActorUserId, DetailsJson, IsRead, CreatedDate
                FROM dbo.[Notification]
                WHERE UserId = @UserId
                ORDER BY CreatedDate DESC";

            var list = new List<NotificationDto>();
            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@Limit",  limit);
                con.Open();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(MapRow(r));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetByUser failed: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// True when a notification with this exact Type + ReferenceId already exists for the user.
        /// Used by RepairPortalNotificationPoller so re-scanning the same 12-hour ticket window on
        /// every poll (and across multiple sessions of the same user) never inserts a duplicate row.
        /// </summary>
        public bool ExistsByTypeAndReference(int userId, string notificationType, int referenceId)
        {
            const string sql = @"
                SELECT CASE WHEN EXISTS (
                    SELECT 1 FROM dbo.[Notification]
                    WHERE UserId = @UserId
                      AND NotificationType = @NotificationType
                      AND ReferenceId = @ReferenceId
                ) THEN 1 ELSE 0 END";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId",           userId);
                cmd.Parameters.AddWithValue("@NotificationType", Truncate(notificationType, 100));
                cmd.Parameters.AddWithValue("@ReferenceId",      referenceId);
                con.Open();
                return cmd.ExecuteScalar() is int flag && flag == 1;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] ExistsByTypeAndReference failed: {ex.Message}");
                // Fail "already exists" so a transient DB error can't cause a notification flood.
                return true;
            }
        }

        // ── Portal-scoped variants ───────────────────────────────────────────
        // dbo.Notification is shared by every portal. A portal's bell must only ever show / clear
        // / mark-read its OWN rows — filtered by the dbo.Notification.PortalId FK
        // (Migration_Notification_AddPortalId.sql). PortalKey → PortalId is resolved in-statement.

        private const string PortalIdSubquery =
            "PortalId = (SELECT PortalId FROM dbo.Portal WHERE PortalKey = @PortalKey)";

        public List<NotificationDto> GetByUserAndPortal(int userId, string portalKey, int limit = 50)
        {
            var list = new List<NotificationDto>();
            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = $@"
                    SELECT TOP (@Limit)
                        NotificationId, UserId, Title, Message,
                        NotificationType, ReferenceId, ActorUserId, DetailsJson, IsRead, CreatedDate
                    FROM dbo.[Notification]
                    WHERE UserId = @UserId AND {PortalIdSubquery}
                    ORDER BY CreatedDate DESC";
                cmd.Parameters.AddWithValue("@UserId",    userId);
                cmd.Parameters.AddWithValue("@Limit",     limit);
                cmd.Parameters.AddWithValue("@PortalKey", portalKey);
                con.Open();
                using var r = cmd.ExecuteReader();
                while (r.Read())
                    list.Add(MapRow(r));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetByUserAndPortal failed: {ex.Message}");
            }
            return list;
        }

        public int GetUnreadCountByPortal(int userId, string portalKey)
        {
            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = $@"
                    SELECT COUNT(*) FROM dbo.[Notification]
                    WHERE UserId = @UserId AND IsRead = 0 AND {PortalIdSubquery}";
                cmd.Parameters.AddWithValue("@UserId",    userId);
                cmd.Parameters.AddWithValue("@PortalKey", portalKey);
                con.Open();
                return cmd.ExecuteScalar() is int count ? count : 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUnreadCountByPortal failed: {ex.Message}");
                return 0;
            }
        }

        public void MarkAllAsReadByPortal(int userId, string portalKey)
        {
            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = $@"
                    UPDATE dbo.[Notification] SET IsRead = 1
                    WHERE UserId = @UserId AND IsRead = 0 AND {PortalIdSubquery}";
                cmd.Parameters.AddWithValue("@UserId",    userId);
                cmd.Parameters.AddWithValue("@PortalKey", portalKey);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] MarkAllAsReadByPortal failed: {ex.Message}");
            }
        }

        public void ClearAllByPortal(int userId, string portalKey)
        {
            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand { Connection = con };
                cmd.CommandText = $@"
                    DELETE FROM dbo.[Notification]
                    WHERE UserId = @UserId AND {PortalIdSubquery}";
                cmd.Parameters.AddWithValue("@UserId",    userId);
                cmd.Parameters.AddWithValue("@PortalKey", portalKey);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] ClearAllByPortal failed: {ex.Message}");
            }
        }

        public int GetUnreadCount(int userId)
        {
            const string sql = @"
                SELECT COUNT(*)
                FROM dbo.[Notification]
                WHERE UserId = @UserId AND IsRead = 0";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                con.Open();
                var result = cmd.ExecuteScalar();
                return result is int count ? count : 0;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUnreadCount failed: {ex.Message}");
                return 0;
            }
        }

        public void MarkAsRead(int notificationId, int userId)
        {
            const string sql = @"
                UPDATE dbo.[Notification]
                SET IsRead = 1
                WHERE NotificationId = @NotificationId AND UserId = @UserId";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@NotificationId", notificationId);
                cmd.Parameters.AddWithValue("@UserId",         userId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] MarkAsRead failed: {ex.Message}");
            }
        }

        public void MarkAllAsRead(int userId)
        {
            const string sql = @"
                UPDATE dbo.[Notification]
                SET IsRead = 1
                WHERE UserId = @UserId AND IsRead = 0";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] MarkAllAsRead failed: {ex.Message}");
            }
        }

        public void ClearAll(int userId)
        {
            const string sql = "DELETE FROM dbo.[Notification] WHERE UserId = @UserId";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@UserId", userId);
                con.Open();
                cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] ClearAll failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resolves the requester's UserId from a CartridgeAuthorization record.
        /// Used by SupervisorApprovalPage to send Authorization Status notifications.
        /// Mirrors the web portal's NotificationRepository.GetUserIdByAuthorizationIdAsync.
        /// </summary>
        public int? GetUserIdByAuthorizationId(int authorizationId)
        {
            // Direct: SubmittedByUserId is stored at submission time for desktop self-requests.
            // This is the most reliable path — no EmpId link required.
            const string sqlDirect = @"
                SELECT ca.SubmittedByUserId
                FROM dbo.CartridgeAuthorization ca
                WHERE ca.AuthorizationId = @AuthorizationId
                  AND ca.SubmittedByUserId IS NOT NULL";

            // Primary: CartridgeAuthorization.EmployeeId → User.EmpId (direct link)
            // Fallback for older records or IT-assisted requests.
            const string sqlPrimary = @"
                SELECT u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.AuthorizationId = @AuthorizationId";

            // Secondary fallback: via employee email chain.
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
                using var con = new SqlConnection(GetConnectionString());
                con.Open();

                using (var cmd = new SqlCommand(sqlDirect, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    var result = cmd.ExecuteScalar();
                    if (result is int id)
                    {
                        Debug.WriteLine($"[NotificationRepository] GetUserIdByAuthorizationId: AuthId={authorizationId} → UserId={id} (SubmittedByUserId)");
                        return id;
                    }
                }

                using (var cmd = new SqlCommand(sqlPrimary, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    var result = cmd.ExecuteScalar();
                    if (result is int id)
                    {
                        Debug.WriteLine($"[NotificationRepository] GetUserIdByAuthorizationId: AuthId={authorizationId} → UserId={id} (EmpId join)");
                        return id;
                    }
                }

                using (var cmd = new SqlCommand(sqlFallback, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    var result = cmd.ExecuteScalar();
                    if (result is int id)
                    {
                        Debug.WriteLine($"[NotificationRepository] GetUserIdByAuthorizationId: AuthId={authorizationId} → UserId={id} (email fallback)");
                        return id;
                    }
                }

                Debug.WriteLine($"[NotificationRepository] GetUserIdByAuthorizationId: no User found for AuthId={authorizationId}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUserIdByAuthorizationId failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves a UserId directly from an Employee's EmpId.
        /// Used as a guaranteed fallback when the authorization-based lookup returns null.
        /// </summary>
        public int? GetUserIdByEmployeeId(int empId)
        {
            const string sql = @"
                SELECT TOP 1 UserId
                FROM dbo.[User]
                WHERE EmpId = @EmpId";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                using var cmd = new SqlCommand(sql, con);
                cmd.Parameters.AddWithValue("@EmpId", empId);
                con.Open();
                var result = cmd.ExecuteScalar();
                if (result is int id)
                {
                    Debug.WriteLine($"[NotificationRepository] GetUserIdByEmployeeId: EmpId={empId} → UserId={id}");
                    return id;
                }
                Debug.WriteLine($"[NotificationRepository] GetUserIdByEmployeeId: no User linked to EmpId={empId}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUserIdByEmployeeId failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves the requester's UserId from a CartridgeApproval record.
        /// Used to send Request Status notifications when a supervisor approves/rejects
        /// the initial cartridge request approval (email-token-based flow).
        /// Mirrors the web portal's NotificationRepository.GetUserIdByApprovalIdAsync.
        /// </summary>
        public int? GetUserIdByApprovalId(int approvalId)
        {
            // Primary: CartridgeApproval.EmpId → User.EmpId (direct link)
            const string sqlPrimary = @"
                SELECT u.UserId
                FROM dbo.CartridgeApproval ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmpId
                WHERE ca.ApprovalId = @ApprovalId";

            // Fallback: CartridgeApproval.EmpId → EmployeeEmail → EmailAddress → User.EmailAddress
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
                using var con = new SqlConnection(GetConnectionString());
                con.Open();

                using (var cmd = new SqlCommand(sqlPrimary, con))
                {
                    cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                    var result = cmd.ExecuteScalar();
                    if (result is int id)
                    {
                        Debug.WriteLine($"[NotificationRepository] GetUserIdByApprovalId: ApprovalId={approvalId} → UserId={id} (primary)");
                        return id;
                    }
                }

                Debug.WriteLine($"[NotificationRepository] GetUserIdByApprovalId: primary path returned no result for ApprovalId={approvalId} — trying email fallback");

                using (var cmd = new SqlCommand(sqlFallback, con))
                {
                    cmd.Parameters.AddWithValue("@ApprovalId", approvalId);
                    var result = cmd.ExecuteScalar();
                    if (result is int id)
                    {
                        Debug.WriteLine($"[NotificationRepository] GetUserIdByApprovalId: ApprovalId={approvalId} → UserId={id} (email fallback)");
                        return id;
                    }
                }

                Debug.WriteLine($"[NotificationRepository] GetUserIdByApprovalId: no User found for ApprovalId={approvalId} — Request Status notification will not be delivered");
                return null;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUserIdByApprovalId failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Resolves the requester's UserId and AuthorizationId for a fulfillment notification.
        ///
        /// Mirrors the web app's proven path (CartridgeAuthorization.EmployeeId → User.EmpId),
        /// which already works for Authorization Approved notifications.
        ///
        /// Primary:  exact SubmissionSessionId match on CartridgeAuthorization.
        /// Fallback: most recent CartridgeAuthorization for the same EmployeeId.
        ///
        /// Does NOT join through dbo.Request, avoiding issues with NULL SubmissionSessionId
        /// or CreatedBy mismatches on desktop-submitted requests.
        /// </summary>
        public (int? UserId, int? AuthorizationId) GetUserAndAuthIdForFulfillment(int empId, Guid? sessionId)
        {
            // Primary: exact session match on Approved authorization (mirrors CartridgeManagement filter).
            const string sqlSession = @"
                SELECT TOP 1 ca.AuthorizationId, u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.SubmissionSessionId = @SessionId
                  AND ca.Status = 'Approved'";

            // Fallback: most recent Approved authorization for this employee.
            const string sqlEmpId = @"
                SELECT TOP 1 ca.AuthorizationId, u.UserId
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.[User] u ON u.EmpId = ca.EmployeeId
                WHERE ca.EmployeeId = @EmpId
                  AND ca.Status = 'Approved'
                ORDER BY ca.CreatedDate DESC";

            try
            {
                using var con = new SqlConnection(GetConnectionString());
                con.Open();

                if (sessionId.HasValue)
                {
                    using var cmd = new SqlCommand(sqlSession, con);
                    cmd.Parameters.AddWithValue("@SessionId", sessionId.Value);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        int authId = r.GetInt32(0);
                        int userId = r.GetInt32(1);
                        Debug.WriteLine($"[NotificationRepository] GetUserAndAuthIdForFulfillment: session match — EmpId={empId}, AuthId={authId}, UserId={userId}");
                        return (userId, authId);
                    }
                }

                using (var cmd = new SqlCommand(sqlEmpId, con))
                {
                    cmd.Parameters.AddWithValue("@EmpId", empId);
                    using var r = cmd.ExecuteReader();
                    if (r.Read())
                    {
                        int authId = r.GetInt32(0);
                        int userId = r.GetInt32(1);
                        Debug.WriteLine($"[NotificationRepository] GetUserAndAuthIdForFulfillment: EmpId fallback — EmpId={empId}, AuthId={authId}, UserId={userId}");
                        return (userId, authId);
                    }
                }

                Debug.WriteLine($"[NotificationRepository] GetUserAndAuthIdForFulfillment: no row found — EmpId={empId}, SessionId={sessionId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetUserAndAuthIdForFulfillment failed: {ex.Message}");
            }
            return (null, null);
        }

        /// <summary>
        /// Resolves a CartridgeAuthorization.AuthorizationId to the SetCode of its portal request.
        /// Joins via SubmissionSessionId so each authorization maps to its exact session, not just
        /// the most-recent request for that employee.
        /// </summary>
        public string GetSetCodeByAuthorizationId(int authorizationId)
        {
            const string sql = @"
                SELECT TOP 1 s.SetCode
                FROM dbo.CartridgeAuthorization ca
                INNER JOIN dbo.Request r ON r.SubmissionSessionId = ca.SubmissionSessionId
                INNER JOIN dbo.[Set]   s ON s.SetId = r.SetId
                WHERE ca.AuthorizationId = @AuthorizationId
                  AND s.SetCode IS NOT NULL";

            try
            {
                using (var con = new SqlConnection(GetConnectionString()))
                using (var cmd = new SqlCommand(sql, con))
                {
                    cmd.Parameters.AddWithValue("@AuthorizationId", authorizationId);
                    con.Open();
                    var result = cmd.ExecuteScalar();
                    return result as string;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[NotificationRepository] GetSetCodeByAuthorizationId failed: {ex.Message}");
                return null;
            }
        }

        private static NotificationDto MapRow(SqlDataReader r) => new NotificationDto
        {
            NotificationId   = r.GetInt32(r.GetOrdinal("NotificationId")),
            UserId           = r.GetInt32(r.GetOrdinal("UserId")),
            Title            = r.GetString(r.GetOrdinal("Title")),
            Message          = r.GetString(r.GetOrdinal("Message")),
            NotificationType = r.GetString(r.GetOrdinal("NotificationType")),
            ReferenceId      = r.IsDBNull(r.GetOrdinal("ReferenceId")) ? (int?)null : r.GetInt32(r.GetOrdinal("ReferenceId")),
            ActorUserId      = r.IsDBNull(r.GetOrdinal("ActorUserId")) ? (int?)null : r.GetInt32(r.GetOrdinal("ActorUserId")),
            DetailsJson      = r.IsDBNull(r.GetOrdinal("DetailsJson")) ? null : r.GetString(r.GetOrdinal("DetailsJson")),
            IsRead           = r.GetBoolean(r.GetOrdinal("IsRead")),
            CreatedDate      = r.GetDateTime(r.GetOrdinal("CreatedDate")),
        };

        private static string Truncate(string value, int max)
            => value == null ? string.Empty
             : value.Length <= max ? value
             : value.Substring(0, max);
    }
}
