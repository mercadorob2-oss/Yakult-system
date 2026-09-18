namespace Yakult.Inventory.App.Models
{
    public class ItemCatalogDto
    {
        public int ItemId { get; set; }
        public string ItemCode { get; set; }
        public string Name { get; set; }
        public string ItemType { get; set; }
        public string UnitOfMeasure { get; set; }
        public decimal UnitPrice { get; set; }
        public string DisplayLabel => string.IsNullOrEmpty(ItemCode) ? Name : $"{Name} ({ItemCode})";
    }
}
