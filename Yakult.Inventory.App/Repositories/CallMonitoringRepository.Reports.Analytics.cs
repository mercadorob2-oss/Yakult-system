using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task<List<CallEmployeeReplacementItemRow>> GetEmployeeReplacementItemsAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 2000)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 20000) maxRows = 20000;

            const string userIdSql = "SELECT TOP 1 u.UserId FROM dbo.[User] u WHERE u.EmpId = @EmpId;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId");
                var hasBranchTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");

                var branchJoin = hasBranchId && hasBranchTable
                    ? "\nLEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId"
                    : string.Empty;

                int? userId = await connection.ExecuteScalarAsync<int?>(userIdSql, new { EmpId = empId });
                if (!userId.HasValue || userId.Value <= 0)
                    return new List<CallEmployeeReplacementItemRow>();

                var sql = @"
SELECT TOP (@MaxRows)
    t.TicketId,
    t.TicketCode,
    t.Status AS TicketStatus,
    res.ChangedAt AS ResolutionMarkedAtUtc,
    res.NewValue AS ResolutionType,
    TRY_CONVERT(int, oldItem.NewValue) AS ReplacementOldItemId,
    oldItemName = CONCAT(oi.Name, CASE WHEN oi.ModelNumber IS NULL OR LTRIM(RTRIM(oi.ModelNumber)) = '' THEN '' ELSE CONCAT(' (', oi.ModelNumber, ')') END,
                         CASE WHEN oi.SerialNumber IS NULL OR LTRIM(RTRIM(oi.SerialNumber)) = '' THEN '' ELSE CONCAT(' - ', oi.SerialNumber) END),
    TRY_CONVERT(int, newItem.NewValue) AS ReplacementNewItemId,
    newItemName = CONCAT(ni.Name, CASE WHEN ni.ModelNumber IS NULL OR LTRIM(RTRIM(ni.ModelNumber)) = '' THEN '' ELSE CONCAT(' (', ni.ModelNumber, ')') END,
                         CASE WHEN ni.SerialNumber IS NULL OR LTRIM(RTRIM(ni.SerialNumber)) = '' THEN '' ELSE CONCAT(' - ', ni.SerialNumber) END),
    TRY_CONVERT(int, qty.NewValue) AS ReplacementQty,
    res.Note AS Remarks
FROM dbo.CallTicket t" +
                          branchJoin +
                          @"
OUTER APPLY (
    SELECT TOP 1 h.HistoryId, h.ChangedAt, h.ChangedByUserId, h.NewValue, h.Note
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId
      AND h.FieldName = 'ResolutionType'
      AND h.NewValue = 'Replacement'
      AND h.ChangedByUserId = @UserId
      AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) res
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementOldItemId'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) oldItem
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementNewItemId'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) newItem
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementQty'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) qty
LEFT JOIN dbo.Item oi ON oi.ItemId = TRY_CONVERT(int, oldItem.NewValue)
LEFT JOIN dbo.Item ni ON ni.ItemId = TRY_CONVERT(int, newItem.NewValue)
WHERE res.HistoryId IS NOT NULL
ORDER BY res.ChangedAt DESC, t.TicketId DESC;";

                var rows = await connection.QueryAsync<CallEmployeeReplacementItemRow>(
                    sql,
                    new { UserId = userId.Value, FromUtc = fromUtc, ToUtc = toUtc, MaxRows = maxRows });

                return rows
                    .Select(r =>
                    {
                        if (string.IsNullOrWhiteSpace(r.ReplacementOldItem)) r.ReplacementOldItem = null;
                        if (string.IsNullOrWhiteSpace(r.ReplacementNewItem)) r.ReplacementNewItem = null;
                        return r;
                    })
                    .ToList();
            }
        }

        public async Task<List<CallResolutionReportRow>> GetResolutionReportAsync(DateTime fromUtc, DateTime toUtc, string resolutionType, int maxRows = 2000)
        {
            var type = (resolutionType ?? "All").Trim();
            var filterType = !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase);

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId");
                var hasBranchTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");

                var branchIdSelect = hasBranchId
                    ? "\n    t.BranchId,"
                    : "\n    CAST(NULL AS int) AS BranchId,";

                var branchNameSelect = hasBranchId && hasBranchTable
                    ? @"
    CASE
        WHEN b.BranchId IS NULL THEN NULL
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch,"
                    : "\n    CAST(NULL AS nvarchar(300)) AS Branch,";

                var branchJoin = hasBranchId && hasBranchTable
                    ? "\nLEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId"
                    : string.Empty;

                var sql = @"
