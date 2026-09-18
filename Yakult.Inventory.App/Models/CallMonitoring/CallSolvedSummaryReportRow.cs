using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallSolvedSummaryReportRow
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime SolvedAtUtc { get; set; }

        public string CallerName { get; set; }
        public string Company { get; set; }
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
        public string ResponsiblePerson { get; set; }
        public string FinalStatus { get; set; }

        public string Problem { get; set; }
        public string Solution { get; set; }

        public double ResolutionHours { get; set; }
    }
}

