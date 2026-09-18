using System;
using System.Collections.Generic;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.WPF.Invoice.ViewModels
{
    /// <summary>
    /// Presentation-layer row wrapper around NonLicensedInvoiceRepository.ConfirmedNonLicensedItemDto
    /// for the "Non-Licensed Invoices" grid — the confirmed-results companion to the Invoice
    /// License Review working queue. Read-only; there is no pending/staged state here.
    /// </summary>
    public sealed class NonLicensedInvoiceRow : ViewModelBase
    {
        public int ItemId { get; set; }
        public int SetId { get; set; }
        public int? ReqId { get; set; }
        public string InvoiceNumber { get; set; }
        public string PONumber { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string Status { get; set; }
        public string Site { get; set; }
        public string CompanyName { get; set; }
        public decimal InvoiceTotalAmount { get; set; }
        public string Department { get; set; }
        public string Employee { get; set; }
        public string ItemCode { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ItemType { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string CategoryName { get; set; }
        public string LegacyCategoryText { get; set; }
        public decimal Quantity { get; set; }
        public string Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
        public string VendorName { get; set; }
        public string ConditionName { get; set; }
        public string ReviewedByName { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Every distinct dbo.Item.ItemType found anywhere on this row's invoice (Set) —
        /// same enrichment as the Invoice License Review page, via SetRepository.GetItemTypesBySetIds.</summary>
        public IReadOnlyList<string> ItemTypesOnInvoice { get; set; } = Array.Empty<string>();

        public string ItemTypesOnInvoiceDisplay =>
            ItemTypesOnInvoice != null && ItemTypesOnInvoice.Count > 0
                ? string.Join(", ", ItemTypesOnInvoice)
                : ItemType;

        /// <summary>Every distinct category found anywhere on this row's invoice (Set), one per
        /// line — same enrichment as the Invoice License Review page.</summary>
        public IReadOnlyList<string> CategoriesOnInvoice { get; set; } = Array.Empty<string>();

        public string CategoriesOnInvoiceDisplay =>
            CategoriesOnInvoice != null && CategoriesOnInvoice.Count > 0
                ? string.Join("\n", CategoriesOnInvoice)
                : DisplayCategory;

        /// <summary>Effective category shown in the grid — prefers the ItemCategory master name,
        /// falls back to the legacy free-text Item.Category column.</summary>
        public string DisplayCategory =>
            !string.IsNullOrWhiteSpace(CategoryName) ? CategoryName : LegacyCategoryText;
    }
}
