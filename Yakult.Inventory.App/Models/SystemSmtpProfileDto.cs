using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a system SMTP profile for sending emails
    /// </summary>
    public class SystemSmtpProfileDto
    {
        public int ProfileId { get; set; }
        public string ProfileName { get; set; }
        public string SmtpServer { get; set; }
        public int SmtpPort { get; set; }
        public bool UseSsl { get; set; }
        public string SmtpUsername { get; set; }
        public byte[] SmtpPasswordEnc { get; set; } // Encrypted password
        public int FromEmailId { get; set; }
        public bool IsActive { get; set; }

        // Joined fields for display
        public string FromEmailAddress { get; set; }
        public string FromDisplayName { get; set; }

        // Audit fields
        public DateTime? DateCreated { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
    }
}
