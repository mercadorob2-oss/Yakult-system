using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTechProfileSummary
    {
        public int EmpId { get; set; }
        public string EmployeeName { get; set; }
        public int? UserId { get; set; }

        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }

        public int AssignedPending { get; set; }
        public int AssignedInProgress { get; set; }
        public int AssignedEscalated { get; set; }
        public int AssignedOpenTotal { get; set; }

        public int HandledAsAssigneeSolvedClosed { get; set; }
        public int HandledAsSolverSolvedClosed { get; set; }
    }
}

