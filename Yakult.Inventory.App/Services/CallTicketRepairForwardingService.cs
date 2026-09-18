using System;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Models.CallMonitoring;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Services
{
    /// <summary>
    /// Creates or retrieves the single Repair Portal workflow linked to an IT Call ticket.
    /// The source IT Call remains the support case; the Repair Ticket owns the physical work.
    /// </summary>
    public sealed class CallTicketRepairForwardingService
    {
        private readonly ICallMonitoringRepository _callRepository;
        private readonly IRepairTicketRepository _repairRepository;

        public CallTicketRepairForwardingService(
            ICallMonitoringRepository callRepository,
            IRepairTicketRepository repairRepository = null)
        {
            _callRepository = callRepository ?? throw new ArgumentNullException(nameof(callRepository));
            _repairRepository = repairRepository ?? new RepairTicketRepository();
        }

        /// <summary>
        /// Builds the Repair Portal intake data from an IT Call without creating a ticket. The
        /// existing Repair intake dialog uses this to show the operator all prefilled fields for
        /// review; persistence happens only when they explicitly select Create Ticket.
        /// </summary>
        public async Task<NewRepairTicketRequest> CreatePrefilledRequestAsync(
            int callTicketId,
            int repairItemId,
            string resolutionType,
            string resolutionRemarks,
            int? submittedByEmployeeId)
        {
            if (callTicketId <= 0)
                throw new ArgumentOutOfRangeException(nameof(callTicketId));
            if (repairItemId <= 0)
                throw new ArgumentOutOfRangeException(nameof(repairItemId));

            var callTicket = await _callRepository.GetTicketNotificationDataAsync(callTicketId);
            if (callTicket == null)
                throw new InvalidOperationException("The IT Call ticket could not be found for repair forwarding.");

            // Legacy CallTicket rows retain the caller's display name but not their EmpId. Resolve
            // it only when that name maps to exactly one active employee; this avoids assigning a
            // repair request to the wrong person when employees share a name.
            var callerOrganization = await ResolveUniqueCallerOrganizationAsync(callTicket.CallerName);
            var hasCallerOrganization = callerOrganization != null
                && callerOrganization.ComId.HasValue
                && callerOrganization.DeptId.HasValue;
            var requestedByComId = hasCallerOrganization ? callerOrganization.ComId : callTicket.ComId;
            var requestedByDeptId = hasCallerOrganization ? callerOrganization.DeptId : callTicket.DeptId;
            var requestedByBranchId = hasCallerOrganization ? callerOrganization.BranchId : callTicket.BranchId;
            var hasRequestedByOrganization = requestedByComId.HasValue && requestedByDeptId.HasValue;
            var canRequestForCaller = hasCallerOrganization && callerOrganization.EmpId > 0;

            return new NewRepairTicketRequest
            {
                CallTicketId = callTicket.TicketId,
                ItemId = repairItemId,
                Problem = BuildRepairProblem(callTicket, resolutionType, resolutionRemarks),
                Priority = NormalizePriority(callTicket.Priority),
                SubmittedByEmpId = submittedByEmployeeId,
                DateReceived = null,
                RequestedByType = canRequestForCaller ? "Employee" : (hasRequestedByOrganization ? "Department" : null),
                RequestedByComId = requestedByComId,
                RequestedByBranchId = requestedByBranchId,
                RequestedByDeptId = hasRequestedByOrganization ? requestedByDeptId : null,
                RequestedByEmpId = canRequestForCaller ? (int?)callerOrganization.EmpId : null
            };
        }

        /// <summary>Returns the caller's organization only for an exact, unambiguous active
        /// employee-name match. Historic IT Calls do not persist CallerEmpId, so a partial match
        /// or duplicate name deliberately returns null and lets the ticket-level org be used.</summary>
        private async Task<CallEmployeeOrgInfo> ResolveUniqueCallerOrganizationAsync(string callerName)
        {
            var normalizedName = (callerName ?? string.Empty).Trim();
            if (normalizedName.Length < 2)
                return null;

            try
            {
                var matches = await _callRepository.SearchEmployeesByNameAsync(normalizedName);
                if (matches == null)
                    return null;

                var exactMatches = matches
                    .Where(employee => employee != null
                        && employee.Id > 0
                        && string.Equals((employee.Name ?? string.Empty).Trim(), normalizedName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                if (exactMatches.Count != 1)
                    return null;

                return await _callRepository.GetEmployeeOrgInfoByEmpIdAsync(exactMatches[0].Id);
            }
            catch
            {
                // A failed optional prefill must not block forwarding; ticket-level org remains
                // available and the Repair operator can still select Requested By manually.
                return null;
            }
        }

        /// <summary>
        /// Retained for non-interactive callers. The WPF Mark-as workflow now uses
        /// CreatePrefilledRequestAsync and waits for the operator to confirm in the Repair Portal.
        /// </summary>
        public async Task<RepairTicketListItem> ForwardAsync(
            int callTicketId,
            int repairItemId,
            string resolutionType,
            string resolutionRemarks,
            int? submittedByEmployeeId,
            int? createdByUserId)
        {
            var request = await CreatePrefilledRequestAsync(
                callTicketId,
                repairItemId,
                resolutionType,
                resolutionRemarks,
                submittedByEmployeeId);
            return await _repairRepository.CreateTicketFromCallTicketAsync(request, createdByUserId);
        }

        private static string BuildRepairProblem(
            CallTicketNotificationData callTicket,
            string resolutionType,
            string resolutionRemarks)
        {
            var originalIssue = (callTicket.Issue ?? string.Empty).Trim();
            var sourceCode = string.IsNullOrWhiteSpace(callTicket.TicketCode)
                ? "IT Call #" + callTicket.TicketId
                : "IT Call " + callTicket.TicketCode.Trim();
            var decision = string.IsNullOrWhiteSpace(resolutionType)
                ? "Forwarded to Repair Portal"
                : "Forwarded after " + resolutionType.Trim();

            var text = sourceCode + ": " + originalIssue + Environment.NewLine + Environment.NewLine + decision + ".";
            if (!string.IsNullOrWhiteSpace(resolutionRemarks))
                text += Environment.NewLine + "ITCM remarks: " + resolutionRemarks.Trim();

            return text.Length <= 2000 ? text : text.Substring(0, 2000);
        }

        private static string NormalizePriority(string priority)
        {
            switch ((priority ?? string.Empty).Trim().ToUpperInvariant())
            {
                case "LOW": return "Low";
                case "HIGH": return "High";
                case "CRITICAL": return "Critical";
                default: return "Medium";
            }
        }
    }
}
