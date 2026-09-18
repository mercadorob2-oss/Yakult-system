using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a user-submitted report/problem ticket.
    /// </summary>
    public class ReportListItem
    {
        public int ReportId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string SubmittedBy { get; set; }
        public DateTime SubmittedDate { get; set; }
        public string Status { get; set; } // "Open", "In Progress", "Resolved", "Closed"
        public string Priority { get; set; } // "Low", "Medium", "High", "Critical"
        public string Category { get; set; } // "Bug", "Complaint", "Feedback", "Feature Request"
    }
}
