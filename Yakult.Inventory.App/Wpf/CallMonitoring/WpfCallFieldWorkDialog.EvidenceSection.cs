using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Yakult.Inventory.App.Models.CallMonitoring;
using System.Windows.Media.Imaging;

namespace Yakult.Inventory.App.Wpf.CallMonitoring
{
    public sealed partial class WpfCallFieldWorkDialog
    {
        private async Task<int> GuardPhotoCountAsync()
        {
            if (_photos.Count >= 20)
            {
                MessageBox.Show(this, "Maximum 20 photos per visit.", "Field Work", MessageBoxButton.OK, MessageBoxImage.Warning);
                return 0;
            }
            return 1;
        }

        private string FormatFileSize(int bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024*1024) return $"{bytes/1024} KB";
            return $"{bytes/(1024*1024):0.0} MB";
        }

        private void UpdateEvidenceHeader()
        {
            // Called after photos load to update preview labels with KB formatting
            if (_photoPreviewLabel != null && _photos != null)
            {
                var sel = _photoGrid.SelectedItem as CallFieldVisitAttachmentItem;
                if (sel == null && _photos.Count > 0) sel = _photos[0];
                if (sel != null) _photoPreviewLabel.Text = $"{sel.FileName} • {FormatFileSize(sel.FileSizeBytes ?? 0)} • {sel.UploadedByName ?? "—"} • {sel.UploadedAt:MMM d, yyyy h:mm tt}";
            }
            if (_signaturePreviewLabel != null)
            {
                _signaturePreviewLabel.Text = _currentVisit != null && _currentVisit.HasSignature ? $"Signature on file • Visit #{_currentVisit.FieldVisitId}" : "No signature yet — capture before completing visit";
                _signaturePreviewBorder.Visibility = _currentVisit != null && _currentVisit.HasSignature ? Visibility.Visible : Visibility.Collapsed;
            }
        }
    }
}


