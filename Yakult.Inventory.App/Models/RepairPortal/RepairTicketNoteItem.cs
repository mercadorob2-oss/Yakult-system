using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    public sealed class RepairTicketNoteItem
    {
        public long NoteId { get; set; }
        public int RepairTicketId { get; set; }
        public string NoteType { get; set; }
        public string NoteText { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; }
    }
}
