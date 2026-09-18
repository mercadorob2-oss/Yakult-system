using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallSlaComplianceReportRow
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public string Department { get; set; }
        public int? BranchId { get; set; }
        public string Branch { get; set; }

        public string Location
        {
            get
            {
                var dept = (Department ?? string.Empty).Trim();
                var branch = (Branch ?? string.Empty).Trim();

                if (!string.IsNullOrWhiteSpace(dept) && !string.IsNullOrWhiteSpace(branch))
                    return $"{dept} ({branch})";

                if (!string.IsNullOrWhiteSpace(dept))
                    return dept;

                if (!string.IsNullOrWhiteSpace(branch))
                    return branch;

                return string.Empty;
            }
        }
        public string Priority { get; set; }
        public string AssignedToName { get; set; }
        public string CompletedByName { get; set; }
        public string CompletedStatus { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime CompletedAtUtc { get; set; }
    }
}
