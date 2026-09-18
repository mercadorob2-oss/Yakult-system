using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Full Part attachment including bytes — used only when a single attachment is
    /// opened/previewed.</summary>
    public sealed class RepairPartAttachmentItem
    {
        public int PartAttachmentId { get; set; }
        public int RepairPartId { get; set; }
        public int RepairTicketId { get; set; }
        public string AttachmentType { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public byte[] FileBytes { get; set; }
        public int? FileSizeBytes { get; set; }
        public int SortOrder { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; }
    }

    /// <summary>Lightweight Part attachment summary (no FileBytes) for the expanded card's
    /// evidence carousel.</summary>
    public sealed class RepairPartAttachmentSummary
    {
        public int PartAttachmentId { get; set; }
        public int RepairPartId { get; set; }
        public int RepairTicketId { get; set; }
        public string AttachmentType { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public int? FileSizeBytes { get; set; }
        public int SortOrder { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; }
    }
}
