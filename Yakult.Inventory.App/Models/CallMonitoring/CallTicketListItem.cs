using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketListItem
    {
        public int TicketId { get; set; }
        public string TicketSource { get; set; }
        public string TicketCode { get; set; }
        public int? ComId { get; set; }
        public string Company { get; set; }
        public string Issue { get; set; }
        public string Department { get; set; }
        public int? BranchId { get; set; }
        public string Branch { get; set; }
        public string ResponsiblePerson { get; set; }
        public int? AssignedToEmpId { get; set; }
        public int TicketAgeDays { get; set; }
        public int IdleDays { get; set; }
        public System.DateTime? LastContactAt { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public string CallerName { get; set; }
        public string IssueType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SolvedAt { get; set; }

        /// <summary>Repair Portal ticket permanently linked to this IT Call, loaded separately
        /// from the IT Call status so forwarding remains visible after the parent is resolved.</summary>
        public string LinkedRepairTicketCode { get; set; }

        public int TotalCount { get; set; }
    }
}

