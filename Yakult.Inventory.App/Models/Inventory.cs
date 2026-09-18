using System;

namespace Yakult.Inventory.App.Models
{
    public class Inventory
    {
        public int InvId { get; set; }
        public string Description { get; set; }
        public string EntryType { get; set; }  // Positive, Negative, Fixed Assets
        public int Quantity { get; set; }
        public DateTime DatePosted { get; set; }
        public int PostedBy { get; set; }
        public int? ReqId { get; set; }  // Nullable - only for Hardware items via Request
        public byte[] RowVer { get; set; }
        public int? ItemId { get; set; }  // Nullable
        public int SetId { get; set; }  // REQUIRED - No longer nullable after database fix!
    }
}
