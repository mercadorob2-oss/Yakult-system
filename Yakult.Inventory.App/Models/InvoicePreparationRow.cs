using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>One row of dbo.InvoicePreparation — a Sub-Type Group, for the Invoice
    /// Preparation list page. "SupplierName" is derived from the member items' own
    /// Vendor at query time ("(Mixed)" if they don't all share one) rather than stored
    /// on the group itself, since the group-creation popup never asks for a supplier.</summary>
    public sealed class InvoicePreparationSummaryRow
    {
        public int PreparationId { get; set; }
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }
        public string SupplierName { get; set; }
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string Status { get; set; }
        public int ItemCount { get; set; }
        public decimal Subtotal { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool Selected { get; set; }
    }

    /// <summary>One dbo.InvoicePreparationItem row, joined with its dbo.Item for display —
    /// used by the read-only Preview Contents dialog.</summary>
    public sealed class InvoicePreparationItemDto
    {
        public int PreparationItemId { get; set; }
        public int PreparationId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string Remarks { get; set; }

        /// <summary>The item's Part# (sourced from dbo.Renewals.PartNumber).</summary>
        public string PartNumber { get; set; }

        /// <summary>dbo.Item.ModelNumber — the physical model number, when the item carries one.</summary>
        public string ModelNumber { get; set; }

        public string SerialNumber { get; set; }
        public string ItemType { get; set; }

        /// <summary>dbo.ItemCategory.Name when the item has a CategoryId, falling back to
        /// the free-text dbo.Item.Category column.</summary>
        public string Category { get; set; }

        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }

        /// <summary>The owning group's Sub-Type and Reference Code — only populated when
        /// the item is fetched via <see cref="Repositories.InvoicePreparationRepository.GetItemsForGroups"/>
        /// (e.g. to pre-fill SoftwareServiceSetDialog, including its own Sub-Type/Reference
        /// Code header fields with what was typed on the Items Page's group dialog); null
        /// from <see cref="Repositories.InvoicePreparationRepository.GetGroupItems"/>, where
        /// the Sub-Type is already shown once on the owning group's header.</summary>
        public string SubType { get; set; }
        public string ReferenceCode { get; set; }

        /// <summary>The owning group's Start/End Date (typed on the Items Page's group
        /// dialog) — only populated alongside SubType/ReferenceCode above.</summary>
        public DateTime? BeginDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
