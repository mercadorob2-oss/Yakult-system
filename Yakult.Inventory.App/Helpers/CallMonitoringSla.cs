using System;

namespace Yakult.Inventory.App.Helpers
{
    public static class CallMonitoringSla
    {
        public static (int TargetHours, int WarningHours, string Name) GetResolutionSlaTarget(string priority, string issueType)
        {
            var p = (priority ?? string.Empty).Trim();
            if (p.Equals("Critical", StringComparison.OrdinalIgnoreCase)) return (24, 6, "Critical 24h");
            if (p.Equals("High", StringComparison.OrdinalIgnoreCase)) return (48, 12, "High 48h");
            if (p.Equals("Medium", StringComparison.OrdinalIgnoreCase)) return (72, 24, "Medium 72h");
            if (p.Equals("Low", StringComparison.OrdinalIgnoreCase)) return (120, 24, "Low 120h");

            return (72, 24, "Default 72h");
        }
    }
}

