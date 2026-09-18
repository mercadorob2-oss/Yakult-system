using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Full Part detail — loaded lazily when a RepairPartCard is first expanded.</summary>
    public sealed class RepairPartDetail : RepairPart
    {
        public List<RepairPartNoteItem> DiagnosticNotes { get; set; } = new List<RepairPartNoteItem>();
        public List<RepairPartNoteItem> RepairNotes { get; set; } = new List<RepairPartNoteItem>();
        public List<RepairPartAttachmentSummary> Attachments { get; set; } = new List<RepairPartAttachmentSummary>();
        public List<RepairPartHistoryItem> History { get; set; } = new List<RepairPartHistoryItem>();
    }
}
