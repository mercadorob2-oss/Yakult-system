using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.BorrowItems;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>Spare-item loaners for tickets in "Repairing" status — reuses the existing Borrow
    /// Items feature (dbo.BorrowLog, BorrowItemsRepository) rather than the Request/Set-spawning
    /// pattern used by the Unrepairable Discard/Replace disposition, since this is a genuine
    /// temporary, returnable loan (BorrowedAtUtc/ReturnedAtUtc), which is exactly what BorrowLog
    /// already models. dbo.BorrowLog.RepairTicketId (see
    /// Migration_RepairPortal_BorrowLog_RepairTicketLink.sql) ties a loan back to the ticket that
    /// spawned it, so it can be found/auto-returned later.</summary>
    public sealed partial class RepairTicketRepository
    {
        private sealed class SpareTicketInfo
        {
            public string TicketCode;
            public string RequestedByType;
            public int? RequestedByEmpId;
            public int? RequestedByDeptId;
            public string RequestedByDeptName;
        }

        private async Task<SpareTicketInfo> GetSpareTicketInfoAsync(int repairTicketId)
        {
            const string sql = @"
SELECT t.TicketCode, t.RequestedByType, t.RequestedByEmpId, t.RequestedByDeptId, d.Name AS RequestedByDeptName
FROM dbo.RepairTicket t
LEFT JOIN dbo.Department d ON d.DeptId = t.RequestedByDeptId
WHERE t.RepairTicketId = @RepairTicketId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new SpareTicketInfo
                    {
                        TicketCode = GetStringOrNull(reader, 0),
                        RequestedByType = GetStringOrNull(reader, 1),
                        RequestedByEmpId = GetIntOrNull(reader, 2),
                        RequestedByDeptId = GetIntOrNull(reader, 3),
                        RequestedByDeptName = GetStringOrNull(reader, 4)
                    };
                }
            }
        }

        private const string BorrowLogSelectColumns = @"
    b.BorrowId, b.ItemId, b.SerialNumber, b.ItemName, b.ItemDescription, b.ModelNumber,
    b.BorrowedByEmpId, b.BorrowedByEmpName, b.BorrowedByDeptId, b.BorrowedByDeptName,
    b.BorrowEncodedByUserId, b.BorrowEncodedByUserName, b.BorrowedAtUtc,
    b.ReturnedByEmpId, b.ReturnedByEmpName, b.ReturnedByDeptId, b.ReturnedByDeptName,
    b.ReturnEncodedByUserId, b.ReturnEncodedByUserName, b.ReturnedAtUtc, b.RepairTicketId";

        private static BorrowLogRow MapBorrowLogRow(SqlDataReader reader)
        {
            return new BorrowLogRow
            {
                BorrowId = reader.GetInt32(0),
                ItemId = reader.GetInt32(1),
                SerialNumber = GetStringOrNull(reader, 2),
                ItemName = GetStringOrNull(reader, 3),
                ItemDescription = GetStringOrNull(reader, 4),
                ModelNumber = GetStringOrNull(reader, 5),
                BorrowedByEmpId = GetIntOrNull(reader, 6),
                BorrowedByEmpName = GetStringOrNull(reader, 7),
                BorrowedByDeptId = GetIntOrNull(reader, 8),
                BorrowedByDeptName = GetStringOrNull(reader, 9),
                BorrowEncodedByUserId = reader.GetInt32(10),
                BorrowEncodedByUserName = GetStringOrNull(reader, 11),
                BorrowedAtUtc = reader.GetDateTime(12),
                ReturnedByEmpId = GetIntOrNull(reader, 13),
                ReturnedByEmpName = GetStringOrNull(reader, 14),
                ReturnedByDeptId = GetIntOrNull(reader, 15),
                ReturnedByDeptName = GetStringOrNull(reader, 16),
                ReturnEncodedByUserId = GetIntOrNull(reader, 17),
                ReturnEncodedByUserName = GetStringOrNull(reader, 18),
                ReturnedAtUtc = GetDateOrNull(reader, 19),
                RepairTicketId = GetIntOrNull(reader, 20)
            };
        }

        /// <summary>The currently open (not yet returned) spare loan for this ticket, if any.</summary>
        public async Task<BorrowLogRow> GetActiveSpareAsync(int repairTicketId)
        {
            var sql = "SELECT" + BorrowLogSelectColumns + @"
FROM dbo.BorrowLog b
WHERE b.RepairTicketId = @RepairTicketId AND b.ReturnedAtUtc IS NULL
ORDER BY b.BorrowedAtUtc DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    return await reader.ReadAsync() ? MapBorrowLogRow(reader) : null;
                }
            }
        }

        /// <summary>The most recent spare loan for this ticket regardless of return status — used by
        /// the printed report, since the ticket may already be Completed with the spare returned.</summary>
        public async Task<BorrowLogRow> GetMostRecentSpareAsync(int repairTicketId)
        {
            var sql = "SELECT TOP (1)" + BorrowLogSelectColumns + @"
FROM dbo.BorrowLog b
WHERE b.RepairTicketId = @RepairTicketId
ORDER BY b.BorrowedAtUtc DESC;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    return await reader.ReadAsync() ? MapBorrowLogRow(reader) : null;
                }
            }
        }

        /// <summary>Assigns a spare item to this ticket's requester while it's being repaired, via
        /// BorrowItemsRepository.BorrowAsync. Throws a friendly error if the ticket has no
        /// resolvable requester set yet (Requested By must be filled in first).</summary>
        public async Task AssignSpareAsync(int repairTicketId, string serialNumber, int decidedByUserId)
        {
            var ticket = await GetSpareTicketInfoAsync(repairTicketId);
            if (ticket == null) throw new InvalidOperationException("Repair ticket not found.");

            var isDepartment = string.Equals(ticket.RequestedByType, "Department", StringComparison.OrdinalIgnoreCase);
            var empId = isDepartment ? null : ticket.RequestedByEmpId;
            var deptId = isDepartment ? ticket.RequestedByDeptId : null;
            var deptName = isDepartment ? ticket.RequestedByDeptName : null;

            if (!empId.HasValue && (!deptId.HasValue || string.IsNullOrWhiteSpace(deptName)))
                throw new InvalidOperationException("This ticket has no Requested By set yet — set it before assigning a spare.");

            var borrowRepo = new BorrowItemsRepository();
            await borrowRepo.BorrowAsync(serialNumber, empId, decidedByUserId, null, deptId, deptName, repairTicketId);

            await AddSpareHistoryAsync(repairTicketId, decidedByUserId, "SpareAssigned", $"Spare assigned via Repair Ticket {ticket.TicketCode}");
        }

        /// <summary>"Unlink Spare" — returns the ticket's currently active spare loan. No-ops if
        /// there is none.</summary>
        public async Task UnlinkSpareAsync(int repairTicketId, int decidedByUserId)
        {
            var active = await GetActiveSpareAsync(repairTicketId);
            if (active == null) return;

            await ReturnActiveSpareAsync(repairTicketId, active, decidedByUserId, "SpareReturned");
        }

        /// <summary>Silently returns the ticket's active spare loan, if any — called automatically
        /// when the ticket is marked Completed. Unlike UnlinkSpareAsync this never throws for the
        /// "nothing to return" case, since most tickets won't have a spare at all.</summary>
        public async Task AutoReturnSpareIfAnyAsync(int repairTicketId, int decidedByUserId)
        {
            try
            {
                var active = await GetActiveSpareAsync(repairTicketId);
                if (active == null) return;

                await ReturnActiveSpareAsync(repairTicketId, active, decidedByUserId, "SpareAutoReturned");
            }
            catch
            {
                // Non-critical — the ticket still completes even if the auto-return fails; the
                // technician can still Unlink Spare manually afterward.
            }
        }

        private async Task ReturnActiveSpareAsync(int repairTicketId, BorrowLogRow active, int decidedByUserId, string historyFieldName)
        {
            var ticket = await GetSpareTicketInfoAsync(repairTicketId);

            var isDepartment = active.BorrowedByEmpId == null;
            var empId = isDepartment ? null : active.BorrowedByEmpId;
            var deptId = isDepartment ? active.BorrowedByDeptId : null;
            var deptName = isDepartment ? active.BorrowedByDeptName : null;

            var borrowRepo = new BorrowItemsRepository();
            await borrowRepo.ReturnAsync(active.BorrowId, empId, decidedByUserId, deptId, deptName);

            await AddSpareHistoryAsync(repairTicketId, decidedByUserId, historyFieldName, $"Spare returned via Repair Ticket {ticket?.TicketCode}");
        }

        private async Task AddSpareHistoryAsync(int repairTicketId, int decidedByUserId, string fieldName, string note)
        {
            const string sql = @"
INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
VALUES (@RepairTicketId, @ChangedByUserId, @FieldName, NULL, NULL, @Note);";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@ChangedByUserId", OrDbNull(decidedByUserId));
                cmd.Parameters.AddWithValue("@FieldName", fieldName);
                cmd.Parameters.AddWithValue("@Note", OrDbNull(note));
                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
