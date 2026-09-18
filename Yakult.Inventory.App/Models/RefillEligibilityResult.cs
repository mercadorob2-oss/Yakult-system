namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Projection for refill eligibility calculations (read-only)
    /// </summary>
    public class RefillEligibilityResult
    {
        public int BatchId { get; set; }
        public string VendorName { get; set; }
        public string CartridgeModel { get; set; }
        public int CartridgeModelId { get; set; }
        public int OriginalQty { get; set; }
        public int ReturnedQty { get; set; }
        public string QtyPerCartridge { get; set; }
        public string Status { get; set; }
        public bool IsRefillable { get; set; }
    }
}
