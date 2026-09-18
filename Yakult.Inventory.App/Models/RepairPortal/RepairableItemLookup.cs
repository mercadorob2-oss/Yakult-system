namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Row shown in the New Repair Ticket item picker (search-as-you-type).</summary>
    public sealed class RepairableItemLookup
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string SerialNumber { get; set; }
        public string ModelNumber { get; set; }
        public string SetCode { get; set; }
        public string CompanyName { get; set; }
        public string BranchName { get; set; }
        public string DeptName { get; set; }

        /// <summary>Who the item's current Set is dispatched to — Company/Branch/Department come
        /// from the Set itself (ComId/CurrentBranchId/CurrentDepartmentId); EmpId comes from the
        /// linked Request row for this item within that Set (dbo.Request.EmpId, NULL for a
        /// department-level dispatch). Used to prefill the New Repair Ticket dialog's Requested By
        /// section when the picked item belongs to a Set — null on items with no Set (e.g. one just
        /// created via "+ Add Item").</summary>
        public int? RequestedByComId { get; set; }
        public int? RequestedByBranchId { get; set; }
        public int? RequestedByDeptId { get; set; }
        public int? RequestedByEmpId { get; set; }

        /// <summary>Display name of who the item's Set is dispatched to — the Employee if
        /// RequestedByEmpId is set, otherwise blank (department-level dispatches show via DeptName
        /// instead). Only populated by SearchRepairableItemsAsync/GetCandidateRepairItemsAsync.</summary>
        public string RequestedByEmpName { get; set; }

        /// <summary>Only populated by SearchReplacementCandidatesAsync (the "Replace" disposition
        /// picker's DataGrid columns) — other callers of this shared lookup model leave these null.</summary>
        public System.DateTime? DateCreated { get; set; }
        public string CreatedByName { get; set; }
    }
}
