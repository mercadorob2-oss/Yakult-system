using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketNoteItem
    {
        public int NoteId { get; set; }
        public int TicketId { get; set; }
        public string NoteType { get; set; }
        public string NoteText { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int? CreatedByUserId { get; set; }
        public string CreatedByName { get; set; }
    }
}

