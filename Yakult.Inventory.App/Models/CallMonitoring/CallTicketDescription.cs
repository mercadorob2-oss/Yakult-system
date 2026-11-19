#nullable enable
using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    // Shared with portal intake so older clients can read the complete request
    // through the existing Issue column, alongside the structured columns.
    public static class CallTicketDescription
    {
        public static string Format(string? issueType, string? classification, string? deviceName, string? concern)
        {
            var lines = new List<string>();
            if (!string.IsNullOrWhiteSpace(issueType)) lines.Add("Issue Type: " + issueType.Trim());
            if (!string.IsNullOrWhiteSpace(classification)) lines.Add("Classification: " + classification.Trim());
            if (!string.IsNullOrWhiteSpace(deviceName))
            {
                var isComputer = string.Equals(classification, "Computer", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(classification, "Laptop", StringComparison.OrdinalIgnoreCase);
                lines.Add((isComputer ? "Computer Name: " : "Device / application name: ") + deviceName.Trim());
            }
            var text = (concern ?? string.Empty).Trim().Replace("\r\n", "\n").Replace("\r", "\n");
            if (lines.Count == 0) return text;
            return string.Join("\n", lines) + "\n\nConcern:\n" + text;
        }

        public static string ForDisplay(string? issueType, string? classification, string? deviceName, string? issue)
        {
            var text = (issue ?? string.Empty).Trim();
            if ((text.StartsWith("Issue Type:", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("Classification:", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("Computer Name:", StringComparison.OrdinalIgnoreCase)
                    || text.StartsWith("Device / application name:", StringComparison.OrdinalIgnoreCase))
                && text.IndexOf("\nConcern:", StringComparison.OrdinalIgnoreCase) >= 0)
                return text;
            return Format(issueType, classification, deviceName, text);
        }
    }
}
