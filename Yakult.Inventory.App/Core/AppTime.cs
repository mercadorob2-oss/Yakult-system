using System;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Centralized time helpers. Call Monitoring timestamps are treated as UTC in the DB.
    /// </summary>
    public static class AppTime
    {
        public static DateTime UtcNow => DateTime.UtcNow;

        public static DateTime AssumeUtc(DateTime value)
        {
            if (value.Kind == DateTimeKind.Utc)
                return value;

            if (value.Kind == DateTimeKind.Local)
                return value.ToUniversalTime();

            // Unspecified: assume it's already UTC (common when loading from SQL).
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        public static string ToLocalString(DateTime value, string format = "g")
        {
            var utc = AssumeUtc(value);
            return utc.ToLocalTime().ToString(string.IsNullOrWhiteSpace(format) ? "g" : format);
        }
    }
}

