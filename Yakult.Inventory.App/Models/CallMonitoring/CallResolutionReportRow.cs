using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallResolutionReportRow
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public string TicketStatus { get; set; }
        public DateTime? TicketCreatedAt { get; set; }
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
        public string ResponsiblePerson { get; set; }
        public string Priority { get; set; }

        public DateTime? ResolutionMarkedAt { get; set; }
        public string ResolutionType { get; set; }
        public string MarkedByName { get; set; }

        public int? ReplacementOldItemId { get; set; }
        public string ReplacementOldItem { get; set; }
        public int? ReplacementNewItemId { get; set; }
        public string ReplacementNewItem { get; set; }
        public int? ReplacementQty { get; set; }
        public string Remarks { get; set; }
    }
}
