using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEmailLogItem
    {
        public long EmailLogId { get; set; }
        public int? TicketId { get; set; }
        public string Branch { get; set; }
        public string EmailType { get; set; }
        public string Recipient { get; set; }
        public string Subject { get; set; }
        public string Status { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime DateSent { get; set; }
        public int? CreatedByUserId { get; set; }
    }
}

