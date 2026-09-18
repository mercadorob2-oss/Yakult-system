using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallEmailSettingsItem
    {
        public int SettingsId { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        public string SmtpUsername { get; set; }
        public byte[] SmtpPasswordEnc { get; set; }
        public string FromName { get; set; }
        public string FromEmail { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int? UpdatedByUserId { get; set; }
    }
}

