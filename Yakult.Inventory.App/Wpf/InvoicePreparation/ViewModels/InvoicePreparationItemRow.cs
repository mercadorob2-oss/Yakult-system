using System;

namespace Yakult.Inventory.App.WPF.InvoicePreparation.ViewModels
{
    /// <summary>Read-only presentation row for the Preview Contents dialog — one member
    /// item of a Sub-Type Group. No editing here; membership and category are set from
    /// the Items Page.</summary>
    public sealed class InvoicePreparationItemRow
    {
        public int PreparationItemId { get; set; }
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string Description { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Amount => Quantity * UnitPrice;
        public string Remarks { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public string ItemType { get; set; }
        public string Category { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
    }
}
