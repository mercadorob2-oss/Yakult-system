using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for Vendor Batch Audit Trail
    /// Represents request-level rows that contributed to a vendor batch
    /// Used for read-only audit trail display with optional controlled editing
    /// </summary>
    public class VendorBatchAuditTrailDto
    {
        public int EmptyCartridgeId { get; set; }  // For edit operations
        public int? RequestId { get; set; }
        public DateTime RequestDate { get; set; }
        public int ReturnedQty { get; set; }
        public int CartridgeModelId { get; set; }   // Used by transfer/remove operations
        public string CartridgeModel { get; set; }
        public string Vendor { get; set; }
        public int BatchId { get; set; }
        public string ReturnedByName { get; set; }  // User who returned the cartridges
        public string Remarks { get; set; }
    }
}