SELECT TOP (@MaxRows)
    t.TicketId,
    t.TicketCode,
    t.Status AS TicketStatus,
    t.CreatedAt AS TicketCreatedAt,
    COALESCE(deptSnap.NewValue, d.Name) AS Department," +
                          branchIdSelect +
                          branchNameSelect +
                          @"
    COALESCE(respSnap.NewValue, eAssignee.Name) AS ResponsiblePerson,
    t.Priority,
    res.ChangedAt AS ResolutionMarkedAt,
    res.NewValue AS ResolutionType,
    u.Name AS MarkedByName,
    TRY_CONVERT(int, oldItem.NewValue) AS ReplacementOldItemId,
    oldItemName = CONCAT(oi.Name, CASE WHEN oi.ModelNumber IS NULL OR LTRIM(RTRIM(oi.ModelNumber)) = '' THEN '' ELSE CONCAT(' (', oi.ModelNumber, ')') END,
                         CASE WHEN oi.SerialNumber IS NULL OR LTRIM(RTRIM(oi.SerialNumber)) = '' THEN '' ELSE CONCAT(' - ', oi.SerialNumber) END),
    TRY_CONVERT(int, newItem.NewValue) AS ReplacementNewItemId,
    newItemName = CONCAT(ni.Name, CASE WHEN ni.ModelNumber IS NULL OR LTRIM(RTRIM(ni.ModelNumber)) = '' THEN '' ELSE CONCAT(' (', ni.ModelNumber, ')') END,
                         CASE WHEN ni.SerialNumber IS NULL OR LTRIM(RTRIM(ni.SerialNumber)) = '' THEN '' ELSE CONCAT(' - ', ni.SerialNumber) END),
    TRY_CONVERT(int, qty.NewValue) AS ReplacementQty,
    res.Note AS Remarks
FROM dbo.CallTicket t
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId" +
                          branchJoin +
                          @"
OUTER APPLY (
    SELECT TOP 1 h.HistoryId, h.ChangedAt, h.ChangedByUserId, h.NewValue, h.Note
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ResolutionType'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) res
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ResolutionDepartment'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) deptSnap
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ResolutionResponsiblePerson'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) respSnap
LEFT JOIN dbo.[User] u ON u.UserId = res.ChangedByUserId
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementOldItemId'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) oldItem
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementNewItemId'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) newItem
OUTER APPLY (
    SELECT TOP 1 h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId AND h.FieldName = 'ReplacementQty'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) qty
LEFT JOIN dbo.Item oi ON oi.ItemId = TRY_CONVERT(int, oldItem.NewValue)
LEFT JOIN dbo.Item ni ON ni.ItemId = TRY_CONVERT(int, newItem.NewValue)
WHERE res.HistoryId IS NOT NULL
  AND res.ChangedAt >= @FromUtc AND res.ChangedAt <= @ToUtc
  AND (@FilterType = 0 OR res.NewValue = @Type)
ORDER BY res.ChangedAt DESC, t.TicketId DESC;";

                var rows = await connection.QueryAsync<CallResolutionReportRow>(
                    sql,
                    new { FromUtc = fromUtc, ToUtc = toUtc, FilterType = filterType ? 1 : 0, Type = type, MaxRows = maxRows });

                return rows
                    .Select(r =>
                    {
                        if (string.IsNullOrWhiteSpace(r.ReplacementOldItem)) r.ReplacementOldItem = null;
                        if (string.IsNullOrWhiteSpace(r.ReplacementNewItem)) r.ReplacementNewItem = null;
                        return r;
                    })
                    .ToList();
            }
        }

        public async Task<List<CallSlaComplianceReportRow>> GetSlaComplianceReportAsync(DateTime fromUtc, DateTime toUtc, int maxRows = 2000)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId");
                var hasBranchTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");

                var branchIdSelect = hasBranchId
                    ? "\n    t.BranchId,"
                    : "\n    CAST(NULL AS int) AS BranchId,";

                var branchNameSelect = hasBranchId && hasBranchTable
                    ? @"
    CASE
        WHEN b.BranchId IS NULL THEN NULL
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch,"
                    : "\n    CAST(NULL AS nvarchar(300)) AS Branch,";

                var branchJoin = hasBranchId && hasBranchTable
                    ? "\nLEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId"
                    : string.Empty;

                var sql = @"
WITH Completed AS (
    SELECT
        h.TicketId,
        MAX(h.ChangedAt) AS CompletedAt
    FROM dbo.CallTicketHistory h
    WHERE h.FieldName = 'Status'
      AND h.NewValue IN ('Solved', 'Resolved (Temporary)', 'Closed')
      AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
    GROUP BY h.TicketId
),
CompletedDetail AS (
    SELECT
        c.TicketId,
        c.CompletedAt,
        h2.NewValue AS CompletedStatus,
        h2.ChangedByUserId
    FROM Completed c
    OUTER APPLY (
        SELECT TOP 1 h.HistoryId, h.NewValue, h.ChangedByUserId
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = c.TicketId
          AND h.FieldName = 'Status'
          AND h.NewValue IN ('Solved', 'Resolved (Temporary)', 'Closed')
          AND h.ChangedAt = c.CompletedAt
        ORDER BY h.HistoryId DESC
    ) h2
)
SELECT TOP (@MaxRows)
    t.TicketId,
    t.TicketCode,
    d.Name AS Department," +
                          branchIdSelect +
                          branchNameSelect +
                          @"
    t.Priority,
    eAssignee.Name AS AssignedToName,
    u.Name AS CompletedByName,
    cd.CompletedStatus,
    t.CreatedAt AS CreatedAtUtc,
    cd.CompletedAt AS CompletedAtUtc
FROM CompletedDetail cd
JOIN dbo.CallTicket t ON t.TicketId = cd.TicketId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId" +
                          branchJoin +
                          @"
LEFT JOIN dbo.[User] u ON u.UserId = cd.ChangedByUserId
ORDER BY cd.CompletedAt DESC, t.TicketId DESC;";

                var rows = await connection.QueryAsync<CallSlaComplianceReportRow>(
                    sql,
                    new { FromUtc = fromUtc, ToUtc = toUtc, MaxRows = maxRows });
                return rows.ToList();
            }
        }
    }
}
