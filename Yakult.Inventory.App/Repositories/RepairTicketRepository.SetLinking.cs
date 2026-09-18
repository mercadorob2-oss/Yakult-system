using System;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Repositories
{
    /// <summary>Links a repair ticket's item to a real dbo.Request/dbo.Set/dbo.SetItem record when
    /// the item didn't already belong to a Set at intake, so "who has this item" stays discoverable
    /// through the same Request/Set chain every other module reads — instead of the Requested By
    /// fields only ever existing as flat columns on RepairTicket. See
    /// NewRepairTicketViewModel.SaveAsync for the call site (gated on IsRequestedByEditable, i.e.
    /// only for items with no existing Set).</summary>
    public sealed partial class RepairTicketRepository
    {
        public async Task LinkNewItemToRequestedBySetAsync(NewRepairTicketRequest request, string ticketCode, int createdByUserId)
        {
            var requestDto = new Pages.RequestDto
            {
                ItemId = request.ItemId,
                ComId = request.RequestedByComId,
                BranchId = request.RequestedByBranchId,
                DeptId = request.RequestedByDeptId,
                EmpId = request.RequestedByType == "Department" ? null : request.RequestedByEmpId,
                Quantity = 1,
                IssuedQty = 1,
                // "Submitted" mirrors the existing Request Portal convention (a Request becomes
                // Submitted once it's grouped into a Set) — accurate here since both happen together.
                Status = "Submitted",
                EntryType = "Negative",
                // Deliberately left NULL: only the Cartridge Management / Request-Set-Management
                // pending queues filter on WorkflowType, so leaving it unset keeps this Request out
                // of both without needing a new queue-exclusion rule.
                WorkflowType = null,
                RequestSource = "REPAIR_PORTAL",
                Description = $"Auto-created from Repair Ticket {ticketCode} intake",
                CreatedByUserId = createdByUserId,
                DateCreated = DateTime.Now
                // SetId intentionally left unset (null) — RequestRepository.AddRequest only posts an
                // Inventory ledger row when SetId is already populated at insert time, so leaving it
                // null here keeps this Request creation inventory read-only, same as every other
                // Request in this app.
            };

            var reqId = new RequestRepository().AddRequest(requestDto);

            var setRepository = new SetRepository();
            // "Dispatched" (not the CreateSetAsync default "Pending") — the item is by definition
            // already physically with whoever this ticket names as Requested By, so there's no
            // separate dispatch step to wait for.
            var setId = await setRepository.CreateSetAsync(createdByUserId,
                remarks: $"Auto-created from Repair Ticket {ticketCode} intake", status: "Dispatched");

            await setRepository.AddRequestToSetAsync(reqId, setId);
        }
    }
}
