using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketEscalationOverrideItem
    {
        public int TicketId { get; set; }
        public int DaysToSupervisor { get; set; }
        public int DaysToManager { get; set; }
        public string Reason { get; set; }
        public int? OverriddenByUserId { get; set; }
        public DateTime? OverriddenAt { get; set; }
    }
}

