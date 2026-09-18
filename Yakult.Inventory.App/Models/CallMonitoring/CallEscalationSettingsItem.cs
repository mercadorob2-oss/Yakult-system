using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEscalationSettingsItem
    {
        public int DaysToSupervisor { get; set; }
        public int DaysToManager { get; set; }
        public string SupervisorPosition { get; set; }
        public string ManagerPosition { get; set; }
    }
}

