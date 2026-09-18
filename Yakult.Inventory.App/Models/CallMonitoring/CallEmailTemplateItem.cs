using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEmailTemplateItem
    {
        public int TemplateId { get; set; }
        public string TemplateType { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public bool IsActive { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
    }
}

