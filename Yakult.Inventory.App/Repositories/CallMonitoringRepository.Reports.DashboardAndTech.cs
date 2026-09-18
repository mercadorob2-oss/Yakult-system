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
        public async Task<List<CallTechResolutionSampleRow>> GetTechResolutionSamplesAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 5000)
        {
            if (empId <= 0) throw new ArgumentOutOfRangeException(nameof(empId));
            if (maxRows < 1) maxRows = 1;
            if (maxRows > 20000) maxRows = 20000;

            const string sql = @"
WITH Completed AS
(
    SELECT
        h.TicketId,
        MAX(h.ChangedAt) AS CompletedAtUtc
    FROM dbo.CallTicketHistory h
    WHERE h.FieldName = 'Status'
      AND h.NewValue IN ('Solved', 'Resolved (Temporary)', 'Closed')
      AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
    GROUP BY h.TicketId
)
SELECT TOP (@MaxRows)
    t.TicketId,
    t.CreatedAt,
    c.CompletedAtUtc,
    t.Priority,
    t.IssueType
FROM Completed c
INNER JOIN dbo.CallTicket t ON t.TicketId = c.TicketId
WHERE t.AssignedToEmpId = @EmpId
ORDER BY c.CompletedAtUtc DESC, t.TicketId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTechResolutionSampleRow>(
                    sql,
                    new
                    {
                        EmpId = empId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        MaxRows = maxRows
                    });

                return rows?.ToList() ?? new List<CallTechResolutionSampleRow>();
            }
        }

        public sealed class CallTechResolutionSampleRow
        {
            public int TicketId { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime CompletedAtUtc { get; set; }
            public string Priority { get; set; }
            public string IssueType { get; set; }
        }

        public async Task<CallTechProfileSummary> GetTechProfileSummaryAsync(int empId, DateTime fromUtc, DateTime toUtc)
        {
            const string lookupSql = @"
SELECT TOP 1
    e.EmpId,
    e.Name AS EmployeeName,
    u.UserId
FROM dbo.Employee e
LEFT JOIN dbo.[User] u ON u.EmpId = e.EmpId
WHERE e.EmpId = @EmpId;";

            const string assignedCountsSql = @"
SELECT
    t.Status,
    COUNT(1) AS Cnt
FROM dbo.CallTicket t
WHERE t.AssignedToEmpId = @EmpId
GROUP BY t.Status;";

            const string handledAsAssigneeSql = @"
WITH Completed AS (
    SELECT
        h.TicketId,
        MAX(h.ChangedAt) AS CompletedAt
    FROM dbo.CallTicketHistory h
    WHERE h.FieldName = 'Status'
      AND h.NewValue IN ('Solved', 'Resolved (Temporary)', 'Closed')
      AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
    GROUP BY h.TicketId
)
SELECT COUNT(1)
FROM Completed c
JOIN dbo.CallTicket t ON t.TicketId = c.TicketId
WHERE t.AssignedToEmpId = @EmpId;";

            const string handledAsSolverSql = @"
SELECT COUNT(DISTINCT h.TicketId)
FROM dbo.CallTicketHistory h
WHERE h.FieldName = 'Status'
  AND h.NewValue IN ('Solved', 'Resolved (Temporary)', 'Closed')
  AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
  AND h.ChangedByUserId = @UserId;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var lookup = await connection.QuerySingleOrDefaultAsync<CallTechProfileSummary>(
                    lookupSql,
                    new { EmpId = empId });

                if (lookup == null)
                    throw new InvalidOperationException("Employee not found.");

                lookup.FromUtc = fromUtc;
                lookup.ToUtc = toUtc;

                var assignedCounts = (await connection.QueryAsync<CallStatusCountRow>(
                        assignedCountsSql,
                        new { EmpId = empId }))
                    .ToList();

                int GetCount(string status)
                {
                    return assignedCounts
                        .Where(x => string.Equals(x.Status, status, StringComparison.OrdinalIgnoreCase))
                        .Select(x => x.Cnt)
                        .FirstOrDefault();
                }

                lookup.AssignedPending = GetCount("Pending");
                lookup.AssignedInProgress = GetCount("In Progress");
                lookup.AssignedEscalated = GetCount("Escalated");

                lookup.AssignedOpenTotal = assignedCounts
                    .Where(x =>
                        !string.Equals(x.Status, "Solved", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(x.Status, "Closed", StringComparison.OrdinalIgnoreCase))
                    .Sum(x => x.Cnt);

                lookup.HandledAsAssigneeSolvedClosed = await connection.ExecuteScalarAsync<int>(
                    handledAsAssigneeSql,
                    new { EmpId = empId, FromUtc = fromUtc, ToUtc = toUtc });

                if (lookup.UserId.HasValue && lookup.UserId.Value > 0)
                {
                    lookup.HandledAsSolverSolvedClosed = await connection.ExecuteScalarAsync<int>(
                        handledAsSolverSql,
                        new { UserId = lookup.UserId.Value, FromUtc = fromUtc, ToUtc = toUtc });
                }
                else
                {
                    lookup.HandledAsSolverSolvedClosed = 0;
                }

                return lookup;
            }
        }

        private sealed class CallStatusCountRow
        {
            public string Status { get; set; }
            public int Cnt { get; set; }
        }

        public async Task<List<CallTechHandledTicketRow>> GetTechHandledTicketsAsync(
            int empId,
            DateTime fromUtc,
            DateTime toUtc,
            int maxRows = 200)
        {
            const string userIdSql = @"SELECT TOP 1 u.UserId FROM dbo.[User] u WHERE u.EmpId = @EmpId;";

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
                    ? "\n LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId"
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
        h2.NewValue AS Status,
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
     t.CreatedAt,
     t.DeptId,
     d.Name AS Department," +
                     branchIdSelect +
                     branchNameSelect +
                     @"
    t.Issue,
    cd.Status,
    cd.CompletedAt AS CompletedAtUtc,
    cd.ChangedByUserId AS CompletedByUserId,
    u.Name AS CompletedBy,
    t.AssignedToEmpId,
    e.Name AS AssignedToName,
    t.Priority
FROM CompletedDetail cd
JOIN dbo.CallTicket t ON t.TicketId = cd.TicketId
LEFT JOIN dbo.[User] u ON u.UserId = cd.ChangedByUserId
LEFT JOIN dbo.Employee e ON e.EmpId = t.AssignedToEmpId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId" +
                    branchJoin +
                    @"
WHERE t.AssignedToEmpId = @EmpId
   OR (@UserId IS NOT NULL AND cd.ChangedByUserId = @UserId)
ORDER BY cd.CompletedAt DESC;";

                int? userId = await connection.ExecuteScalarAsync<int?>(userIdSql, new { EmpId = empId });
                var rows = await connection.QueryAsync<CallTechHandledTicketRow>(
                    sql,
                    new
                    {
                        EmpId = empId,
                        UserId = userId,
                        FromUtc = fromUtc,
                        ToUtc = toUtc,
                        MaxRows = maxRows
                    });
                var list = rows.ToList();
                foreach (var r in list)
                {
                    r.CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc);
                    if (r.CompletedAtUtc.HasValue)
                        r.CompletedAtUtc = DateTime.SpecifyKind(r.CompletedAtUtc.Value, DateTimeKind.Utc);
                }
                return list;
            }
        }

        public async Task<List<CallTechOpenTicketRow>> GetOpenTicketsAssignedToEmployeeAsync(int empId, int maxRows = 300)
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
                    ? "\n LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId"
                    : string.Empty;

                var sql = @"
