using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for dbo.CartridgeModel table - Single source of truth for cartridge models
    /// </summary>
    public class CartridgeModelDto
    {
        public int CartridgeModelId { get; set; }
        public string ModelNumber { get; set; }
        public int? VendorId { get; set; }
        public string VendorName { get; set; }
        public bool IsRequestable { get; set; }
        public bool IsRefillable { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public int AvailableStock { get; set; }

        /// <summary>
        /// Display name for dropdown selection
        /// </summary>
        public string DisplayName => string.IsNullOrWhiteSpace(VendorName)
            ? ModelNumber
            : $"{ModelNumber} - {VendorName}";
    }
}
