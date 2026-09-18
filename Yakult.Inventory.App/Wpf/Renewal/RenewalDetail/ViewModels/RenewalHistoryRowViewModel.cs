using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    public class RenewalHistoryRowViewModel : ViewModelBase
    {
        public string RenewalStatus { get; set; }
        public DateTime? RenewedDate { get; set; }
        public int RenewalCount { get; set; }
        public DateTime? NewStartDate { get; set; }
        public DateTime? NewEndDate { get; set; }
        public int? RenewalYears { get; set; }
        public string RenewalNotes { get; set; }
        public decimal? RenewalAmount { get; set; }
        public string CreatedByUsername { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsArchived { get; set; }
        public string ItemName { get; set; }
        public string ItemCode { get; set; }

        /// <summary>True when this is the item's most recent, non-archived record — i.e. the one
        /// actually in effect right now. Computed by the caller (grouped per ItemId), since the
        /// stored RenewalStatus describes how the row was created, not whether it's still current.</summary>
        public bool IsCurrent { get; set; }

        public string ItemDisplay => string.IsNullOrWhiteSpace(ItemCode)
            ? (ItemName ?? "—")
            : $"{ItemCode} — {ItemName}";

        public string DateDisplay  => (RenewedDate ?? CreatedAt).ToString("MMMM d, yyyy");
        public string TimeDisplay  => (RenewedDate ?? CreatedAt).ToString("h:mm tt");
        /// <summary>RenewalCount=1 is this item's first tracked record ("Original"); each count
        /// above that is one renewal past the original, matching the "Original" / "Renewal #N"
        /// wording used elsewhere in the app (e.g. the Renewals reports' chain labels).</summary>
        public string CounterLabel => RenewalCount <= 1 ? "Original" : $"Renewal #{RenewalCount - 1}";
        public bool HasNotes       => !string.IsNullOrWhiteSpace(RenewalNotes);

        public string PeriodDisplay
        {
            get
            {
                if (NewStartDate.HasValue && NewEndDate.HasValue)
                    return $"{NewStartDate:MM/dd/yyyy} → {NewEndDate:MM/dd/yyyy}";
                return "—";
            }
        }

        public string AmountDisplay => RenewalAmount.HasValue ? $"₱{RenewalAmount:N2}" : "—";
        public string YearsDisplay  => RenewalYears.HasValue
            ? $"{RenewalYears} yr{(RenewalYears > 1 ? "s" : "")}"
            : "—";

        /// <summary>What's actually shown on the badge — reflects whether this record is presently
        /// in effect, not the raw stored RenewalStatus (see IsCurrent).</summary>
        public string DisplayStatus
        {
            get
            {
                if (IsArchived) return "Archived";
                if (!IsCurrent) return "Superseded";
                if (NewEndDate.HasValue && NewEndDate.Value.Date < DateTime.Today) return "Expired";
                return "Active";
            }
        }

        public string StatusColor
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Archived":   return "#5A6A7E";
                    case "Superseded": return "#1A6DD0";
                    case "Expired":    return "#E03C31";
                    default:           return "#1E9E5E";
                }
            }
        }

        public string StatusBackground
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Archived":   return "#F3F4F6";
                    case "Superseded": return "#EBF5FB";
                    case "Expired":    return "#FEF2F2";
                    default:           return "#EAFAF1";
                }
            }
        }

        /// <summary>Clarifies what the status badge means for this specific line item — since
        /// entries for different items can otherwise look identical unless ItemDisplay is also
        /// shown.</summary>
        public string StatusExplanation
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Archived":   return "This renewal record has been archived.";
                    case "Superseded": return "This period has since been renewed — a newer record now covers this item.";
                    case "Expired":    return "This item's coverage ended without being renewed.";
                    default:           return "This is the current, active coverage period for this item.";
                }
            }
        }
    }
}
