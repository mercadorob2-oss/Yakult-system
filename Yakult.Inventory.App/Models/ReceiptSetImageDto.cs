using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Represents a single image attached to a receipt set's SI/DR/PO document.
    /// Multiple rows may exist per (ReceiptSetId, DocType) to support multi-page documents.
    /// </summary>
    public sealed class ReceiptSetImageDto
    {
        public int ImageId { get; set; }
        public int ReceiptSetId { get; set; }

        /// <summary>"SI", "DR", or "PO".</summary>
        public string DocType { get; set; }

        public string ImagePath { get; set; }
        public byte[] ImageBytes { get; set; }

        /// <summary>e.g. "image/jpeg" or "application/pdf". Null for rows written before the
        /// MimeType column existed -- treat null as image/jpeg for backward compatibility.</summary>
        public string MimeType { get; set; }

        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CreatedBy { get; set; }
    }
}
