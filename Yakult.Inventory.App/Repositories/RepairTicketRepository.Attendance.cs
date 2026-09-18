using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    public sealed partial class RepairTicketRepository
    {
        public async Task<RepairTechnicianAttendanceStatus> GetTodayStatusAsync(int employeeId)
        {
            const string sql = @"
SELECT TOP (1) AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
FROM dbo.RepairTechnicianAttendance
WHERE EmployeeId = @EmployeeId
  AND WorkDate = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time')
ORDER BY AttendanceId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return MapAttendance(reader);
                }
            }

            return new RepairTechnicianAttendanceStatus { EmployeeId = employeeId };
        }

        /// <summary>Full Time In/Out history for a technician, most recent first — one row per
        /// session (dbo.RepairTechnicianAttendance stores multiple rows per day per employee since
        /// Migration_RepairPortal_Attendance_AllowMultipleSessionsPerDay.sql, not one row per day).</summary>
        public async Task<System.Collections.Generic.List<RepairTechnicianAttendanceStatus>> GetAttendanceHistoryAsync(int employeeId)
        {
            const string sql = @"
SELECT AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
FROM dbo.RepairTechnicianAttendance
WHERE EmployeeId = @EmployeeId
ORDER BY WorkDate DESC, AttendanceId DESC;";

            var list = new System.Collections.Generic.List<RepairTechnicianAttendanceStatus>();
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                        list.Add(MapAttendance(reader));
                }
            }

            return list;
        }

        private static RepairTechnicianAttendanceStatus MapAttendance(SqlDataReader reader)
        {
            return new RepairTechnicianAttendanceStatus
            {
                AttendanceId = GetIntOrNull(reader, 0),
                EmployeeId = reader.GetInt32(1),
                WorkDate = reader.GetDateTime(2),
                TimeIn = GetDateOrNull(reader, 3),
                TimeOut = GetDateOrNull(reader, 4)
            };
        }

        /// <summary>Most recent TimeOut on record for this technician across ALL days (not just
        /// today) — used to show "last time you clocked out" even before the technician has timed
        /// in yet today, since GetTodayStatusAsync only ever looks at today's row.</summary>
        public async Task<DateTime?> GetLastTimeOutAsync(int employeeId)
        {
            const string sql = @"
SELECT TOP (1) TimeOut
FROM dbo.RepairTechnicianAttendance
WHERE EmployeeId = @EmployeeId AND TimeOut IS NOT NULL
ORDER BY WorkDate DESC, AttendanceId DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return GetDateOrNull(reader, 0);
                }
            }

            return null;
        }

        public async Task<RepairTechnicianAttendanceStatus> TimeInAsync(int employeeId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_TimeIn", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return MapAttendance(reader);
                }
            }

            return await GetTodayStatusAsync(employeeId);
        }

        public async Task<RepairTechnicianAttendanceStatus> TimeOutAsync(int employeeId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_TimeOut", con) { CommandType = CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@EmployeeId", employeeId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                        return MapAttendance(reader);
                }
            }

            return await GetTodayStatusAsync(employeeId);
        }
    }
}
