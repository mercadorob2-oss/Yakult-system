using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallFieldVisitItem
    {
        public int FieldVisitId { get; set; }
        public int TicketId { get; set; }
        public string Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Notes { get; set; }
        public int? TechnicianEmpId { get; set; }
        public string TechnicianName { get; set; }
        public bool HasSignature { get; set; }
        public string Department { get; set; }
        public string Branch { get; set; }
        public string Location
        {
            get
            {
                var d = (Department ?? string.Empty).Trim();
                var b = (Branch ?? string.Empty).Trim();
                if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(b)) return d + " (" + b + ")";
                if (!string.IsNullOrWhiteSpace(d)) return d;
                if (!string.IsNullOrWhiteSpace(b)) return b;
                return string.Empty;
            }
        }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public string ScheduledDateDisplay
        {
            get
            {
                if (!ScheduledAt.HasValue) return "—";
                return ScheduledAt.Value.ToString("yyyy-MM-dd HH:mm");
            }
        }

        public string CompletedDateDisplay
        {
            get
            {
                if (!CompletedAt.HasValue) return "—";
                return CompletedAt.Value.ToString("yyyy-MM-dd HH:mm");
            }
        }
    }
}
