namespace Yakult.Inventory.App.Models
{
    public class InventoryCreateDto
    {
        public string Description { get; set; }
        public int Quantity { get; set; }
        public int PostedBy { get; set; }
        public int ItemId { get; set; }
        public int? ReqId { get; set; }
        public int? SetId { get; set; }  // Optional - will be auto-determined if null
    }
}
