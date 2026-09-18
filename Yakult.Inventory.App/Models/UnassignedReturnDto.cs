using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a returned empty cartridge that has not yet been assigned to any refill batch.
    /// Used by the manual batch-assignment wizard (ManualBatchAssignmentDialog).
    ///
    /// DESIGN NOTE:
    ///   SupplierId / SupplierName = the vendor that originally issued the cartridge (for audit display).
    ///   This is NOT the refill vendor.  The refill vendor is chosen separately in Step 2.
    /// </summary>
    public class UnassignedReturnDto
    {
        public int EmptyCartridgeId { get; set; }
        public int CartridgeModelId { get; set; }
        public string CartridgeModel { get; set; }

        /// <summary>Original issuing vendor — informational only, does not constrain refill vendor.</summary>
        public int SupplierId { get; set; }
        public string SupplierName { get; set; }

        public int Quantity { get; set; }
        public DateTime ReturnedAt { get; set; }
        public string Remarks { get; set; }

        /// <summary>
        /// 'GOOD' | 'DAMAGED' | null (legacy rows inserted before the ConditionStatus migration).
        /// Null is treated as non-damaged for backward-compatibility.
        /// </summary>
        public string ConditionStatus { get; set; }
    }
}
