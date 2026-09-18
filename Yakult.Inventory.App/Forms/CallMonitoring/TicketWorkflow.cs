using System;

namespace Yakult.Inventory.App.Forms.CallMonitoring
{
    internal static class TicketWorkflow
    {
        // Single status catalog shared by every dropdown builder, filter, and
        // rank check. Matches dbo.sp_Call_SetTicketStatus (canonical migration).
        public static readonly string[] CanonicalStatuses =
        {
            "Pending",
            "In Progress",
            "Escalated",
            "Forwarded to Repair",
            "Resolved (Temporary)",
            "Solved",
            "Closed",
            "Reopened"
        };

        // Direct-pick dropdown catalog. "Forwarded to Repair" is deliberately
        // excluded: forwarding must go through the forward flow so a Repair
        // ticket gets linked. Rank/filters still recognize it (see below).
        public static readonly string[] DirectPickStatuses =
        {
            "Pending",
            "In Progress",
            "Escalated",
            "Resolved (Temporary)",
            "Solved",
            "Closed",
            "Reopened"
        };

        public static int GetStatusRank(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Pending", StringComparison.OrdinalIgnoreCase)) return 0;
            if (s.Equals("In Progress", StringComparison.OrdinalIgnoreCase)) return 1;
            if (s.Equals("Reopened", StringComparison.OrdinalIgnoreCase)) return 1; // treat as active work
            if (s.Equals("Escalated", StringComparison.OrdinalIgnoreCase)) return 2;
            if (s.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase)) return 2; // active side-branch, never backward from terminal
            if (s.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)) return 3;
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase)) return 4;
            if (s.Equals("Closed", StringComparison.OrdinalIgnoreCase)) return 4;
            return -1;
        }

        public static bool IsStatusTransitionAllowed(string currentStatus, string newStatus)
        {
            var cur = (currentStatus ?? string.Empty).Trim();
            var next = (newStatus ?? string.Empty).Trim();

            if (string.IsNullOrWhiteSpace(next) || next.Equals(cur, StringComparison.OrdinalIgnoreCase))
                return true;

            // Reopened should only be set via the Reopen action (from a final state).
            if (next.Equals("Reopened", StringComparison.OrdinalIgnoreCase))
                return cur.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                    || cur.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
                    || cur.Equals("Closed", StringComparison.OrdinalIgnoreCase);

            var curRank = GetStatusRank(cur);
            var nextRank = GetStatusRank(next);

            // Unknown statuses: don't block (avoid breaking environments with custom values).
            if (curRank < 0 || nextRank < 0)
                return true;

            // No backward movement.
            return nextRank >= curRank;
        }
    }
}
