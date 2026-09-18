using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents an active DISPOSE or SELL vendor batch waiting to be finalized.
    /// </summary>
    public class OutboundBatchDto
    {
        public int       BatchId      { get; set; }
        public string    BatchPurpose { get; set; }   // "DISPOSE" | "SELL"
        public int       VendorId     { get; set; }
        public string    VendorName   { get; set; }
        public int       TotalQty     { get; set; }   // SUM(ReturnedQty)
        public string    Status       { get; set; }   // "Active" | "Disposed" | "Sold"
        public DateTime  CreatedDate  { get; set; }
        public DateTime? ClosedDate   { get; set; }   // set when finalized
        public string    Remarks      { get; set; }
        public string    ModelSummary { get; set; }   // e.g. "HP 85A (3), Canon 728 (2)"
    }
}
