using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallTicketVolumePoint
    {
        public DateTime Day { get; set; }
        public int TicketCount { get; set; }
    }
}

