using System;

namespace Yakult.Inventory.App.Models
{
    /// <summary>
    /// Data Transfer Object for Set Images
    /// </summary>
    public class SetImageDto
    {
        public int ImageId { get; set; }
        public int SetId { get; set; }
        public string ImagePath { get; set; }
        public string ImageType { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadDate { get; set; }
        public byte[] ImageData { get; set; }
        public string MimeType { get; set; }
        public string OriginalFileName { get; set; }
        public int? FileSizeBytes { get; set; }

        /// <summary>
        /// Gets the filename from the full path, or the stored original filename for DB-backed images
        /// </summary>
        public string FileName => !string.IsNullOrEmpty(OriginalFileName)
            ? OriginalFileName
            : System.IO.Path.GetFileName(ImagePath ?? "");
        
        /// <summary>
        /// Gets formatted upload date
        /// </summary>
        public string UploadDateFormatted => UploadDate.ToString("yyyy-MM-dd HH:mm");
    }
}
