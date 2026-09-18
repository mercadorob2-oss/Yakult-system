using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairTicketFilterCriteria
    {
        public string SearchText { get; set; }
        public string Status { get; set; }
        public int? ComId { get; set; }
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }
        public string Priority { get; set; }
        public int? AssignedTechEmpId { get; set; }

        /// <summary>Multi-select — matches if the ticket's item Category is any of these. Null/empty
        /// means no restriction.</summary>
        public List<string> Categories { get; set; }

        /// <summary>One of: "Waiting","Repairing","Completed","NeedsParts","HighPriority" — applied in addition to Status.</summary>
        public string QuickFilterChip { get; set; }

        /// <summary>Excludes Status = 'Completed' — since resolved-Unrepairable tickets also become
        /// Completed (see RepairTicketRepository.Disposition.cs's auto-complete), this alone is
        /// enough to declutter both genuine repairs and resolved dispositions together.</summary>
        public bool HideCompleted { get; set; }

        /// <summary>One of: "DateReceivedDesc" (default), "DateReceivedAsc", "PriorityDesc", "StatusAsc".</summary>
        public string SortKey { get; set; } = "DateReceivedDesc";
    }
}
