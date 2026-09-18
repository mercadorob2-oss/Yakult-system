using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Data access for user activity logs.
    /// Reads from vw_UserActivityDetailed (view over dbo.UserActivityLog + dbo.User).
    /// Writes to dbo.UserActivityLog via InsertLog.
    /// </summary>
    public class UserActivityRepository
    {
        private readonly string _connectionString;

        public UserActivityRepository()
        {
            _connectionString = DatabaseConfig.ConnectionString;
        }

        public UserActivityRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // ─────────────────────────────────────────────────────────────
        //  QUERY
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Returns activity records, applying all non-null filter fields.
        /// Source: vw_UserActivityDetailed (always) + dbo.AuditTrail (when filter.IncludeAuditTrail is true).
        /// Results are ordered newest-first; max 5 000 rows.
        /// </summary>
        public List<UserActivityLogDto> GetFiltered(UserActivityFilter filter)
        {
            var result = new List<UserActivityLogDto>();

            // Build dynamic WHERE clauses — applied to the combined set.
            // The refactored view exposes ActivityDate (not CreatedDate) and UserName (not Name).
            var where = new System.Text.StringBuilder("WHERE 1 = 1");
            if (filter.DateFrom.HasValue)
                where.Append(" AND ActivityDate >= @DateFrom");
            if (filter.DateTo.HasValue)
                where.Append(" AND ActivityDate < @DateTo");
            if (filter.UserId.HasValue)
                where.Append(" AND UserId = @UserId");
            if (!string.IsNullOrWhiteSpace(filter.ActionType))
                where.Append(" AND ActionType = @ActionType");
            if (!string.IsNullOrWhiteSpace(filter.EntityType))
                where.Append(" AND EntityType = @EntityType");

            // Base rows — always from vw_UserActivityDetailed.
            // Columns are UserName and ActivityDate in the refactored view.
            string innerLog = @"
                SELECT ActivityId, UserId, UserName, EmailAddress,
                       ActionType, EntityType, EntityId, Description, ActivityDate
                FROM vw_UserActivityDetailed";

            // Historical Invoice creates — from dbo.[Set] WHERE IsInvoice = 1.
            // Only included where no ActivityLogger row already exists for that SetId,
            // so re-running after new code is deployed never creates duplicates.
            // ActivityId uses -2000000 offset to avoid colliding with UserActivityLog or AuditTrail IDs.
            string innerHistoricalInvoices = @"
                UNION ALL
                SELECT
                    -2000000 - s.SetId                                      AS ActivityId,
                    s.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Invoice'                                                AS EntityType,
                    s.SetId                                                  AS EntityId,
                    'Invoice created (Set #' + CAST(s.SetId AS NVARCHAR(20))
                        + CASE WHEN s.DocumentNumber IS NOT NULL
                               THEN N' – Doc: ' + s.DocumentNumber ELSE N'' END
                        + N')'                                                   AS Description,
                    CAST(s.CreatedAt AS DATETIME2)                          AS ActivityDate
                FROM dbo.[Set] s
                LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                WHERE s.IsInvoice = 1
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.UserActivityLog ual
                      WHERE ual.EntityType = 'Invoice'
                        AND ual.EntityId   = s.SetId
                        AND ual.ActionType = 'Create'
                  )";

            // Historical Set/Dispatch creates — non-invoice Sets.
            // ActivityId uses -3000000 offset.
            string innerHistoricalSets = @"
                UNION ALL
                SELECT
                    -3000000 - s.SetId                                      AS ActivityId,
                    s.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Set'                                                    AS EntityType,
                    s.SetId                                                  AS EntityId,
                    CASE WHEN s.RenewalOfSetId IS NOT NULL
                         THEN 'Renewal set created (Set #' + CAST(s.SetId AS NVARCHAR(20))
                              + ', renewal of Set #' + CAST(s.RenewalOfSetId AS NVARCHAR(20)) + ')'
                         ELSE 'Set #' + CAST(s.SetId AS NVARCHAR(20)) + ' created'
                              + CASE WHEN s.DocumentNumber IS NOT NULL
                                     THEN N' – ' + s.DocumentNumber ELSE N'' END
                    END                                                      AS Description,
                    CAST(s.CreatedAt AS DATETIME2)                          AS ActivityDate
                FROM dbo.[Set] s
                LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
                WHERE (s.IsInvoice IS NULL OR s.IsInvoice = 0)
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.UserActivityLog ual
                      WHERE ual.EntityType = 'Set'
                        AND ual.EntityId   = s.SetId
                        AND ual.ActionType = 'Create'
                  )";

            // Historical Renewal operations — only 'Renewed' status rows are actual
            // renewal events; 'Active' baseline rows are created during Item add.
            // ActivityId uses -4000000 offset.
            string innerHistoricalRenewals = @"
                UNION ALL
                SELECT
                    -4000000 - r.RenewalId                                  AS ActivityId,
                    r.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Renewal'                                                AS EntityType,
                    r.RenewalId                                              AS EntityId,
                    'Item renewed (Renewal #' + CAST(r.RenewalId AS NVARCHAR(20))
                        + CASE WHEN r.ItemId IS NOT NULL
                               THEN N' – Item #' + CAST(r.ItemId AS NVARCHAR(20)) ELSE N'' END
                        + N')'                                               AS Description,
                    CAST(r.CreatedAt AS DATETIME2)                          AS ActivityDate
                FROM dbo.Renewals r
                LEFT JOIN dbo.[User] u ON r.CreatedBy = u.UserId
                WHERE r.RenewalStatus = 'Renewed'
                  AND NOT EXISTS (
                      SELECT 1 FROM dbo.UserActivityLog ual
                      WHERE ual.EntityType = 'Renewal'
                        AND ual.EntityId   = r.RenewalId
                        AND ual.ActionType = 'Create'
                  )";

            // Historical Branch creates — ActivityId uses -5000000 offset.
            string innerHistoricalBranches = @"
                UNION ALL
                SELECT
                    -5000000 - b.BranchId                                   AS ActivityId,
                    b.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Branch'                                                 AS EntityType,
                    b.BranchId                                               AS EntityId,
                    'Branch created: ' + b.Name                              AS Description,
                    CAST(b.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Branch b
                LEFT JOIN dbo.[User] u ON b.CreatedBy = u.UserId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Branch'
                      AND ual.EntityId   = b.BranchId
                      AND ual.ActionType = 'Create'
                )";

            // Historical Category creates — ActivityId uses -6000000 offset.
            string innerHistoricalCategories = @"
                UNION ALL
                SELECT
                    -6000000 - ic.CategoryId                                 AS ActivityId,
                    ic.CreatedBy                                             AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Category'                                               AS EntityType,
                    ic.CategoryId                                            AS EntityId,
                    'Category created: ' + ic.Name                           AS Description,
                    CAST(ic.DateCreated AS DATETIME2)                        AS ActivityDate
                FROM dbo.ItemCategory ic
                LEFT JOIN dbo.[User] u ON ic.CreatedBy = u.UserId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Category'
                      AND ual.EntityId   = ic.CategoryId
                      AND ual.ActionType = 'Create'
                )";

            // Historical Company creates — CreatedBy is nullable; rows without a creator are skipped.
            // ActivityId uses -7000000 offset.
            string innerHistoricalCompanies = @"
                UNION ALL
                SELECT
                    -7000000 - c.ComId                                       AS ActivityId,
                    c.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Company'                                                AS EntityType,
                    c.ComId                                                  AS EntityId,
                    'Company created: ' + c.Name                             AS Description,
                    CAST(c.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Company c
                LEFT JOIN dbo.[User] u ON c.CreatedBy = u.UserId
                WHERE c.CreatedBy IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Company'
                      AND ual.EntityId   = c.ComId
                      AND ual.ActionType = 'Create'
                  )";

            // Historical Department creates — ActivityId uses -8000000 offset.
            string innerHistoricalDepartments = @"
                UNION ALL
                SELECT
                    -8000000 - d.DeptId                                      AS ActivityId,
                    d.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Department'                                             AS EntityType,
                    d.DeptId                                                 AS EntityId,
                    'Department created: ' + d.Name                          AS Description,
                    CAST(d.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Department d
                LEFT JOIN dbo.[User] u ON d.CreatedBy = u.UserId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Department'
                      AND ual.EntityId   = d.DeptId
                      AND ual.ActionType = 'Create'
                )";

            // Historical Employee creates — CreatedBy is nullable; rows without a creator are skipped.
            // ActivityId uses -9000000 offset.
            string innerHistoricalEmployees = @"
                UNION ALL
                SELECT
                    -9000000 - e.EmpId                                       AS ActivityId,
                    e.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Employee'                                               AS EntityType,
                    e.EmpId                                                  AS EntityId,
                    'Employee created: ' + e.Name                            AS Description,
                    CAST(e.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Employee e
                LEFT JOIN dbo.[User] u ON e.CreatedBy = u.UserId
                WHERE e.CreatedBy IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Employee'
                      AND ual.EntityId   = e.EmpId
                      AND ual.ActionType = 'Create'
                  )";

            // Historical Item creates — ActivityId uses -10000000 offset.
            string innerHistoricalItems = @"
                UNION ALL
                SELECT
                    -10000000 - i.ItemId                                     AS ActivityId,
                    i.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Item'                                                   AS EntityType,
                    i.ItemId                                                 AS EntityId,
                    'Item created: ' + i.Name                                AS Description,
                    CAST(i.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Item i
                LEFT JOIN dbo.[User] u ON i.CreatedBy = u.UserId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Item'
                      AND ual.EntityId   = i.ItemId
                      AND ual.ActionType = 'Create'
                )";

            // Historical Request creates — ActivityId uses -11000000 offset.
            string innerHistoricalRequests = @"
                UNION ALL
                SELECT
                    -11000000 - r.ReqId                                      AS ActivityId,
                    r.CreatedBy                                              AS UserId,
                    u.Name                                                   AS UserName,
                    u.EmailAddress                                           AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'Request'                                                AS EntityType,
                    r.ReqId                                                  AS EntityId,
                    'Request #' + CAST(r.ReqId AS NVARCHAR(20)) + ' created' AS Description,
                    CAST(r.DateCreated AS DATETIME2)                         AS ActivityDate
                FROM dbo.Request r
                LEFT JOIN dbo.[User] u ON r.CreatedBy = u.UserId
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'Request'
                      AND ual.EntityId   = r.ReqId
                      AND ual.ActionType = 'Create'
                )";

            // Historical BorrowLog borrow events — ActivityId uses -12000000 offset.
            string innerHistoricalBorrows = @"
                UNION ALL
                SELECT
                    -12000000 - bl.BorrowId                                  AS ActivityId,
                    bl.BorrowEncodedByUserId                                 AS UserId,
                    bl.BorrowEncodedByUserName                               AS UserName,
                    NULL                                                     AS EmailAddress,
                    'Create'                                                 AS ActionType,
                    'BorrowLog'                                              AS EntityType,
                    bl.BorrowId                                              AS EntityId,
                    'Item borrowed: ' + bl.ItemName
                        + ' (SN: ' + bl.SerialNumber + ')'
                        + ' by ' + bl.BorrowedByEmpName                      AS Description,
                    CAST(bl.BorrowedAtUtc AS DATETIME2)                      AS ActivityDate
                FROM dbo.BorrowLog bl
                WHERE NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'BorrowLog'
                      AND ual.EntityId   = bl.BorrowId
                      AND ual.ActionType = 'Create'
                )";

            // Historical BorrowLog return events — ActivityId uses -13000000 offset.
            // Only included when a return has been recorded.
            string innerHistoricalReturns = @"
                UNION ALL
                SELECT
                    -13000000 - bl.BorrowId                                  AS ActivityId,
                    bl.ReturnEncodedByUserId                                 AS UserId,
                    bl.ReturnEncodedByUserName                               AS UserName,
                    NULL                                                     AS EmailAddress,
                    'Update'                                                 AS ActionType,
                    'BorrowLog'                                              AS EntityType,
                    bl.BorrowId                                              AS EntityId,
                    'Item returned: ' + bl.ItemName
                        + ' (SN: ' + bl.SerialNumber + ')'
                        + ' by ' + ISNULL(bl.ReturnedByEmpName, '(unknown)') AS Description,
                    CAST(bl.ReturnedAtUtc AS DATETIME2)                      AS ActivityDate
                FROM dbo.BorrowLog bl
                WHERE bl.ReturnedAtUtc IS NOT NULL
                  AND bl.ReturnEncodedByUserId IS NOT NULL
                  AND NOT EXISTS (
                    SELECT 1 FROM dbo.UserActivityLog ual
                    WHERE ual.EntityType = 'BorrowLog'
                      AND ual.EntityId   = bl.BorrowId
                      AND ual.ActionType = 'Update'
                  )";

            // Optional UNION with dbo.AuditTrail.
            // AuditTrail.Id is negated so IDs never collide with UserActivityLog IDs.
            // Aliases must match the view column names (UserName, ActivityDate).
            string innerAudit = @"
                UNION ALL
                SELECT
                    -at.Id                         AS ActivityId,
                    ISNULL(at.UserId, 0)           AS UserId,
                    at.UserName                    AS UserName,
                    NULL                           AS EmailAddress,
                    at.Action                      AS ActionType,
                    at.EntityType                  AS EntityType,
                    at.EntityId                    AS EntityId,
                    at.Notes                       AS Description,
                    at.Timestamp                   AS ActivityDate
                FROM dbo.AuditTrail at";

            string combined = innerLog
                + innerHistoricalInvoices
                + innerHistoricalSets
                + innerHistoricalRenewals
                + innerHistoricalBranches
                + innerHistoricalCategories
                + innerHistoricalCompanies
                + innerHistoricalDepartments
                + innerHistoricalEmployees
                + innerHistoricalItems
                + innerHistoricalRequests
                + innerHistoricalBorrows
                + innerHistoricalReturns
                + (filter.IncludeAuditTrail ? innerAudit : string.Empty);

            string sql = $@"
                SELECT TOP 5000
                    ActivityId, UserId, UserName, EmailAddress,
                    ActionType, EntityType, EntityId,
                    Description, ActivityDate
                FROM ({combined}) combined
                {where}
                ORDER BY ActivityDate DESC";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                if (filter.DateFrom.HasValue)
                    cmd.Parameters.AddWithValue("@DateFrom", filter.DateFrom.Value.Date);
                if (filter.DateTo.HasValue)
                    // DateTo is inclusive for the UI, so shift to next midnight for < comparison
                    cmd.Parameters.AddWithValue("@DateTo", filter.DateTo.Value.Date.AddDays(1));
                if (filter.UserId.HasValue)
                    cmd.Parameters.AddWithValue("@UserId", filter.UserId.Value);
                if (!string.IsNullOrWhiteSpace(filter.ActionType))
                    cmd.Parameters.AddWithValue("@ActionType", filter.ActionType.Trim());
                if (!string.IsNullOrWhiteSpace(filter.EntityType))
                    cmd.Parameters.AddWithValue("@EntityType", filter.EntityType.Trim());

                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        result.Add(MapRow(reader));
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns all active users with their role, ordered by name, for the User filter dropdown.
        /// Format: "Name – RoleName" (one row per user).
        /// </summary>
        public List<(int UserId, string Name)> GetDistinctUsers()
        {
            var result = new List<(int, string)>();
            const string sql = @"
                SELECT u.UserId,
                       u.Name + ' – ' + ISNULL(r.RoleName, 'No Role') AS DisplayName
                FROM dbo.[User] u
                OUTER APPLY (
                    SELECT TOP 1 ro.RoleName
                    FROM dbo.UserRole ur
                    INNER JOIN dbo.Role ro ON ro.RoleId = ur.RoleId
                    WHERE ur.UserId = u.UserId
                    ORDER BY ro.RoleName
                ) r
                WHERE u.IsActive = 1
                ORDER BY u.Name";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        int uid = reader.GetInt32(reader.GetOrdinal("UserId"));
                        string name = reader.IsDBNull(reader.GetOrdinal("DisplayName")) ? "(unknown)" : reader.GetString(reader.GetOrdinal("DisplayName"));
                        result.Add((uid, name));
                    }
                }
            }
            return result;
        }

        /// <summary>Returns the standard action types used by ActivityLogger for the filter dropdown.</summary>
        public List<string> GetDistinctActionTypes()
        {
            return new List<string>
            {
                "Approve", "Create", "Delete", "Export", "Import",
                "Reject", "ResetPassword", "Update"
            };
        }

        /// <summary>Returns distinct EntityType values for the filter dropdown, excluding internal types.</summary>
        public List<string> GetDistinctEntityTypes()
        {
            var result = new List<string>();

            string sql = @"
                SELECT DISTINCT Val
                FROM (
                    SELECT [EntityType] AS Val FROM dbo.UserActivityLog
                    UNION ALL
                    SELECT [EntityType] AS Val FROM dbo.AuditTrail
                    UNION ALL
                    SELECT Val FROM (VALUES
                        ('BorrowLog'),('Branch'),('Category'),('Company'),('Department'),
                        ('Employee'),('Invoice'),('Item'),('Renewal'),
                        ('Request'),('Set'),('Vendor')
                    ) t(Val)
                ) s
                WHERE Val IS NOT NULL
                  AND LTRIM(RTRIM(Val)) <> ''
                  AND Val NOT IN ('SetItemUpdate', 'UserActivityLog')
                ORDER BY Val";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(reader.GetString(0));
                }
            }
            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  WRITE
        // ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Inserts one row into dbo.UserActivityLog.
        /// Called by ActivityLogger; exceptions are intentionally not swallowed here
        /// so the caller can decide on error handling policy.
        /// </summary>
        public void InsertLog(
            int userId,
            string actionType,
            string entityType,
            int? entityId,
            string description,
            string ipAddress = null,
            string userAgent = null)
        {
            const string sql = @"
                INSERT INTO dbo.UserActivityLog
                    (UserId, ActionType, EntityType, EntityId, Description, CreatedDate, IpAddress, UserAgent)
                VALUES
                    (@UserId, @ActionType, @EntityType, @EntityId, @Description, SYSUTCDATETIME(), @IpAddress, @UserAgent)";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@UserId", userId);
                cmd.Parameters.AddWithValue("@ActionType", Truncate(actionType, 50));
                cmd.Parameters.AddWithValue("@EntityType", Truncate(entityType, 50));
                cmd.Parameters.AddWithValue("@EntityId", (object)entityId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Description", (object)Truncate(description, 500) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@IpAddress", (object)Truncate(ipAddress, 45) ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@UserAgent", (object)Truncate(userAgent, 255) ?? DBNull.Value);
                con.Open();
                cmd.ExecuteNonQuery();
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  HELPERS
        // ─────────────────────────────────────────────────────────────

        private List<string> GetDistinctColumn(string columnName)
        {
            var result = new List<string>();
            if (!string.Equals(columnName, "ActionType", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(columnName, "EntityType", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentOutOfRangeException(nameof(columnName), columnName, "Unsupported distinct column.");
            }

            string auditColumn = string.Equals(columnName, "ActionType", StringComparison.OrdinalIgnoreCase)
                ? "at.[Action]"
                : "at.[EntityType]";

            string sql = $@"
                SELECT DISTINCT Val
                FROM (
                    SELECT [{columnName}] AS Val
                    FROM dbo.UserActivityLog
                    UNION ALL
                    SELECT {auditColumn} AS Val
                    FROM dbo.AuditTrail at
                ) s
                WHERE Val IS NOT NULL AND LTRIM(RTRIM(Val)) <> ''
                ORDER BY Val";

            using (var con = new SqlConnection(_connectionString))
            using (var cmd = new SqlCommand(sql, con))
            {
                con.Open();
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        result.Add(reader.GetString(0));
                }
            }
            return result;
        }

        private static UserActivityLogDto MapRow(SqlDataReader reader)
        {
            int activityIdOrd = reader.GetOrdinal("ActivityId");
            return new UserActivityLogDto
            {
                ActivityId   = reader.IsDBNull(activityIdOrd) ? 0 : reader.GetInt32(activityIdOrd),
                UserId       = reader.GetInt32(reader.GetOrdinal("UserId")),
                Name         = reader.IsDBNull(reader.GetOrdinal("UserName"))     ? null : reader.GetString(reader.GetOrdinal("UserName")),
                EmailAddress = reader.IsDBNull(reader.GetOrdinal("EmailAddress")) ? null : reader.GetString(reader.GetOrdinal("EmailAddress")),
                ActionType   = reader.IsDBNull(reader.GetOrdinal("ActionType"))   ? null : reader.GetString(reader.GetOrdinal("ActionType")),
                EntityType   = reader.IsDBNull(reader.GetOrdinal("EntityType"))   ? null : reader.GetString(reader.GetOrdinal("EntityType")),
                EntityId     = reader.IsDBNull(reader.GetOrdinal("EntityId"))     ? (int?)null : reader.GetInt32(reader.GetOrdinal("EntityId")),
                Description  = reader.IsDBNull(reader.GetOrdinal("Description"))  ? null : reader.GetString(reader.GetOrdinal("Description")),
                CreatedDate  = reader.GetDateTime(reader.GetOrdinal("ActivityDate"))
            };
        }

        private static string Truncate(string value, int maxLength)
        {
            if (value == null) return null;
            return value.Length <= maxLength ? value : value.Substring(0, maxLength);
        }
    }
}
