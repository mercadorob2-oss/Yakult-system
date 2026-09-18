using System;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Detail.ViewModels
{
    /// <summary>Mirrors AttachmentDisplayItem's shape exactly (same property names the
    /// ImageCarouselControl binds to) so the control can be reused unmodified for a Part's
    /// evidence carousel.</summary>
    public sealed class PartAttachmentDisplayItem : ViewModelBase
    {
        public RepairPartAttachmentSummary Summary { get; }
        public int AttachmentId => Summary.PartAttachmentId;
        public string AttachmentType => Summary.AttachmentType;
        public string FileName => Summary.FileName;
        public string MimeType => Summary.MimeType;
        public DateTime UploadedAt => Summary.UploadedAt;
        public string UploadedByName => Summary.UploadedByName;
        public bool IsImage => string.Equals(AttachmentType, "Image", StringComparison.OrdinalIgnoreCase);
        public bool IsVideo => string.Equals(AttachmentType, "Video", StringComparison.OrdinalIgnoreCase);

        private byte[] _bytes;
        public byte[] Bytes { get => _bytes; set => SetField(ref _bytes, value); }

        private bool _isSelected;
        /// <summary>Multi-select checkbox state for bulk deletion from the Evidence carousel.</summary>
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public PartAttachmentDisplayItem(RepairPartAttachmentSummary summary)
        {
            Summary = summary;
        }
    }
}
