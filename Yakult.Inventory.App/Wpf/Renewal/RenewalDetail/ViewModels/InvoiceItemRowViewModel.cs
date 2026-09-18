using System;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Renewal.RenewalDetail.ViewModels
{
    public class InvoiceItemRowViewModel : ViewModelBase
    {
        public int SetItemId { get; set; }
        public string ItemCode { get; set; }
        public string Description { get; set; }
        public string ItemName { get; set; }

        /// <summary>Falls back to the item's catalog Name when Description is blank.</summary>
        public string DisplayDescription => string.IsNullOrWhiteSpace(Description) ? ItemName : Description;
        public decimal Quantity { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount { get; set; }
        public DateTime? LineStartDate { get; set; }
        public DateTime? LineEndDate { get; set; }
        public string RenewalStatus { get; set; }

        public int? GroupId { get; set; }
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }

        /// <summary>Parent Tag Group this item belongs to — an independent, orthogonal
        /// grouping from Sub-Type, free-text label, no financial semantics of its own.</summary>
        public int? ParentTagGroupId { get; set; }
        public string ParentTag { get; set; }

        public string GroupDisplay => string.IsNullOrWhiteSpace(SubType)
            ? "—"
            : string.IsNullOrWhiteSpace(ReferenceCode) ? SubType : $"{SubType} #{ReferenceCode}";

        /// <summary>The group's Reference Code / ID on its own, for its own column.</summary>
        public string ReferenceCodeDisplay =>
            string.IsNullOrWhiteSpace(ReferenceCode) ? "—" : ReferenceCode;

        /// <summary>For the Table View column — blank rather than an em-dash when untagged,
        /// so a plain invoice's column doesn't read as noisy.</summary>
        public string ParentTagDisplay => string.IsNullOrWhiteSpace(ParentTag) ? "" : ParentTag;

        public string DisplayStatus => string.IsNullOrEmpty(RenewalStatus) ? "Currently In Use" : RenewalStatus;

        public string StartDateDisplay => LineStartDate?.ToString("MM/dd/yyyy") ?? "—";
        public string EndDateDisplay   => LineEndDate?.ToString("MM/dd/yyyy")   ?? "—";
        public string AmountDisplay    => $"₱{Amount:N2}";
        public string UnitPriceDisplay => $"₱{UnitPrice:N2}";
        public string QuantityDisplay  => Quantity.ToString("G29");

        public string StatusBackground
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Renewed":  return "#EBF5FB";
                    case "Expired":  return "#FEF2F2";
                    case "Archived": return "#F3F4F6";
                    default:         return "#EAFAF1";
                }
            }
        }

        public string StatusForeground
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Renewed":  return "#1A6DD0";
                    case "Expired":  return "#E03C31";
                    case "Archived": return "#5A6A7E";
                    default:         return "#1E9E5E";
                }
            }
        }

        public string RowBackground
        {
            get
            {
                switch (DisplayStatus)
                {
                    case "Renewed":  return "#F7FBFF";
                    case "Expired":  return "#FFF8F8";
                    case "Archived": return "#FAFAFA";
                    default:         return "White";
                }
            }
        }
    }
}
