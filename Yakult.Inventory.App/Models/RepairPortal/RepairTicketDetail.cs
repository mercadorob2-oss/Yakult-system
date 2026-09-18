using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairTicketDetail
    {
        public int RepairTicketId { get; set; }
        public string TicketCode { get; set; }
        public int ItemId { get; set; }
        public string ItemNameSnapshot { get; set; }
        public string ItemSerialSnapshot { get; set; }
        public string ModelNumber { get; set; }
        public string Category { get; set; }
        public string SetCode { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DeptName { get; set; }

        /// <summary>Company / Branch / Department joined for display — only the non-blank ones, so
        /// a ticket with partial or no org data doesn't show bare "/" separators with nothing
        /// between them.</summary>
        public string OrgLine
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(CompanyName)) parts.Add(CompanyName);
                if (!string.IsNullOrWhiteSpace(BranchName)) parts.Add(BranchName);
                if (!string.IsNullOrWhiteSpace(DeptName)) parts.Add(DeptName);
                return string.Join(" / ", parts);
            }
        }
        public string Problem { get; set; }
        public string Diagnosis { get; set; }
        public string Resolution { get; set; }
        public string PartsUsed { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string SubmittedByName { get; set; }
        public string AssignedTechName { get; set; }
        public DateTime DateReceived { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        /// <summary>Who is asking for the repair — distinct from SubmittedByName (who physically
        /// brought/logged the item). One of "Department" or "Employee", with the matching Id set.</summary>
        public string RequestedByType { get; set; }
        public int? RequestedByDeptId { get; set; }
        public int? RequestedByEmpId { get; set; }

        /// <summary>Company/Branch context for a Department-mode request — see
        /// NewRepairTicketRequest.RequestedByComId/RequestedByBranchId.</summary>
        public int? RequestedByComId { get; set; }
        public int? RequestedByBranchId { get; set; }
        public string RequestedByComName { get; set; }
        public string RequestedByBranchName { get; set; }
        public string RequestedByDeptName { get; set; }
        public string RequestedByEmpName { get; set; }

        /// <summary>Human-readable display of RequestedByType/Dept/Emp, e.g. "Department: IT
        /// Department", "Employee: Juan Dela Cruz", or "Not set" — computed server-side.</summary>
        public string RequestedByLabel { get; set; }

        /// <summary>Days spent in the CURRENT status only (not a full per-status breakdown) —
        /// computed server-side from the most recent Status history row, frozen at CompletedAt
        /// once the ticket reaches a terminal status.</summary>
        public int DaysInCurrentStatus { get; set; }

        public List<RepairTicketAttachmentSummary> Attachments { get; set; } = new List<RepairTicketAttachmentSummary>();
        public List<RepairTicketNoteItem> Notes { get; set; } = new List<RepairTicketNoteItem>();
        public List<RepairTicketHistoryItem> History { get; set; } = new List<RepairTicketHistoryItem>();

        public List<RepairItemObservation> Observations { get; set; } = new List<RepairItemObservation>();
        public List<RepairPart> Parts { get; set; } = new List<RepairPart>();
        public RepairConclusion Conclusion { get; set; }
    }
}
