namespace Yakult.Inventory.App.Models.BorrowItems
{
    public sealed class BorrowItemLookup
    {
        public int ItemId { get; set; }
        public string SerialNumber { get; set; }
        public string ItemName { get; set; }
        public string ItemDescription { get; set; }
        public string ModelNumber { get; set; }
        public string Category { get; set; }

        public string DisplayText
        {
            get
            {
                var name = (ItemName ?? string.Empty).Trim();
                var model = (ModelNumber ?? string.Empty).Trim();
                var serial = (SerialNumber ?? string.Empty).Trim();
                var desc = (ItemDescription ?? string.Empty).Trim();

                var baseText = name.Length > 0 ? name : (serial.Length > 0 ? serial : $"ItemId {ItemId}");
                if (model.Length > 0) baseText += $" ({model})";
                if (desc.Length > 0) baseText += $" - {desc}";
                return baseText;
            }
        }
    }
}