SELECT TOP (@MaxRows)
    t.TicketId,
    t.TicketCode,
    t.DeptId,
    d.Name AS Department," +
                    branchIdSelect +
                    branchNameSelect +
                    @"
    t.Issue,
    t.Status,
    t.Priority,
    t.CreatedAt,
    t.UpdatedAt
FROM dbo.CallTicket t
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId" +
                    branchJoin +
                    @"
WHERE t.AssignedToEmpId = @EmpId
  AND t.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
ORDER BY t.UpdatedAt DESC, t.CreatedAt DESC;";

                var rows = await connection.QueryAsync<CallTechOpenTicketRow>(
                    sql,
                    new { EmpId = empId, MaxRows = maxRows });
                var list = rows.ToList();
                foreach (var r in list)
                {
                    r.CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc);
                    r.UpdatedAt = DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc);
                }
                return list;
            }
        }

        public async Task<CallDashboardMetrics> GetDashboardMetricsAsync()
        {
            const string sql = @"SELECT TOP 1 OpenTickets, CriticalTickets, AvgResolutionMinutes, TodaysVolume FROM dbo.vw_Call_DashboardMetrics;";
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                return await connection.QuerySingleAsync<CallDashboardMetrics>(sql);
            }
        }

        public async Task<List<CallTicketVolumePoint>> GetTicketVolumeByDayAsync(DateTime fromUtc, DateTime toUtcExclusive)
        {
            const string sql = @"
SELECT [Day], TicketCount
FROM
(
    SELECT
        CONVERT(date, t.CreatedAt) AS [Day],
        COUNT(*) AS TicketCount
    FROM dbo.CallTicket t
    WHERE t.CreatedAt >= @FromUtc
      AND t.CreatedAt < @ToUtcExclusive
    GROUP BY CONVERT(date, t.CreatedAt)
) src
ORDER BY [Day];";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = (await connection.QueryAsync<CallTicketVolumePoint>(sql, new { FromUtc = fromUtc, ToUtcExclusive = toUtcExclusive }))?.ToList()
                    ?? new List<CallTicketVolumePoint>();

                var countsByDay = rows
                    .GroupBy(r => r.Day.Date)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.TicketCount));

                var startDayUtc = fromUtc.Date;
                var endDayUtc = toUtcExclusive.Date.AddDays(-1);
                var rangeLength = Math.Max(1, (endDayUtc - startDayUtc).Days + 1);
                var result = new List<CallTicketVolumePoint>(capacity: rangeLength);

                for (var day = startDayUtc; day <= endDayUtc; day = day.AddDays(1))
                {
                    result.Add(new CallTicketVolumePoint
                    {
                        Day = day,
                        TicketCount = countsByDay.TryGetValue(day, out var count) ? count : 0
                    });
                }

                return result;
            }
        }

        public Task<List<CallTicketVolumePoint>> GetTicketVolumeLast7DaysAsync()
        {
            var toUtcExclusive = DateTime.UtcNow.Date.AddDays(1);
            var fromUtc = toUtcExclusive.AddDays(-7);
            return GetTicketVolumeByDayAsync(fromUtc, toUtcExclusive);
        }

        public async Task<List<CallIssueTypePoint>> GetIssueTypeDistributionAsync()
        {
            const string sql = @"
 SELECT IssueType, TicketCount
 FROM dbo.vw_Call_IssueTypeDistribution
 ORDER BY TicketCount DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallIssueTypePoint>(sql);
                return rows.ToList();
            }
        }

        public async Task<List<(string Bucket, int TicketCount)>> GetResolutionBucketDistributionAsync(DateTime fromUtc, DateTime toUtc)
        {
            const string sql = @"
SELECT
    Bucket =
        CASE
            WHEN t.Status = 'Resolved (Temporary)' AND res.NewValue = 'Replacement' THEN 'Temporary Replacement'
            WHEN t.Status = 'Resolved (Temporary)' AND res.NewValue = 'Service Only' THEN 'Temporary Service'
            WHEN res.NewValue = 'Service Only' THEN 'Repair'
            WHEN res.NewValue = 'Replacement' THEN 'Replacement'
            ELSE COALESCE(NULLIF(LTRIM(RTRIM(res.NewValue)), ''), 'Other')
        END,
    TicketCount = COUNT(*)
FROM dbo.CallTicket t
OUTER APPLY (
    SELECT TOP 1 h.HistoryId, h.ChangedAt, h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = t.TicketId
      AND h.FieldName = 'ResolutionType'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) res
WHERE res.HistoryId IS NOT NULL
  AND res.ChangedAt >= @FromUtc AND res.ChangedAt <= @ToUtc
  AND t.Status IN ('Solved', 'Resolved (Temporary)', 'Closed')
  AND res.NewValue IN ('Service Only', 'Replacement')
GROUP BY
    CASE
        WHEN t.Status = 'Resolved (Temporary)' AND res.NewValue = 'Replacement' THEN 'Temporary Replacement'
            WHEN t.Status = 'Resolved (Temporary)' AND res.NewValue = 'Service Only' THEN 'Temporary Service'
        WHEN res.NewValue = 'Service Only' THEN 'Repair'
        WHEN res.NewValue = 'Replacement' THEN 'Replacement'
        ELSE COALESCE(NULLIF(LTRIM(RTRIM(res.NewValue)), ''), 'Other')
    END
ORDER BY TicketCount DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync(sql, new { FromUtc = fromUtc, ToUtc = toUtc });
                var result = new List<(string Bucket, int TicketCount)>();
                foreach (var r in rows)
                {
                    string bucket = r.Bucket;
                    int count = r.TicketCount;
                    result.Add((bucket, count));
                }

                return result;
            }
        }

        public async Task<List<(string Bucket, int TicketCount)>> GetActiveTicketStatusDistributionAsync()
        {
            const string sql = @"
SELECT
    Bucket =
        CASE
            WHEN t.Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN 'Solved'
            WHEN t.Status = 'Pending' THEN 'Pending'
            ELSE 'In Progress'
        END,
    TicketCount = COUNT(*)
FROM dbo.CallTicket t
GROUP BY
    CASE
        WHEN t.Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN 'Solved'
        WHEN t.Status = 'Pending' THEN 'Pending'
        ELSE 'In Progress'
    END
ORDER BY TicketCount DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync(sql);
                var result = new List<(string Bucket, int TicketCount)>();
                foreach (var r in rows)
                {
                    string bucket = r.Bucket;
                    int count = r.TicketCount;
                    result.Add((bucket, count));
                }

                return result;
            }
        }

        public async Task<List<CallTicketListItem>> GetTicketsByActiveStatusBucketAsync(string bucket, int maxRows = 500)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                throw new ArgumentException("Bucket is required.", nameof(bucket));

            const string sql = @"
