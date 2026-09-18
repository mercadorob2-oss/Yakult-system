using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallBranchSmtpProfileRow
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; }
        public string CompanyName { get; set; }
        public string DepartmentName { get; set; }
        public string RecipientEmails { get; set; }
        public string EscalationEmails { get; set; }
        /// <summary>Grid display: the address, or a placeholder when no override is configured.</summary>
        public string RecipientDisplay => string.IsNullOrWhiteSpace(RecipientEmails) ? "No override configured" : RecipientEmails.Trim();
        /// <summary>Grid display: escalation prefixed, or null (renders blank) when none.</summary>
        public string EscalationDisplay => string.IsNullOrWhiteSpace(EscalationEmails) ? null : "-> " + EscalationEmails.Trim();
        public int? ProfileId { get; set; }
        public string ProfileName { get; set; }
        public string SmtpServer { get; set; }
        public int? SmtpPort { get; set; }
        public bool? UseSsl { get; set; }
        public string SmtpUsername { get; set; }
        public string FromEmail { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}

