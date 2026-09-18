using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class NewRepairTicketRequest
    {
        public int ItemId { get; set; }

        /// <summary>Optional parent IT Call ticket that initiated this physical-repair workflow.</summary>
        public int? CallTicketId { get; set; }

        public string Problem { get; set; }
        public string Priority { get; set; } = "Medium";
        public int? SubmittedByEmpId { get; set; }

        /// <summary>Manually-editable intake date — when the item actually arrived, distinct from
        /// CreatedAt (when the ticket record was created). Null defers to SYSUTCDATETIME() server-side.</summary>
        public DateTime? DateReceived { get; set; }

        /// <summary>Who is asking for the repair — distinct from SubmittedByEmpId (who physically
        /// brought/logged the item). One of "Department" or "Employee", with the matching Id set.</summary>
        public string RequestedByType { get; set; }
        public int? RequestedByDeptId { get; set; }
        public int? RequestedByEmpId { get; set; }

        /// <summary>Company/Branch context for a Department-mode request — Company is required,
        /// Branch is optional, when RequestedByType = "Department" (a bare Department is ambiguous
        /// across Companies/Branches). Validated against dbo.BranchDepartmentCompany server-side.</summary>
        public int? RequestedByComId { get; set; }
        public int? RequestedByBranchId { get; set; }
    }
}
