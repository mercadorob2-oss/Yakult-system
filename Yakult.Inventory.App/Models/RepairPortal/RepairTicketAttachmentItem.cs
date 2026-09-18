using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Full attachment including bytes — used only when a single attachment is opened/previewed.</summary>
    public sealed class RepairTicketAttachmentItem
    {
        public int AttachmentId { get; set; }
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

    /// <summary>Lightweight attachment summary (no FileBytes) for gallery/detail thumbnail lists.</summary>
    public sealed class RepairTicketAttachmentSummary
    {
        public int AttachmentId { get; set; }
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
