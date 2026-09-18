using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallFieldVisitReportRow
    {
        public int FieldVisitId { get; set; }
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public string Issue { get; set; }
        public string Department { get; set; }
        public string Branch { get; set; }
        public string Location
        {
            get
            {
                var d = (Department ?? "").Trim();
                var b = (Branch ?? "").Trim();
                if (!string.IsNullOrWhiteSpace(d) && !string.IsNullOrWhiteSpace(b)) return $"{d} ({b})";
                if (!string.IsNullOrWhiteSpace(d)) return d;
                if (!string.IsNullOrWhiteSpace(b)) return b;
                return "";
            }
        }
        public int? TechnicianEmpId { get; set; }
        public string TechnicianName { get; set; }
        public string Status { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string ScheduledDateDisplay => ScheduledAt.HasValue ? ScheduledAt.Value.ToString("yyyy-MM-dd") : "—";
        public string CompletedDateDisplay => CompletedAt.HasValue ? CompletedAt.Value.ToString("yyyy-MM-dd") : "—";
        public bool HasSignature { get; set; }
        public int AttachmentCount { get; set; }
        public string Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
