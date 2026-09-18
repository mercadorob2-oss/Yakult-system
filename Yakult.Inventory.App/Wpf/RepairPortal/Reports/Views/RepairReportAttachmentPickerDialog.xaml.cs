using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Models.RepairPortal;
using Yakult.Inventory.App.WPF.CartridgeManagement.Infrastructure;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Reports.Views
{
    /// <summary>Wraps a RepairReportAttachmentRow with a checkbox-selection flag and, for the batch
    /// report flow, which ticket it came from — same thin-wrapper pattern as RepairReportRow and
    /// RepairTicketTableRow elsewhere in this module.</summary>
    public sealed class AttachmentPickerRow : ViewModelBase
    {
        public RepairReportAttachmentRow Attachment { get; }
        public string SourceLabel { get; }
        public string TicketLabel { get; }

        private bool _isSelected = true;
        public bool IsSelected { get => _isSelected; set => SetField(ref _isSelected, value); }

        public AttachmentPickerRow(RepairReportAttachmentRow attachment, string ticketLabel = null)
        {
            Attachment = attachment;
            SourceLabel = attachment.SourceLabel;
            TicketLabel = ticketLabel;
        }
    }

    /// <summary>Replaces the old blanket "Include attachment images in this report?" Yes/No prompt —
    /// same custom-chrome shape as Pages\Software\SelectedItemsReviewDialog and
    /// Wpf\RepairPortal\Table\Views\RepairTicketSelectionReviewDialog. Everything is checked by
    /// default (same effective outcome as the old "Yes"), so a technician who doesn't care can just
    /// hit Continue; unticking specific rows is how a blurry/irrelevant photo gets excluded.</summary>
    public partial class RepairReportAttachmentPickerDialog : Window
    {
        private readonly ObservableCollection<AttachmentPickerRow> _rows;

        public RepairReportAttachmentPickerDialog(List<AttachmentPickerRow> rows)
        {
            InitializeComponent();

            _rows = new ObservableCollection<AttachmentPickerRow>(rows ?? Enumerable.Empty<AttachmentPickerRow>());
            AttachmentsGrid.ItemsSource = _rows;
            foreach (var row in _rows)
                row.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(AttachmentPickerRow.IsSelected)) RefreshFooter(); };

            // The Ticket column only means anything for the batch/requester-grouped flow, where
            // rows span several tickets — hide it entirely for the single-ticket report.
            TicketColumn.Visibility = _rows.Any(r => !string.IsNullOrEmpty(r.TicketLabel))
                ? Visibility.Visible : Visibility.Collapsed;

            RefreshFooter();
        }

        /// <summary>Read by the caller after ShowDialog() returns — the checked-off subset to embed.</summary>
        public HashSet<RepairReportAttachmentRow> SelectedAttachments =>
            new HashSet<RepairReportAttachmentRow>(_rows.Where(r => r.IsSelected).Select(r => r.Attachment));

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
                DragMove();
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = true;
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelected = true;
            RefreshFooter();
        }

        private void BtnNone_Click(object sender, RoutedEventArgs e)
        {
            foreach (var row in _rows) row.IsSelected = false;
            RefreshFooter();
        }

        private void RefreshFooter()
        {
            var selected = _rows.Count(r => r.IsSelected);
            TxtCount.Text = $"{selected} of {_rows.Count} selected";
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
