using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>Append-only history of Reviewed By/Received By signatures actually entered when a
    /// Repair Report was generated (see Migration_RepairPortal_ReportSignatureHistory.sql) — lets
    /// RepairSignatoryPickerWindow prefill from whoever signed last time instead of starting blank
    /// on every reprint, without ever overwriting what was recorded before.</summary>
    public sealed partial class RepairTicketRepository
    {
        /// <summary>Appends one signature record. Called only for non-blank results — Skip (which
        /// explicitly blanks every field before closing) naturally records nothing.</summary>
        public async Task RecordReportSignatureAsync(int repairTicketId, string roleName, string employeeName, string title, DateTime? signedDate, int? recordedByUserId)
        {
            const string sql = @"
INSERT INTO dbo.RepairReportSignature (RepairTicketId, RoleName, EmployeeName, Title, SignedDate, RecordedByUserId)
VALUES (@RepairTicketId, @RoleName, @EmployeeName, @Title, @SignedDate, @RecordedByUserId);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@RoleName", roleName);
                cmd.Parameters.AddWithValue("@EmployeeName", (object)employeeName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@Title", (object)title ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@SignedDate", (object)signedDate ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@RecordedByUserId", (object)recordedByUserId ?? DBNull.Value);
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>Most recent signature per role for this ticket — one row per RoleName, or none
        /// if this ticket's report has never been generated with a filled-in signature before.</summary>
        public async Task<Dictionary<string, (string name, string title, DateTime? date)>> GetLatestReportSignaturesAsync(int repairTicketId)
        {
            var result = new Dictionary<string, (string name, string title, DateTime? date)>(StringComparer.OrdinalIgnoreCase);

            const string sql = @"
SELECT RoleName, EmployeeName, Title, SignedDate
FROM (
    SELECT RoleName, EmployeeName, Title, SignedDate,
           ROW_NUMBER() OVER (PARTITION BY RoleName ORDER BY RecordedAt DESC) AS rn
    FROM dbo.RepairReportSignature
    WHERE RepairTicketId = @RepairTicketId
) x
WHERE rn = 1;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var roleName = GetStringOrNull(reader, 0);
                        var name = GetStringOrNull(reader, 1);
                        var title = GetStringOrNull(reader, 2);
                        DateTime? date = reader.IsDBNull(3) ? (DateTime?)null : reader.GetDateTime(3);
                        result[roleName] = (name, title, date);
                    }
                }
            }

            return result;
        }
    }
}
