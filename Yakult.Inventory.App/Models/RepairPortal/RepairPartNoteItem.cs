using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Same property names as RepairTicketNoteItem (NoteId/NoteText/CreatedByName/
    /// CreatedAt) so the generic NoteThreadControl can be reused unmodified.</summary>
    public sealed class RepairPartNoteItem
    {
        public long NoteId { get; set; }
        public int RepairPartId { get; set; }
        public int RepairTicketId { get; set; }
        public string NoteType { get; set; }
        public string NoteText { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
    }
}
