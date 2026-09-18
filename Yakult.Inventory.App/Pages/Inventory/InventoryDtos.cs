using System;

namespace Yakult.Inventory.App.Pages.Inventory
{
    // Moved verbatim out of ViewInventoryPage.cs when that page was converted to a thin WPF
    // ElementHost wrapper. Kept in this namespace/location because EditInventoryDialog.cs and
    // ItemMovementAuditDetailsDialog.cs reference these types via
    // "using Yakult.Inventory.App.Pages.Inventory;".

    public class InventoryViewDto
    {
        public int InvId { get; set; }
        public string Description { get; set; }
        public string EntryType { get; set; }
        public int Quantity { get; set; }
        public DateTime DatePosted { get; set; }
        public string PostedByName { get; set; }
        public int? RequestId { get; set; }
        public string ItemName { get; set; }
        public string Category { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
        public int CategoryTotalStock { get; set; }
        public bool IsArchived { get; set; }

        /// <summary>For checkbox selection in grid (WPF list-page binding).</summary>
        public bool Selected { get; set; }
    }

    public class CategoryStockDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; }
        public int TotalStock { get; set; }
        public int ActiveItems { get; set; }
        public int RequestedItems { get; set; }
    }

    public class HardwareInventoryDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }
        public int GoodCount { get; set; }
        public int DamagedCount { get; set; }
        public string FixedAsset { get; set; }
    }

    public class SoftwareLicenseInventoryDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }
        public int WarrantyYears { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int RenewalCount { get; set; }
    }

    public class ServicesInventoryDto
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; }
        public string CategoryName { get; set; }
        public int ActiveStock { get; set; }
        public int RequestedItems { get; set; }
        public int WarrantyYears { get; set; }
        public DateTime? WarrantyStartDate { get; set; }
        public DateTime? WarrantyEndDate { get; set; }
        public int RenewalCount { get; set; }
    }
}
