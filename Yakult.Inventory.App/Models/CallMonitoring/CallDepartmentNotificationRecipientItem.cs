using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallDepartmentNotificationRecipientItem
    {
        public int DeptId { get; set; }
        public string DepartmentName { get; set; }
        public string RecipientEmails { get; set; }
        public string EscalationEmails { get; set; }
        public bool IsActive { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
    }
}

