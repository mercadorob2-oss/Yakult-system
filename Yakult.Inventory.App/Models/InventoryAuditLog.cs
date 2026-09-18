using System;

namespace Yakult.Inventory.App.Models
{
    public class InventoryAuditLog
    {
        public int AuditId { get; set; }
        public int? InvId { get; set; }
        public string Action { get; set; }
        public int? SetId { get; set; }
        public int? ItemId { get; set; }
        public int? ReqId { get; set; }
        public string ErrorMessage { get; set; }
        public DateTime AuditDate { get; set; }
        public string AuditUser { get; set; }
    }
}
