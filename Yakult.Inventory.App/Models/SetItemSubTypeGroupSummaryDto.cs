using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// One row of dbo.vw_SetItemSubTypeGroups — a Sub-Type Group with its aggregated member
    /// SetItem subtotal plus the persisted financial-card overrides (Subtotal/VAT/WHT/Discount).
    /// Shared read shape for both the Invoice detail page and the Renewal detail window.
    /// </summary>
    public class SetItemSubTypeGroupSummaryDto
    {
        public int GroupId { get; set; }
        public int SetId { get; set; }
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int ItemCount { get; set; }

        /// <summary>Computed SUM(SetItem.Amount) — fallback when SubtotalOverride is null.</summary>
        public decimal Subtotal { get; set; }
        public decimal? SubtotalOverride { get; set; }
        public decimal? VatPercent { get; set; }
        public decimal? WhtPercent { get; set; }
        public decimal? DiscountPercent { get; set; }
    }
}
