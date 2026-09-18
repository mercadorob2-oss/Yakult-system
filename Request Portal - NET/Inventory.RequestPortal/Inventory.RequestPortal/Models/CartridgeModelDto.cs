namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// DTO for dbo.CartridgeModel — cartridge master-data records.
    /// PORTED FROM: Yakult.Inventory.App/Models/CartridgeModelDto.cs (Cartridge Master Data →
    /// Cartridge Models page). Vendor association is resolved via the dbo.VendorCartridgeModel
    /// bridge table, same as desktop.
    /// </summary>
    public class CartridgeModelDto
    {
        public int CartridgeModelId { get; set; }
        public string? ModelNumber { get; set; }
        public int? VendorId { get; set; }
        public string? VendorName { get; set; }
        public bool IsRequestable { get; set; }
        public bool IsRefillable { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string? CreatedByName { get; set; }
        public int AvailableStock { get; set; }
    }
}
