using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a log entry for sent system emails
    /// </summary>
    public class SystemEmailLogDto
    {
        public int LogId { get; set; }
        public string TemplateKey { get; set; }
        public string Recipients { get; set; } // Comma-separated
        public string Subject { get; set; }
        public string Status { get; set; } // Sent, Failed, Skipped
        public string ErrorMessage { get; set; }
        public int? ProfileId { get; set; }
        public string ProfileName { get; set; }
        public DateTime SentDate { get; set; }
        public int? SentByUserId { get; set; }
        public string SentByUserName { get; set; }

        // Optional: Reference to related entity
        public string EntityType { get; set; } // e.g., "InventorySet", "CartridgeRequest"
        public int? EntityId { get; set; }
    }
}
