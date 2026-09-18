using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// DTO for empty cartridge work-in-progress inventory
    /// Represents returned empty cartridges awaiting vendor refill
    /// SCHEMA: Uses CartridgeModelId (INT FK) not text
    /// </summary>
    public class EmptyCartridgeDto
    {
        public int EmptyCartridgeId { get; set; }
        public int CartridgeModelId { get; set; }
        public int? VendorId { get; set; }
        public int? SourceItemId { get; set; }
        public int? ReturnTransactionId { get; set; }
        public int Quantity { get; set; }
        public int? ConditionId { get; set; }
        public int? VendorBatchId { get; set; }
        public string Status { get; set; }
        public string RefillStatus { get; set; }
        public DateTime ReturnedAt { get; set; }
        public int ReturnedBy { get; set; }
        public int? ReqId { get; set; }
        public int? EmpId { get; set; }
        public int? BranchId { get; set; }
        public int? DeptId { get; set; }
        public string Remarks { get; set; }
        public DateTime CreatedDate { get; set; }
        public int CreatedBy { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedBy { get; set; }
    }
}
