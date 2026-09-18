using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a vendor cartridge batch for refill tracking
    /// </summary>
    public class VendorCartridgeBatchDto
    {
        public int BatchId { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public string CartridgeModel { get; set; }
        public int CartridgeModelId { get; set; }
        public int OriginalQty { get; set; }
        public int ReturnedQty { get; set; }  // Count of empty cartridges assigned to this batch
        public string Status { get; set; } // Active | SentForRefill | Closed
        public DateTime DateReceived { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Remarks { get; set; }
    }
}
