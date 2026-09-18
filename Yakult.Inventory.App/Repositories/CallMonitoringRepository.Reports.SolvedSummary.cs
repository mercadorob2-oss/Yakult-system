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
        // Date range is based on SolvedAt/CompletedAt (i.e., when status moved to Solved/Resolved Temporary).
        public async Task<List<CallSolvedSummaryReportRow>> GetSolvedSummaryReportAsync(DateTime fromUtc, DateTime toUtc, int maxRows = 2000)
        {
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicket"))
                    return new List<CallSolvedSummaryReportRow>();

                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory"))
                    return new List<CallSolvedSummaryReportRow>();

                var hasBranchId = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId");
                var hasBranchTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");
                var hasCompanyTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.Company");
                var hasProvidedSolution = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "ProvidedSolution");
                var hasCallerName = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "CallerName");
                var hasNoteTable = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketNote");

                string noteOrderBy = null;
                if (hasNoteTable)
                {
                    var noteDateColumn = await GetFirstExistingColumnAsync(
                        connection,
                        "dbo.CallTicketNote",
                        new[] { "CreatedAt", "CreatedOn", "CreatedDate", "DateCreated", "Created" });

                    noteOrderBy = noteDateColumn != null
                        ? $"n.[{noteDateColumn}] DESC, n.NoteId DESC"
                        : "n.NoteId DESC";
                }

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

                var companySelect = hasCompanyTable
                    ? "\n    c.Name AS Company,"
                    : "\n    CAST(NULL AS nvarchar(200)) AS Company,";

                var companyJoin = hasCompanyTable
                    ? "\nLEFT JOIN dbo.Company c ON c.ComId = t.ComId"
                    : string.Empty;

                var callerSelect = hasCallerName
                    ? "\n    t.CallerName,"
                    : "\n    CAST(NULL AS nvarchar(200)) AS CallerName,";

                var providedSolutionSelect = hasProvidedSolution
                    ? "t.ProvidedSolution"
                    : "CAST(NULL AS nvarchar(max))";

                var solutionExpr = hasNoteTable
                    ? $"COALESCE(sol.NoteText, {providedSolutionSelect})"
                    : providedSolutionSelect;

                var solApply = hasNoteTable
                    ? $@"
OUTER APPLY (
    SELECT TOP 1 n.NoteText
    FROM dbo.CallTicketNote n
    WHERE n.TicketId = t.TicketId
      AND n.NoteType = 'Solution'
    ORDER BY {noteOrderBy}
) sol"
                    : string.Empty;

                // Note: use a single interpolated string so all dynamic fragments are expanded.
                var sql = $@"
WITH Completed AS (
    SELECT
        h.TicketId,
        MAX(h.ChangedAt) AS CompletedAt
    FROM dbo.CallTicketHistory h
    WHERE h.FieldName = 'Status'
      AND h.NewValue IN ('Solved', 'Resolved (Temporary)')
      AND h.ChangedAt >= @FromUtc AND h.ChangedAt <= @ToUtc
    GROUP BY h.TicketId
),
CompletedDetail AS (
    SELECT
        c.TicketId,
        c.CompletedAt,
        h2.NewValue AS CompletedStatus
    FROM Completed c
    OUTER APPLY (
        SELECT TOP 1 h.HistoryId, h.NewValue
        FROM dbo.CallTicketHistory h
        WHERE h.TicketId = c.TicketId
          AND h.FieldName = 'Status'
          AND h.NewValue IN ('Solved', 'Resolved (Temporary)')
          AND h.ChangedAt = c.CompletedAt
        ORDER BY h.HistoryId DESC
    ) h2
)
SELECT TOP (@MaxRows)
    t.TicketId,
    t.TicketCode,
    t.CreatedAt AS CreatedAtUtc,
    cd.CompletedAt AS SolvedAtUtc,{callerSelect}{companySelect}
    COALESCE(deptSnap.NewValue, d.Name) AS Department,{branchIdSelect}{branchNameSelect}
    COALESCE(respSnap.NewValue, eAssignee.Name) AS ResponsiblePerson,
    t.Priority,
    cd.CompletedStatus AS FinalStatus,
    t.Issue AS Problem,
    {solutionExpr} AS Solution,
    CAST(CASE WHEN cd.CompletedAt IS NULL THEN NULL ELSE DATEDIFF(MINUTE, t.CreatedAt, cd.CompletedAt) / 60.0 END AS float) AS ResolutionHours
FROM CompletedDetail cd
JOIN dbo.CallTicket t ON t.TicketId = cd.TicketId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId" +
                    $"{companyJoin}{branchJoin}" +
                    $@"
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
{solApply}
ORDER BY cd.CompletedAt DESC, t.TicketId DESC;";

                var rows = await connection.QueryAsync<CallSolvedSummaryReportRow>(
                    sql,
                    new { FromUtc = fromUtc, ToUtc = toUtc, MaxRows = maxRows });

                return (rows ?? Enumerable.Empty<CallSolvedSummaryReportRow>())
                    .Select(r =>
                    {
                        if (r == null) return null;

                        r.CreatedAtUtc = r.CreatedAtUtc.Kind == DateTimeKind.Utc ? r.CreatedAtUtc : DateTime.SpecifyKind(r.CreatedAtUtc, DateTimeKind.Utc);
                        r.SolvedAtUtc = r.SolvedAtUtc.Kind == DateTimeKind.Utc ? r.SolvedAtUtc : DateTime.SpecifyKind(r.SolvedAtUtc, DateTimeKind.Utc);

                        r.TicketCode = (r.TicketCode ?? string.Empty).Trim();
                        r.Company = (r.Company ?? string.Empty).Trim();
                        r.CallerName = (r.CallerName ?? string.Empty).Trim();
                        r.Department = (r.Department ?? string.Empty).Trim();
                        r.Branch = (r.Branch ?? string.Empty).Trim();
                        r.ResponsiblePerson = (r.ResponsiblePerson ?? string.Empty).Trim();
                        r.Priority = (r.Priority ?? string.Empty).Trim();
                        r.FinalStatus = (r.FinalStatus ?? string.Empty).Trim();
                        r.Problem = (r.Problem ?? string.Empty).Trim();
                        r.Solution = (r.Solution ?? string.Empty).Trim();

                        if (string.IsNullOrWhiteSpace(r.Company)) r.Company = null;
                        if (string.IsNullOrWhiteSpace(r.CallerName)) r.CallerName = null;
                        if (string.IsNullOrWhiteSpace(r.Department)) r.Department = null;
                        if (string.IsNullOrWhiteSpace(r.Branch)) r.Branch = null;
                        if (string.IsNullOrWhiteSpace(r.ResponsiblePerson)) r.ResponsiblePerson = null;
                        if (string.IsNullOrWhiteSpace(r.Priority)) r.Priority = null;
                        if (string.IsNullOrWhiteSpace(r.FinalStatus)) r.FinalStatus = null;
                        if (string.IsNullOrWhiteSpace(r.Problem)) r.Problem = null;
                        if (string.IsNullOrWhiteSpace(r.Solution)) r.Solution = null;

                        if (r.ResolutionHours < 0) r.ResolutionHours = 0;
                        return r;
                    })
                    .Where(r => r != null)
                    .ToList();
            }
        }
    }
}
