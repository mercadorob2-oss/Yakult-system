using System;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class CallMonitoringRepository
    {
        public async Task<CallMonthlyReport> GetMonthlyDetailedReportAsync(DateTime monthLocal)
        {
            var month = new DateTime(monthLocal.Year, monthLocal.Month, 1);
            var from = TimeZoneInfo.ConvertTimeToUtc(month, CallMonthlyReport.ReportZone);
            var end = TimeZoneInfo.ConvertTimeToUtc(month.AddMonths(1), CallMonthlyReport.ReportZone);
            var generated = DateTime.UtcNow;
            if (from > generated) throw new ArgumentException("Choose the current month or an earlier month.");
            var cutoff = end < generated ? end : generated;
            var report = new CallMonthlyReport { MonthLocal = month, FromUtc = from, CutoffUtc = cutoff, GeneratedUtc = generated };
            using (var connection = new SqlConnection(GetConnectionString()))
            {
                if (!await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketHistory"))
                    throw new InvalidOperationException("Ticket history is required for a monthly report.");
                var hasNotes = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallTicketNote");
                var hasVisits = await CallSchemaGate.TableExistsAsync(connection, "dbo.CallFieldVisit");
                var hasAttachments = hasVisits && await CallSchemaGate.TableExistsAsync(connection, "dbo.CallFieldVisitAttachment");
                var hasBranch = await CallSchemaGate.ColumnExistsAsync(connection, "dbo.CallTicket", "BranchId")
                    && await CallSchemaGate.TableExistsAsync(connection, "dbo.Branch");
                var noteActivity = hasNotes ? @" OR EXISTS (SELECT 1 FROM dbo.CallTicketNote n WHERE n.TicketId=t.TicketId AND n.CreatedAt>=@From AND n.CreatedAt<@End)" : "";
                var visitActivity = hasVisits ? @" OR EXISTS (SELECT 1 FROM dbo.CallFieldVisit v WHERE v.TicketId=t.TicketId AND
                    ((v.CreatedAt>=@From AND v.CreatedAt<@End) OR (v.UpdatedAt>=@From AND v.UpdatedAt<@End)
                    OR (v.ScheduledAt>=@From AND v.ScheduledAt<@End) OR (v.CompletedAt>=@From AND v.CompletedAt<@End)))" : "";
                // First later OldValue permits reconstruction even if the ticket has no earlier status event.
                var sql = @"
SELECT t.TicketId, t.TicketCode, t.CreatedAt, t.UpdatedAt,
 c.Name AS Company, d.Name AS Department, " + (hasBranch ? "b.Name" : "CAST(NULL AS nvarchar(300))") + @" AS Branch,
 t.CallerName, t.IssueType, t.Priority, e.Name AS ResponsiblePerson, t.Issue, t.ProvidedSolution,
 CASE WHEN t.CreatedAt>=@From THEN 'Not yet created' ELSE
 COALESCE(NULLIF(beforeStart.NewValue,''), NULLIF(afterStart.OldValue,''),
 CASE WHEN t.UpdatedAt<@From THEN t.Status END, 'Unknown') END AS OpeningStatus,
 COALESCE(NULLIF(beforeEnd.NewValue,''), NULLIF(afterEnd.OldValue,''),
 CASE WHEN t.UpdatedAt<@End THEN t.Status END, 'Unknown') AS ClosingStatus
INTO #MonthlyTickets
FROM dbo.CallTicket t
LEFT JOIN dbo.Company c ON c.ComId=t.ComId
LEFT JOIN dbo.Department d ON d.DeptId=t.DeptId
LEFT JOIN dbo.Employee e ON e.EmpId=t.AssignedToEmpId
" + (hasBranch ? "LEFT JOIN dbo.Branch b ON b.BranchId=t.BranchId" : "") + @"
OUTER APPLY (SELECT TOP 1 NewValue FROM dbo.CallTicketHistory h WHERE h.TicketId=t.TicketId AND h.FieldName='Status' AND h.ChangedAt<@From ORDER BY h.ChangedAt DESC,h.HistoryId DESC) beforeStart
OUTER APPLY (SELECT TOP 1 OldValue FROM dbo.CallTicketHistory h WHERE h.TicketId=t.TicketId AND h.FieldName='Status' AND h.ChangedAt>=@From ORDER BY h.ChangedAt,h.HistoryId) afterStart
OUTER APPLY (SELECT TOP 1 NewValue FROM dbo.CallTicketHistory h WHERE h.TicketId=t.TicketId AND h.FieldName='Status' AND h.ChangedAt<@End ORDER BY h.ChangedAt DESC,h.HistoryId DESC) beforeEnd
OUTER APPLY (SELECT TOP 1 OldValue FROM dbo.CallTicketHistory h WHERE h.TicketId=t.TicketId AND h.FieldName='Status' AND h.ChangedAt>=@End ORDER BY h.ChangedAt,h.HistoryId) afterEnd
WHERE t.CreatedAt<@End;
DELETE t FROM #MonthlyTickets t WHERE t.CreatedAt<@From
 AND t.OpeningStatus IN ('Solved','Closed') AND t.ClosingStatus IN ('Solved','Closed')
 AND NOT ((t.UpdatedAt>=@From AND t.UpdatedAt<@End)
 OR EXISTS (SELECT 1 FROM dbo.CallTicketHistory h WHERE h.TicketId=t.TicketId AND h.ChangedAt>=@From AND h.ChangedAt<@End)
 " + noteActivity + visitActivity + @");
SELECT * FROM #MonthlyTickets ORDER BY CreatedAt,TicketId;
SELECT h.HistoryId,h.TicketId,h.ChangedAt,h.ChangedByUserId,u.Name AS ChangedByName,h.FieldName,h.OldValue,h.NewValue,h.Note
FROM dbo.CallTicketHistory h JOIN #MonthlyTickets t ON t.TicketId=h.TicketId
LEFT JOIN dbo.[User] u ON u.UserId=h.ChangedByUserId WHERE h.ChangedAt<@End ORDER BY h.ChangedAt,h.HistoryId;
";
                if (hasNotes) sql += @"
SELECT n.NoteId,n.TicketId,n.NoteType,n.NoteText,n.CreatedAt,n.CreatedByUserId,u.Name AS CreatedByName
FROM dbo.CallTicketNote n JOIN #MonthlyTickets t ON t.TicketId=n.TicketId
LEFT JOIN dbo.[User] u ON u.UserId=n.CreatedByUserId WHERE n.CreatedAt<@End ORDER BY n.CreatedAt,n.NoteId;";
                if (hasVisits) sql += @"
SELECT v.FieldVisitId,v.TicketId,v.CreatedAt,v.UpdatedAt,v.ScheduledAt,v.CompletedAt,v.Status,
 e.Name AS TechnicianName,v.Notes,CAST(CASE WHEN DATALENGTH(v.CustomerSignature)>0 THEN 1 ELSE 0 END AS bit) AS HasSignature,
 " + (hasAttachments ? "(SELECT COUNT(*) FROM dbo.CallFieldVisitAttachment a WHERE a.FieldVisitId=v.FieldVisitId AND a.UploadedAt<@End)" : "0") + @" AS AttachmentCount
FROM dbo.CallFieldVisit v JOIN #MonthlyTickets t ON t.TicketId=v.TicketId
LEFT JOIN dbo.Employee e ON e.EmpId=v.TechnicianEmpId WHERE v.CreatedAt<@End ORDER BY v.CreatedAt,v.FieldVisitId;";
                sql += @"
SELECT i.ItemId,i.Name,i.ModelNumber,i.SerialNumber FROM dbo.Item i
WHERE EXISTS (SELECT 1 FROM dbo.CallTicketHistory h JOIN #MonthlyTickets t ON t.TicketId=h.TicketId
 WHERE h.ChangedAt<@End AND ((h.FieldName IN ('ReplacementOldItemId','ReplacementNewItemId') AND TRY_CONVERT(int,h.NewValue)=i.ItemId)
 OR (h.FieldName='ReplacementBatchItem' AND (TRY_CONVERT(int,PARSENAME(REPLACE(h.NewValue,',','.'),3))=i.ItemId
 OR TRY_CONVERT(int,PARSENAME(REPLACE(h.NewValue,',','.'),2))=i.ItemId))));
DROP TABLE #MonthlyTickets;";
                using (var result = await connection.QueryMultipleAsync(sql, new { From = from, End = cutoff }, commandTimeout: 180))
                {
                    report.Tickets = (await result.ReadAsync<CallMonthlyTicket>()).ToList();
                    report.History = (await result.ReadAsync<CallTicketHistoryItem>()).ToList();
                    if (hasNotes) report.Notes = (await result.ReadAsync<CallTicketNoteItem>()).ToList();
                    if (hasVisits) report.Visits = (await result.ReadAsync<CallMonthlyVisit>()).ToList();
                    report.Items = (await result.ReadAsync<CallMonthlyItem>()).ToList();
                }
                if (!hasNotes) report.Warnings.Add("Ticket notes table is unavailable.");
                if (!hasVisits) report.Warnings.Add("Field work table is unavailable.");
                if (!hasAttachments) report.Warnings.Add("Field work attachment counts are unavailable.");
                if (report.Tickets.Any(t => t.OpeningStatus == "Unknown" || t.ClosingStatus == "Unknown"))
                    report.Warnings.Add("Some historical statuses are unknown. Those tickets are retained; backlog totals show confirmed statuses separately.");
                return report;
            }
        }
    }
}
