using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>A single reported-problem observation, entered during equipment intake. Mutable
    /// (unlike the append-only Notes tables) — supports Edit/Delete/Reorder.</summary>
    public sealed class RepairItemObservation
    {
        public int ObservationId { get; set; }
        public int RepairTicketId { get; set; }
        public int SortOrder { get; set; }
        public string ObservationText { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string CreatedByName { get; set; }
    }
}