SELECT TOP (@MaxRows)
    v.*
FROM dbo.vw_Call_TicketList v
WHERE (
        CASE
            WHEN v.Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN 'Solved'
            WHEN v.Status = 'Pending' THEN 'Pending'
            ELSE 'In Progress'
        END
      ) = @Bucket
ORDER BY
    ISNULL(v.UpdatedAt, v.CreatedAt) DESC,
    v.TicketId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new { Bucket = bucket.Trim(), MaxRows = maxRows });
                return rows.ToList();
            }
        }

        public async Task<List<CallTicketListItem>> GetResolvedTicketsByResolutionBucketAsync(
            DateTime fromUtc,
            DateTime toUtc,
            string bucket,
            int maxRows = 250)
        {
            if (string.IsNullOrWhiteSpace(bucket))
                throw new ArgumentException("Bucket is required.", nameof(bucket));

            const string sql = @"
SELECT TOP (@MaxRows)
    v.*
FROM dbo.vw_Call_TicketList v
OUTER APPLY (
    SELECT TOP 1 h.HistoryId, h.ChangedAt, h.NewValue
    FROM dbo.CallTicketHistory h
    WHERE h.TicketId = v.TicketId
      AND h.FieldName = 'ResolutionType'
    ORDER BY h.ChangedAt DESC, h.HistoryId DESC
) res
WHERE res.HistoryId IS NOT NULL
  AND res.ChangedAt >= @FromUtc AND res.ChangedAt <= @ToUtc
  AND v.Status IN ('Solved', 'Resolved (Temporary)', 'Closed')
  AND (
        CASE
            WHEN v.Status = 'Resolved (Temporary)' AND res.NewValue = 'Replacement' THEN 'Temporary Replacement'
            WHEN v.Status = 'Resolved (Temporary)' AND res.NewValue = 'Service Only' THEN 'Temporary Service'
            WHEN res.NewValue = 'Service Only' THEN 'Repair'
            WHEN res.NewValue = 'Replacement' THEN 'Replacement'
            ELSE COALESCE(NULLIF(LTRIM(RTRIM(res.NewValue)), ''), 'Other')
        END
      ) = @Bucket
ORDER BY
    ISNULL(v.SolvedAt, v.UpdatedAt) DESC,
    v.UpdatedAt DESC,
    v.TicketId DESC;";

            using (var connection = new SqlConnection(GetConnectionString()))
            {
                var rows = await connection.QueryAsync<CallTicketListItem>(
                    sql,
                    new { FromUtc = fromUtc, ToUtc = toUtc, Bucket = bucket.Trim(), MaxRows = maxRows });
                return rows.ToList();
            }
        }
    }
}
