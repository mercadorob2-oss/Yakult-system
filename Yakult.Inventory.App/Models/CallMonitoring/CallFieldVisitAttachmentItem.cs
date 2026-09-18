using System;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallFieldVisitAttachmentItem
    {
        public int AttachmentId { get; set; }
        public int FieldVisitId { get; set; }
        public string FileName { get; set; }
        public string MimeType { get; set; }
        public int? FileSizeBytes { get; set; }
        public DateTime UploadedAt { get; set; }
        public string UploadedByName { get; set; }
    }
}
