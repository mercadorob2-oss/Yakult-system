using System;
using System.Data;
using System.Data.SqlClient;
using Yakult.Inventory.App.Core;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>
    /// Data access for the Repair Technician Portal (dbo.RepairTicket / RepairTicketHistory /
    /// RepairTicketNote / RepairTicketAttachment / RepairTechnicianAttendance).
    /// Reads are plain parameterized SQL; writes go through the sp_RepairPortal_* stored procs
    /// (see Migration_RepairPortal_StoredProcs.sql).
    /// </summary>
    public sealed partial class RepairTicketRepository : IRepairTicketRepository
    {
        /// <summary>
        /// Constructor does NOT throw if the connection string is missing.
        /// Validation is deferred to method execution to prevent page crashes.
        /// </summary>
        public RepairTicketRepository()
        {
        }

        private static string GetConnectionString()
        {
            DatabaseConfig.EnsureConfigured();
            return DatabaseConfig.ConnectionString;
        }

        private static object OrDbNull(object value) => value ?? DBNull.Value;

        private static string GetStringOrNull(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

        private static int? GetIntOrNull(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? (int?)null : reader.GetInt32(ordinal);

        private static long? GetLongOrNull(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? (long?)null : reader.GetInt64(ordinal);

        // All CreatedAt/UpdatedAt/ChangedAt/UploadedAt/DateReceived/CompletedAt/TimeIn/TimeOut
        // columns in this schema are stored via SYSUTCDATETIME() (UTC). WPF's XAML bindings
        // display whatever DateTime value they're given with no timezone awareness, so without
        // converting here, timestamps appeared 8 hours behind actual Philippines/Manila time.
        // "Singapore Standard Time" is the Windows TimeZoneInfo ID this codebase already uses
        // elsewhere (e.g. Item.sql, CallTicket.sql) for the same UTC+8 offset — reused here for
        // consistency rather than introducing a second timezone identifier.
        private static readonly Lazy<TimeZoneInfo> ManilaTimeZone = new Lazy<TimeZoneInfo>(
            () => TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time"));

        private static DateTime ToManilaLocal(DateTime utcValue)
        {
            var utc = utcValue.Kind == DateTimeKind.Utc ? utcValue : DateTime.SpecifyKind(utcValue, DateTimeKind.Utc);
            return TimeZoneInfo.ConvertTimeFromUtc(utc, ManilaTimeZone.Value);
        }

        /// <summary>Reads a non-nullable UTC timestamp column and converts it to Manila local
        /// time for display. Do not use this for pure calendar-date columns (e.g. WorkDate) that
        /// were already computed server-side in local time — only for genuine UTC instants.</summary>
        private static DateTime GetLocalDateTime(SqlDataReader reader, int ordinal)
            => ToManilaLocal(reader.GetDateTime(ordinal));

        private static DateTime? GetDateOrNull(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? (DateTime?)null : ToManilaLocal(reader.GetDateTime(ordinal));

        private static byte[] GetBytesOrNull(SqlDataReader reader, int ordinal)
            => reader.IsDBNull(ordinal) ? null : (byte[])reader[ordinal];
    }
}
