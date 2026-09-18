using System;

namespace Yakult.Inventory.App.Models
{
    public class AuditEntry
    {
        public int Id { get; set; }
        public string Action { get; set; }
        public int? EntityId { get; set; }
        public string EntityType { get; set; }
        public int? UserId { get; set; }
        public string UserName { get; set; }
        public DateTime Timestamp { get; set; }
        public string Notes { get; set; }
        public string OldValues { get; set; }
        public string NewValues { get; set; }
        public string IpAddress { get; set; }
    }
}

