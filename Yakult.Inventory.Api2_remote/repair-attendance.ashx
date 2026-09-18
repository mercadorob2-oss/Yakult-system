<%@ WebHandler Language="C#" Class="RepairAttendanceHandler" %>
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Web;

/// <summary>Technician's own Repair Portal attendance state and recent sessions.  Time-in/out
/// writes remain in repair-ticket-action.ashx and derive the employee from the JWT actor.</summary>
public sealed class RepairAttendanceHandler : IHttpHandler
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
        if (!RepairMobileApiSupport.TryRequireTechnician(context, out actor)) return;
        if (!actor.EmployeeId.HasValue)
        {
            RepairMobileApiSupport.Error(context, 403, "An IT employee account is required for repair attendance");
            return;
        }

        try
        {
            using (var connection = new SqlConnection(RepairMobileApiSupport.ConnectionString))
            {
                connection.Open();
                if (!RepairMobileApiSupport.HasTable(connection, "dbo.RepairTechnicianAttendance"))
                {
                    RepairMobileApiSupport.Error(context, 503, "Repair attendance is not installed for this environment");
                    return;
                }
                object today = null;
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

                var history = new List<object>();
                using (var command = new SqlCommand(@"
SELECT TOP (60) AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
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
                RepairMobileApiSupport.Ok(context, new { success = true, today = today, history = history });
            }
        }
        catch
        {
            RepairMobileApiSupport.Error(context, 503, "Repair attendance is temporarily unavailable");
        }
    }

    public bool IsReusable { get { return false; } }
}
