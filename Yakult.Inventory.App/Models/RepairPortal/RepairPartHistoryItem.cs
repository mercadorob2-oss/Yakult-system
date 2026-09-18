using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Same property names as RepairTicketHistoryItem (FieldName/OldValue/NewValue/
    /// Note/ChangedByName/ChangedAt) so the generic RepairTimelineControl can be reused
    /// unmodified for a Part's mini-timeline.</summary>
    public sealed class RepairPartHistoryItem
    {
        public long PartHistoryId { get; set; }
        public int RepairPartId { get; set; }
        public int RepairTicketId { get; set; }
        public DateTime ChangedAt { get; set; }
        public int? ChangedByUserId { get; set; }
        public string ChangedByName { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Note { get; set; }
    }
}
