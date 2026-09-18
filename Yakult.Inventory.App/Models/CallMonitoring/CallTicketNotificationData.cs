using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketNotificationData
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public int? ComId { get; set; }
        public string Company { get; set; }
        public int? DeptId { get; set; }
        public string Department { get; set; }
        public int? BranchId { get; set; }
        public string Branch { get; set; }
        public string CallerName { get; set; }
        public string Issue { get; set; }
        public string ProvidedSolution { get; set; }
        public string IssueType { get; set; }
        public string Priority { get; set; }
        public string Status { get; set; }
        public string ContactEmail { get; set; }
        public int? AssignedToEmpId { get; set; }
        public string AssignedTo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastContactAt { get; set; }
        public DateTime? LastReminderSentAt { get; set; }
        public DateTime? SolvedAt { get; set; }
    }
}
