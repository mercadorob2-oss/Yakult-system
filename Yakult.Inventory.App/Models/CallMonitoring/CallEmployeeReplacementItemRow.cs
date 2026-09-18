using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEmployeeReplacementItemRow
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public string TicketStatus { get; set; }

        public DateTime ResolutionMarkedAtUtc { get; set; }
        public string ResolutionType { get; set; }

        public int? ReplacementOldItemId { get; set; }
        public string ReplacementOldItem { get; set; }
        public int? ReplacementNewItemId { get; set; }
        public string ReplacementNewItem { get; set; }
        public int? ReplacementQty { get; set; }

        public string Remarks { get; set; }
    }
}

