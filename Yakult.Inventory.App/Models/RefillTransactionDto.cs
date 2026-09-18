using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Lightweight log of an outbound refill transaction (empties sent to vendor).
    /// Receipt tracking removed: refilled cartridges are entered as new stock via BatchAddItemDialog.
    /// </summary>
    public class RefillTransactionDto
    {
        public int RefillTransactionId { get; set; }
        public int BatchId { get; set; }
        public int VendorId { get; set; }
        public string VendorName { get; set; }
        public int SentQty { get; set; }
        public DateTime SentDate { get; set; }
        public string Status { get; set; } // Sent
        public int CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string Remarks { get; set; }
    }
}
