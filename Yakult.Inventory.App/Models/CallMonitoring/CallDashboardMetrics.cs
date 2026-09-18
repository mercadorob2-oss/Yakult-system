using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallDashboardMetrics
    {
        public int OpenTickets { get; set; }
        public int CriticalTickets { get; set; }
        public int? AvgResolutionMinutes { get; set; }
        public int TodaysVolume { get; set; }
    }
}

