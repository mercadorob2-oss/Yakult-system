using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Summary row for a Part's collapsed card — includes evidence/note counts computed
    /// in one query (no N+1) so the card can show "3 Images · 1 Video · 4 Notes" without a
    /// separate round-trip.</summary>
    public class RepairPart
    {
        public int RepairPartId { get; set; }
        public int RepairTicketId { get; set; }
        public int PartNumber { get; set; }
        public string CustomLabel { get; set; }
        public string PartDisplayName { get; set; }
        public string ProblemDescription { get; set; }
        public string Status { get; set; }
        public string Severity { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public int ImageCount { get; set; }
        public int VideoCount { get; set; }
        public int DocumentCount { get; set; }
        public int NoteCount { get; set; }

        /// <summary>Days spent in the CURRENT status only — see RepairTicketDetail.DaysInCurrentStatus
        /// (Part has no CompletedAt, so this freezes at UpdatedAt once Repaired/CannotRepair).</summary>
        public int DaysInCurrentStatus { get; set; }
    }
}
