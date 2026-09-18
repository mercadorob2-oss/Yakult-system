using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for empty cartridges grouped by vendor and model
    /// Used for quota-driven vendor batch creation
    /// SCHEMA: Includes CartridgeModelId for batch creation
    /// </summary>
    public class EmptyCartridgesByVendorDto
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public int CartridgeModelId { get; set; }
        public string CartridgeModel { get; set; }  // ModelNumber for display
        public int EmptyQty { get; set; }
    }
}
