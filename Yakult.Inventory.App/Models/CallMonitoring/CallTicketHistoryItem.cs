using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketHistoryItem
    {
        public int HistoryId { get; set; }
        public int TicketId { get; set; }
        public DateTime? ChangedAt { get; set; }
        public int? ChangedByUserId { get; set; }
        public string ChangedByName { get; set; }
        public string FieldName { get; set; }
        public string OldValue { get; set; }
        public string NewValue { get; set; }
        public string Note { get; set; }
    }
}

