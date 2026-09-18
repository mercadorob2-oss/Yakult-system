using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Wpf.OwnershipHistory;

namespace Yakult.Inventory.App.Wpf.OwnershipHistory.Views
{
    /// <summary>
    /// "Full View" dialog for a single Set/Invoice — merges the header-level AuditTrail
    /// entries with (for Sets) the ItemAuditTrail-based transfer cycle into one chronological
    /// vertical timeline.
    /// </summary>
    public partial class OwnershipTimelineWindow : Window
    {
        public OwnershipTimelineWindow(string entityType, int entityId, List<AuditEntry> auditEntries, List<TransferCycleStep> transferSteps)
        {
            InitializeComponent();

            TxtTitle.Text = $"Full History — {entityType} #{entityId}";
            TxtSubtitle.Text = entityType == "Set"
                ? "Ownership transfer cycle, including per-item Initiated/Completed steps"
                : "Sender / site change history";

            ListTimeline.ItemsSource = BuildTimeline(auditEntries, transferSteps);
        }

        private static List<TimelineEntry> BuildTimeline(List<AuditEntry> auditEntries, List<TransferCycleStep> transferSteps)
        {
            var timeline = new List<TimelineEntry>();

            foreach (var entry in auditEntries)
            {
                timeline.Add(new TimelineEntry
                {
                    Timestamp = entry.Timestamp,
                    Title = AuditFormatting.BuildHeadline(entry),
                    Subtitle = $"{entry.Action} · by {entry.UserName}",
                    ChangeLines = AuditFormatting.BuildChangeLines(entry),
                    AccentBrush = AuditFormatting.AccentBrushForAction(entry.Action)
                });
            }

            foreach (var step in transferSteps)
            {
                var title = step.Header;
                var separatorIndex = title.IndexOf(" — ", StringComparison.Ordinal);
                if (separatorIndex >= 0)
                    title = title.Substring(separatorIndex + 3);

                timeline.Add(new TimelineEntry
                {
                    Timestamp = step.Timestamp,
                    Title = title,
                    Subtitle = step.Detail,
                    AccentBrush = step.AccentBrush
                });
            }

            return timeline.OrderBy(x => x.Timestamp).ToList();
        }

        private class TimelineEntry
        {
            public DateTime Timestamp { get; set; }
            public string Title { get; set; }
            public string Subtitle { get; set; }
            public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
            public List<ChangeLine> ChangeLines { get; set; } = new List<ChangeLine>();
            public Brush AccentBrush { get; set; }
        }
    }
}
