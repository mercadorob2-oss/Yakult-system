namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// Read-only row for the "View Cartridges" master-data page — individual physical
    /// cartridge items (dbo.Item where Category = 'Cartridge').
    /// PORTED FROM: Yakult.Inventory.App/Wpf/CartridgeManagement/ViewModels/ViewCartridgesViewModel.cs
    /// (FetchItemsAsync). Only the fields actually shown on the desktop grid are carried over.
    /// </summary>
    public class CartridgeItemViewDto
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? SerialNumber { get; set; }
        public string? ModelNumber { get; set; }
        public string? ConditionName { get; set; }
        public string? LatestStatus { get; set; }
        public int RepairCount { get; set; }
        public string? VendorName { get; set; }
        public int StockOnHand { get; set; }
        public bool Active { get; set; }
        public DateTime DateCreated { get; set; }
        public string? CreatedByName { get; set; }
        public string? Remarks { get; set; }
        public decimal Amount { get; set; }
    }
}
