<%@ WebHandler Language="C#" Class="RepairReportsHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>
/// Aggregated Repair Portal reports for the mobile Reports tab. Visible to all authenticated
/// users (not just technicians). Returns:
///  - isTechnician flag
///  - attendance today + recent history (technicians only, when table exists)
///  - ticket summary counts (scope-aware: global for technicians, "mine" for requesters)
///  - recent tickets for quick report access
/// Reuses the same filters and joins as repair-tickets.ashx / repair-attendance.ashx.
/// </summary>
public sealed class RepairReportsHandler : IHttpHandler
{
    public void ProcessRequest(HttpContext context)
    {
        RepairMobileApiSupport.Prepare(context, "GET, OPTIONS");
        if (RepairMobileApiSupport.IsOptions(context)) return;
        if (!string.Equals(context.Request.HttpMethod, "GET", StringComparison.OrdinalIgnoreCase))
        {
            RepairMobileApiSupport.Error(context, 405, "Method not allowed");
            return;
        }

        CallTicketApiUser actor;
        if (!RepairMobileApiSupport.TryRequireAuthenticated(context, out actor)) return;

        var pageSizeRaw = context.Request.QueryString["pageSize"];
        var recentLimit = RepairMobileApiSupport.ClampPageSize(pageSizeRaw, 20, 50);

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();

                var isTechnician = actor.IsItAuthorized;
                var hasAttendanceTable = RepairMobileApiSupport.HasTable(connection, "dbo.RepairTechnicianAttendance");

                // Attendance (technicians only)
                object today = null;
                var history = new List<object>();

                if (isTechnician && actor.EmployeeId.HasValue && hasAttendanceTable)
                {
                    using (var command = new SqlCommand(@"
SELECT TOP (1) AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
FROM dbo.RepairTechnicianAttendance
WHERE EmployeeId = @EmployeeId
  AND WorkDate = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time')
ORDER BY AttendanceId DESC;", connection))
                    {
                        command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.Value;
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                today = new
                                {
                                    attendanceId = RepairMobileApiSupport.IntValue(reader, "AttendanceId"),
                                    employeeId = RepairMobileApiSupport.IntValue(reader, "EmployeeId"),
                                    workDate = RepairMobileApiSupport.IsoDate(reader["WorkDate"]),
                                    timeIn = RepairMobileApiSupport.IsoUtc(reader["TimeIn"]),
                                    timeOut = RepairMobileApiSupport.IsoUtc(reader["TimeOut"])
                                };
                            }
                        }
                    }

                    using (var command = new SqlCommand(@"
SELECT TOP (30) AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
FROM dbo.RepairTechnicianAttendance
WHERE EmployeeId = @EmployeeId
ORDER BY WorkDate DESC, AttendanceId DESC;", connection))
                    {
                        command.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.Value;
                        using (var reader = command.ExecuteReader())
                            while (reader.Read()) history.Add(new
                            {
                                attendanceId = RepairMobileApiSupport.IntValue(reader, "AttendanceId"),
                                employeeId = RepairMobileApiSupport.IntValue(reader, "EmployeeId"),
                                workDate = RepairMobileApiSupport.IsoDate(reader["WorkDate"]),
                                timeIn = RepairMobileApiSupport.IsoUtc(reader["TimeIn"]),
                                timeOut = RepairMobileApiSupport.IsoUtc(reader["TimeOut"])
                            });
                    }
                }

                // Summary counts
                bool hasRepairTicketTable = RepairMobileApiSupport.HasTable(connection, "dbo.RepairTicket");
                if (!hasRepairTicketTable)
                {
                    RepairMobileApiSupport.Error(context, 503, "Repair tickets are not installed for this environment");
                    return;
                }

                object summary = null;
                if (isTechnician)
                {
                    using (var cmd = new SqlCommand(@"
SELECT
    SUM(CASE WHEN Status = 'Waiting' THEN 1 ELSE 0 END) AS Waiting,
    SUM(CASE WHEN Status = 'Diagnosing' THEN 1 ELSE 0 END) AS Diagnosing,
    SUM(CASE WHEN Status = 'Repairing' THEN 1 ELSE 0 END) AS Repairing,
    SUM(CASE WHEN Status = 'AwaitingParts' THEN 1 ELSE 0 END) AS AwaitingParts,
    SUM(CASE WHEN Status = 'Testing' THEN 1 ELSE 0 END) AS Testing,
    SUM(CASE WHEN Status = 'Completed' THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN Status = 'Unrepairable' THEN 1 ELSE 0 END) AS Unrepairable,
    COUNT(1) AS Total
FROM dbo.RepairTicket;", connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            int waiting = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                            int diagnosing = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                            int repairing = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                            int awaitingParts = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]);
                            int testing = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]);
                            int completed = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]);
                            int unrepairable = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader[6]);
                            int total = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader[7]);
                            summary = new
                            {
                                waiting = waiting,
                                diagnosing = diagnosing,
                                repairing = repairing,
                                awaitingParts = awaitingParts,
                                testing = testing,
                                completed = completed,
                                unrepairable = unrepairable,
                                total = total
                            };
                        }
                    }
                }
                else
                {
                    using (var cmd = new SqlCommand(@"
SELECT
    SUM(CASE WHEN t.Status = 'Waiting' THEN 1 ELSE 0 END) AS Waiting,
    SUM(CASE WHEN t.Status = 'Diagnosing' THEN 1 ELSE 0 END) AS Diagnosing,
    SUM(CASE WHEN t.Status = 'Repairing' THEN 1 ELSE 0 END) AS Repairing,
    SUM(CASE WHEN t.Status = 'AwaitingParts' THEN 1 ELSE 0 END) AS AwaitingParts,
    SUM(CASE WHEN t.Status = 'Testing' THEN 1 ELSE 0 END) AS Testing,
    SUM(CASE WHEN t.Status = 'Completed' THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN t.Status = 'Unrepairable' THEN 1 ELSE 0 END) AS Unrepairable,
    COUNT(1) AS Total
FROM dbo.RepairTicket t
WHERE (t.SubmittedByUserId = @UserId OR (@EmployeeId IS NOT NULL AND t.RequestedByEmpId = @EmployeeId));", connection))
                    {
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
                        cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int waiting = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                                int diagnosing = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                                int repairing = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                                int awaitingParts = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]);
                                int testing = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]);
                                int completed = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]);
                                int unrepairable = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader[6]);
                                int total = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader[7]);
                                summary = new
                                {
                                    waiting = waiting,
                                    diagnosing = diagnosing,
                                    repairing = repairing,
                                    awaitingParts = awaitingParts,
                                    testing = testing,
                                    completed = completed,
                                    unrepairable = unrepairable,
                                    total = total
                                };
                            }
                        }
                    }
                }

                // My summary for technicians
                object mySummary = null;
                if (isTechnician)
                {
                    using (var cmd = new SqlCommand(@"
SELECT
    SUM(CASE WHEN t.Status = 'Waiting' THEN 1 ELSE 0 END) AS Waiting,
    SUM(CASE WHEN t.Status = 'Diagnosing' THEN 1 ELSE 0 END) AS Diagnosing,
    SUM(CASE WHEN t.Status = 'Repairing' THEN 1 ELSE 0 END) AS Repairing,
    SUM(CASE WHEN t.Status = 'AwaitingParts' THEN 1 ELSE 0 END) AS AwaitingParts,
    SUM(CASE WHEN t.Status = 'Testing' THEN 1 ELSE 0 END) AS Testing,
    SUM(CASE WHEN t.Status = 'Completed' THEN 1 ELSE 0 END) AS Completed,
    SUM(CASE WHEN t.Status = 'Unrepairable' THEN 1 ELSE 0 END) AS Unrepairable,
    COUNT(1) AS Total
FROM dbo.RepairTicket t
WHERE (t.SubmittedByUserId = @UserId OR (@EmployeeId IS NOT NULL AND t.RequestedByEmpId = @EmployeeId));", connection))
                    {
                        cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = actor.UserId;
                        cmd.Parameters.Add("@EmployeeId", SqlDbType.Int).Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value;
                        using (var reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                int waiting = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader[0]);
                                int diagnosing = reader.IsDBNull(1) ? 0 : Convert.ToInt32(reader[1]);
                                int repairing = reader.IsDBNull(2) ? 0 : Convert.ToInt32(reader[2]);
                                int awaitingParts = reader.IsDBNull(3) ? 0 : Convert.ToInt32(reader[3]);
                                int testing = reader.IsDBNull(4) ? 0 : Convert.ToInt32(reader[4]);
                                int completed = reader.IsDBNull(5) ? 0 : Convert.ToInt32(reader[5]);
                                int unrepairable = reader.IsDBNull(6) ? 0 : Convert.ToInt32(reader[6]);
                                int total = reader.IsDBNull(7) ? 0 : Convert.ToInt32(reader[7]);
                                mySummary = new
                                {
                                    waiting = waiting,
                                    diagnosing = diagnosing,
                                    repairing = repairing,
                                    awaitingParts = awaitingParts,
                                    testing = testing,
                                    completed = completed,
                                    unrepairable = unrepairable,
                                    total = total
                                };
                            }
                        }
                    }
                }

                // Recent tickets
                var hasCallLink = RepairMobileApiSupport.HasColumn(connection, "dbo.RepairTicket", "CallTicketId");
                var callJoin = hasCallLink
                    ? "LEFT JOIN dbo.CallTicket linkedCall ON linkedCall.TicketId = t.CallTicketId"
                    : string.Empty;
                var linkSelect = hasCallLink
                    ? "t.CallTicketId AS LinkedCallTicketId, linkedCall.TicketCode AS LinkedCallTicketCode"
                    : "CAST(NULL AS INT) AS LinkedCallTicketId, CAST(NULL AS NVARCHAR(50)) AS LinkedCallTicketCode";

                string recentSql;
                List<SqlParameter> recentParams = new List<SqlParameter>();
                if (isTechnician)
                {
                    recentSql = @"
SELECT TOP (@Limit)
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot,
    t.Problem, t.Priority, t.Status, t.DateReceived, t.CreatedAt, t.UpdatedAt, t.CompletedAt,
    i.ModelNumber, i.Category,
    " + linkSelect + @"
FROM dbo.RepairTicket t
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
" + callJoin + @"
ORDER BY t.UpdatedAt DESC, t.RepairTicketId DESC;";
                    recentParams.Add(new SqlParameter("@Limit", SqlDbType.Int) { Value = recentLimit });
                }
                else
                {
                    recentSql = @"
SELECT TOP (@Limit)
    t.RepairTicketId, t.TicketCode, t.ItemId, t.ItemNameSnapshot, t.ItemSerialSnapshot,
    t.Problem, t.Priority, t.Status, t.DateReceived, t.CreatedAt, t.UpdatedAt, t.CompletedAt,
    i.ModelNumber, i.Category,
    " + linkSelect + @"
FROM dbo.RepairTicket t
LEFT JOIN dbo.Item i ON i.ItemId = t.ItemId
" + callJoin + @"
WHERE (t.SubmittedByUserId = @UserId OR (@EmployeeId IS NOT NULL AND t.RequestedByEmpId = @EmployeeId))
ORDER BY t.UpdatedAt DESC, t.RepairTicketId DESC;";
                    recentParams.Add(new SqlParameter("@Limit", SqlDbType.Int) { Value = recentLimit });
                    recentParams.Add(new SqlParameter("@UserId", SqlDbType.Int) { Value = actor.UserId });
                    recentParams.Add(new SqlParameter("@EmployeeId", SqlDbType.Int) { Value = actor.EmployeeId.HasValue ? (object)actor.EmployeeId.Value : DBNull.Value });
                }

                var recentTickets = new List<object>();
                using (var command = new SqlCommand(recentSql, connection))
                {
                    command.Parameters.AddRange(recentParams.ToArray());
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            recentTickets.Add(new
                            {
                                repairTicketId = Convert.ToInt32(reader["RepairTicketId"]),
                                ticketCode = RepairMobileApiSupport.StringValue(reader, "TicketCode"),
                                itemId = Convert.ToInt32(reader["ItemId"]),
                                itemName = RepairMobileApiSupport.StringValue(reader, "ItemNameSnapshot"),
                                serialNumber = RepairMobileApiSupport.StringValue(reader, "ItemSerialSnapshot"),
                                modelNumber = RepairMobileApiSupport.StringValue(reader, "ModelNumber"),
                                category = RepairMobileApiSupport.StringValue(reader, "Category"),
                                problem = RepairMobileApiSupport.StringValue(reader, "Problem"),
                                priority = RepairMobileApiSupport.StringValue(reader, "Priority"),
                                status = RepairMobileApiSupport.StringValue(reader, "Status"),
                                dateReceived = RepairMobileApiSupport.IsoDate(reader["DateReceived"]),
                                createdAt = RepairMobileApiSupport.IsoUtc(reader["CreatedAt"]),
                                updatedAt = RepairMobileApiSupport.IsoUtc(reader["UpdatedAt"]),
                                completedAt = RepairMobileApiSupport.IsoUtc(reader["CompletedAt"]),
                                linkedCallTicketId = RepairMobileApiSupport.IntValue(reader, "LinkedCallTicketId"),
                                linkedCallTicketCode = RepairMobileApiSupport.StringValue(reader, "LinkedCallTicketCode")
                            });
                        }
                    }
                }

                RepairMobileApiSupport.Ok(context, new
                {
                    success = true,
                    isTechnician = isTechnician,
                    today = today,
                    history = history,
                    summary = summary,
                    mySummary = mySummary,
                    recentTickets = recentTickets,
                    recentCount = recentTickets.Count
                });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.WriteLine("repair-reports error: " + ex);
            RepairMobileApiSupport.Error(context, 503, "Repair reports are temporarily unavailable");
        }
    }

    public bool IsReusable { get { return false; } }
}
