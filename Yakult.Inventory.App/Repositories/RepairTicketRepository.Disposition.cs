using System;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>Unrepairable ticket disposition — Discard (retire the item) or Replace (retire the
    /// item and auto-issue an existing in-stock item to the original requester via a spawned
    /// Request/Set, same auto-creation pattern as RepairTicketRepository.SetLinking.cs's
    /// LinkNewItemToRequestedBySetAsync). Always changeable: re-picking either option — or calling
    /// ClearDispositionAsync ("Unlink") — reverses the prior choice's side effects first via
    /// ItemLifecycleDecisionRepository.ReverseDecisionAsync and, for Replace,
    /// SetRepository.DeleteSetAndRestoreStock + RequestRepository.DeleteRequest. See
    /// Migration_RepairPortal_Conclusion_Disposition.sql for the RepairConclusion columns this
    /// reads/writes, and Migration_RepairPortal_Conclusion_ClearDisposition.sql for the NULL-clear
    /// support on sp_RepairPortal_SetDisposition.</summary>
    public sealed partial class RepairTicketRepository
    {
        private sealed class DispositionTicketInfo
        {
            public int ItemId;
            public string TicketCode;
            public string Status;
            public string RequestedByType;
            public int? RequestedByComId;
            public int? RequestedByBranchId;
            public int? RequestedByDeptId;
            public int? RequestedByEmpId;
        }

        private async Task<DispositionTicketInfo> GetDispositionTicketInfoAsync(int repairTicketId)
        {
            const string sql = @"
SELECT ItemId, TicketCode, Status, RequestedByType, RequestedByComId, RequestedByBranchId, RequestedByDeptId, RequestedByEmpId
FROM dbo.RepairTicket
WHERE RepairTicketId = @RepairTicketId;";

            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand(sql, con))
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                await con.OpenAsync();
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                        return null;

                    return new DispositionTicketInfo
                    {
                        ItemId = reader.GetInt32(0),
                        TicketCode = GetStringOrNull(reader, 1),
                        Status = GetStringOrNull(reader, 2),
                        RequestedByType = GetStringOrNull(reader, 3),
                        RequestedByComId = GetIntOrNull(reader, 4),
                        RequestedByBranchId = GetIntOrNull(reader, 5),
                        RequestedByDeptId = GetIntOrNull(reader, 6),
                        RequestedByEmpId = GetIntOrNull(reader, 7)
                    };
                }
            }
        }

        /// <summary>Reverses an already-executed disposition's side effects — reactivates the
        /// archived item, and for a prior Replace, deletes the spawned Request/Set with stock
        /// restored. Shared by SetDispositionAsync (switching to a different choice) and
        /// ClearDispositionAsync ("Unlink").</summary>
        private async Task ReversePriorDispositionAsync(RepairConclusion current, int fallbackItemId, int decidedByUserId, string decidedByDisplayName)
        {
            var lifecycleRepo = new ItemLifecycleDecisionRepository();
            await lifecycleRepo.ReverseDecisionAsync(current.DispositionItemId ?? fallbackItemId, decidedByUserId, decidedByDisplayName);

            if (string.Equals(current.Disposition, "Replace", StringComparison.OrdinalIgnoreCase))
            {
                var setRepository = new SetRepository();
                if (current.ReplacementSetId.HasValue)
                    await setRepository.DeleteSetAndRestoreStock(current.ReplacementSetId.Value);
                if (current.ReplacementRequestId.HasValue)
                    new RequestRepository().DeleteRequest(current.ReplacementRequestId.Value);
            }
        }

        private async Task CallSetDispositionProcAsync(int repairTicketId, string disposition, int? dispositionItemId, int decidedByUserId, int? replacementItemId, int? replacementRequestId, int? replacementSetId)
        {
            using (var con = new SqlConnection(GetConnectionString()))
            using (var cmd = new SqlCommand("dbo.sp_RepairPortal_SetDisposition", con) { CommandType = System.Data.CommandType.StoredProcedure })
            {
                cmd.Parameters.AddWithValue("@RepairTicketId", repairTicketId);
                cmd.Parameters.AddWithValue("@Disposition", OrDbNull(disposition));
                cmd.Parameters.AddWithValue("@DispositionItemId", OrDbNull(dispositionItemId));
                cmd.Parameters.AddWithValue("@DecidedByUserId", OrDbNull(decidedByUserId));
                cmd.Parameters.AddWithValue("@ReplacementItemId", OrDbNull(replacementItemId));
                cmd.Parameters.AddWithValue("@ReplacementRequestId", OrDbNull(replacementRequestId));
                cmd.Parameters.AddWithValue("@ReplacementSetId", OrDbNull(replacementSetId));
                cmd.Parameters.AddWithValue("@Note", DBNull.Value);

                await con.OpenAsync();
                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>Sets (or changes) the disposition for an Unrepairable ticket. If a prior
        /// disposition was already executed and differs from this call (different choice, or a
        /// different replacementItemId for Replace), its side effects are reversed first so nothing
        /// is left orphaned — the archived item is reactivated, and any spawned Request/Set is
        /// deleted with stock restored.</summary>
        public async Task SetDispositionAsync(int repairTicketId, string disposition, int? replacementItemId, int decidedByUserId, string decidedByDisplayName)
        {
            if (string.IsNullOrWhiteSpace(disposition)) throw new ArgumentException("Disposition is required.", nameof(disposition));
            disposition = disposition.Trim();
            if (!string.Equals(disposition, "Discard", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException($"Invalid disposition '{disposition}'. Must be Discard or Replace.", nameof(disposition));

            if (string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase) && !replacementItemId.HasValue)
                throw new ArgumentException("replacementItemId is required when disposition is Replace.", nameof(replacementItemId));

            var ticket = await GetDispositionTicketInfoAsync(repairTicketId);
            if (ticket == null) throw new InvalidOperationException("Repair ticket not found.");

            var lifecycleRepo = new ItemLifecycleDecisionRepository();
            var current = await GetConclusionAsync(repairTicketId);

            var isSameChoice = current != null
                && string.Equals(current.Disposition, disposition, StringComparison.OrdinalIgnoreCase)
                && current.DispositionExecutedAt.HasValue
                && (!string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase) || current.ReplacementItemId == replacementItemId);

            if (isSameChoice)
                return; // No-op — this exact disposition is already executed.

            // Reverse the prior executed disposition's side effects, if any.
            if (current != null && current.DispositionExecutedAt.HasValue)
                await ReversePriorDispositionAsync(current, ticket.ItemId, decidedByUserId, decidedByDisplayName);

            // Execute the new choice.
            await lifecycleRepo.ExecuteSellOrDisposeAsync(
                ticket.ItemId, "Disposed", decidedByUserId, decidedByDisplayName,
                quantity: 1, recipientName: null, saleAmount: null,
                remarks: $"{disposition} via Repair Ticket {ticket.TicketCode} — Unrepairable");

            int? newRequestId = null;
            int? newSetId = null;

            if (string.Equals(disposition, "Replace", StringComparison.OrdinalIgnoreCase))
            {
                var requestDto = new Pages.RequestDto
                {
                    ItemId = replacementItemId.Value,
                    ComId = ticket.RequestedByComId,
                    BranchId = ticket.RequestedByBranchId,
                    DeptId = ticket.RequestedByDeptId,
                    EmpId = string.Equals(ticket.RequestedByType, "Department", StringComparison.OrdinalIgnoreCase) ? null : ticket.RequestedByEmpId,
                    Quantity = 1,
                    IssuedQty = 1,
                    Status = "Submitted",
                    EntryType = "Negative",
                    WorkflowType = null,
                    RequestSource = "REPAIR_PORTAL",
                    Description = $"Replacement for Repair Ticket {ticket.TicketCode}",
                    CreatedByUserId = decidedByUserId,
                    DateCreated = DateTime.Now
                };

                newRequestId = new RequestRepository().AddRequest(requestDto);

                var setRepository = new SetRepository();
                newSetId = await setRepository.CreateSetAsync(decidedByUserId,
                    remarks: $"Replacement for Repair Ticket {ticket.TicketCode}", status: "Dispatched");

                await setRepository.AddRequestToSetAsync(newRequestId.Value, newSetId.Value);
            }

            await CallSetDispositionProcAsync(repairTicketId, disposition, ticket.ItemId, decidedByUserId, replacementItemId, newRequestId, newSetId);

            // Completed is the true final state — an Unrepairable ticket whose disposition is
            // resolved (Discard/Replace) is done, not stuck in a separate permanent bucket.
            // skipConditionReset:true because the ticket's item is archived/disposed here, not
            // genuinely repaired — flipping its Condition to "Good" would be wrong (see
            // Migration_RepairPortal_SetTicketStatus_SkipConditionResetParam.sql).
            await SetStatusAsync(repairTicketId, "Completed", decidedByUserId,
                $"Auto-completed: {disposition} disposition resolved.", null, skipConditionReset: true);
        }

        /// <summary>"Unlink" — reverses an already-executed disposition (Discard or Replace) and
        /// clears the recorded state back to "no disposition," for correcting a mistake without
        /// forcing an immediate new choice. No-ops if nothing is currently executed.</summary>
        public async Task ClearDispositionAsync(int repairTicketId, int decidedByUserId, string decidedByDisplayName)
        {
            var ticket = await GetDispositionTicketInfoAsync(repairTicketId);
            if (ticket == null) throw new InvalidOperationException("Repair ticket not found.");

            var current = await GetConclusionAsync(repairTicketId);
            if (current == null || !current.DispositionExecutedAt.HasValue)
                return; // Nothing to unlink.

            await ReversePriorDispositionAsync(current, ticket.ItemId, decidedByUserId, decidedByDisplayName);

            await CallSetDispositionProcAsync(repairTicketId, null, null, decidedByUserId, null, null, null);

            // If the ticket was auto-completed by the disposition being unlinked, revert it back
            // to Unrepairable (a diagnosis step, not resolved anymore) — by construction, a ticket
            // only reaches Completed via this path while it has an executed disposition.
            if (string.Equals(ticket.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                await SetStatusAsync(repairTicketId, "Unrepairable", decidedByUserId,
                    "Reverted: disposition unlinked.", null, skipConditionReset: true);
        }
    }
}
