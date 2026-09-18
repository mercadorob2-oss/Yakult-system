using System;

namespace Yakult.Inventory.App.Models.RepairPortal
{
    /// <summary>Read-only row for the aggregate "Evidence Gallery" section — a union of
    /// whole-equipment (RepairTicketAttachment) and all Parts' (RepairPartAttachment) evidence,
    /// thumbnails only (bytes loaded lazily like AttachmentDisplayItem does for the ticket-level
    /// carousel).</summary>
    public sealed class AttachmentGalleryItem
    {
        public int AttachmentId { get; set; }
        public bool IsPartLevel { get; set; }
        public int? RepairPartId { get; set; }
        public string PartDisplayName { get; set; }
        public string AttachmentType { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; }
    }
}
