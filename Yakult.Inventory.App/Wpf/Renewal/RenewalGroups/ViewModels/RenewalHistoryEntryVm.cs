using System;
using System.Collections.Generic;
using System.Linq;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalGroups.ViewModels
{
    /// <summary>
    /// One entry in the Renewal History timeline window, ported from
    /// ViewRenewalGroupPage.ShowHistoryDialog()'s anonymous "history" projection.
    /// </summary>
    public sealed class RenewalHistoryEntryVm
    {
        public int Number { get; }
        public bool IsLatest { get; }
        public RenewalDto Set { get; }
        public List<string> Items { get; }

        public RenewalHistoryEntryVm(int number, bool isLatest, RenewalDto set, List<string> items, DateTime freezeReferenceDate, bool isRenewed)
        {
            Number = number;
            IsLatest = isLatest;
            Set = set;
            Items = items ?? new List<string>();
            IsRenewed = isRenewed;

            int? ownDays = set.EndDate.HasValue ? (int?)(int)(set.EndDate.Value.Date - freezeReferenceDate).TotalDays : null;
            string daysStr = ownDays.HasValue
                ? (ownDays.Value < 0 ? $"Expired {Math.Abs(ownDays.Value)}d ago" : $"{ownDays.Value} days left")
                : "—";
            if (isRenewed) daysStr += "  (countdown paused)";
            DaysDisplay = daysStr;
        }

        public bool IsRenewed { get; }
        public string SetCode => Set.SetCode ?? "—";
        public string DocumentNumber => Set.DocumentNumber ?? "—";
        public string RefNumber => string.IsNullOrEmpty(Set.ReferenceNumber) ? "—" : Set.ReferenceNumber;
        public string DocumentDateDisplay => Set.DocumentDate.HasValue ? Set.DocumentDate.Value.ToString("MM/dd/yyyy") : "—";
        public string Company => Set.CompanyName ?? "—";
        public string Site => Set.SiteDisplay ?? "—";
        public string Duration => $"{(Set.StartDate?.ToString("MM/dd/yyyy") ?? "—")}  →  {(Set.EndDate?.ToString("MM/dd/yyyy") ?? "—")}";
        public string DaysDisplay { get; }
        public string StatusDisplay => Set.SetLevelStatus ?? Set.ExpiryStatus ?? "—";
        public string AmountDisplay => $"₱{Set.TotalAmountDue:N2}";

        /// <summary>Builds the newest-first display list for a group's renewal chain.</summary>
        public static List<RenewalHistoryEntryVm> BuildFor(RenewalGroupRow group, Func<int, List<string>> itemNamesGetter)
        {
            var chronological = group.Chain.OrderBy(r => r.SetId).ToList();
            var entries = new List<RenewalHistoryEntryVm>();

            for (int i = 0; i < chronological.Count; i++)
            {
                var set = chronological[i];
                int number = i + 1;
                bool isLatest = number == chronological.Count;
                bool isRenewed = !isLatest && chronological.Count > 1;

                DateTime referenceDate = DateTime.Today;
                if (isRenewed && i + 1 < chronological.Count)
                {
                    var next = chronological[i + 1];
                    if (next.StartDate.HasValue) referenceDate = next.StartDate.Value.Date;
                }

                entries.Add(new RenewalHistoryEntryVm(number, isLatest, set, itemNamesGetter(set.SetId), referenceDate, isRenewed));
            }

            entries.Reverse();
            return entries;
        }
    }
}
