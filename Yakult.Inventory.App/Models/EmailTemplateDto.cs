using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents an email template for system notifications
    /// </summary>
    public class EmailTemplateDto
    {
        public int TemplateId { get; set; }
        public string TemplateKey { get; set; } // e.g., "InventorySetDeployed", "CartridgeRequestFulfilled"
        public string SubjectTemplate { get; set; }
        public string BodyTemplate { get; set; } // HTML template
        public bool IsHtml { get; set; }
        public bool IsActive { get; set; }

        // Default sender profile
        public int? DefaultSmtpProfileId { get; set; }
        public string DefaultSmtpProfileName { get; set; } // Display-only, joined from SystemSmtpProfile

        // Audit fields
        public DateTime? DateCreated { get; set; }
        public int? CreatedByUserId { get; set; }
        public DateTime? DateModified { get; set; }
        public int? ModifiedByUserId { get; set; }
    }
}
