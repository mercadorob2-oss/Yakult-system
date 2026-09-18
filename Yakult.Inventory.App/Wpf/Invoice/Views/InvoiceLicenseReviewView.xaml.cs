using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.WPF.Invoice.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Invoice.Views
{
    public partial class InvoiceLicenseReviewView : UserControl
    {
        private const string DragDataFormat = "Yakult.InvoiceLicenseReviewRows";

        private readonly InvoiceLicenseReviewViewModel _vm;
        private Point _dragStartPoint;
        private bool _dragArmed;

        public InvoiceLicenseReviewView()
        {
            InitializeComponent();

            _vm = new InvoiceLicenseReviewViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
            _vm.RequestPreviewInvoice += OnRequestPreviewInvoice;

            Loaded += async (s, e) => await _vm.LoadAsync();
        }

        /// <summary>True while there are staged classification decisions that have not been
        /// saved to the database yet — checked by the wrapper page / MainForm before navigating
        /// away from or closing over this control.</summary>
        public bool HasUnsavedChanges => _vm.HasUnsavedWork;

        /// <summary>Asks the user (via the same Yes/No dialog style used elsewhere on this page)
        /// whether it's OK to discard pending changes and leave. Returns true if it's safe to
        /// navigate away (no pending changes, or the user confirmed discarding them).</summary>
        public bool ConfirmNavigateAway()
        {
            if (!HasUnsavedChanges) return true;

            return WinForms.MessageBox.Show(
                GetOwner(),
                "You have unsaved license classification changes on this page. Leaving now will discard them.\n\nLeave anyway?",
                "Unsaved Changes",
                WinForms.MessageBoxButtons.YesNo,
                WinForms.MessageBoxIcon.Warning) == WinForms.DialogResult.Yes;
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestPreviewInvoice(InvoiceLicenseReviewRow row)
        {
            if (row == null || row.SetId <= 0) return;
            using (var detail = new Yakult.Inventory.App.Pages.Invoice.ViewInvoiceDetailPage(row.SetId))
            {
                detail.ShowDialog(GetOwner());
            }
        }

        // ── Toolbar / context menu / panel button actions (shared code path with drag-drop) ──

        // Reads the row directly off the clicked Button's DataContext instead of a
        // Command/CommandParameter binding resolved via RelativeSource AncestorType from inside
        // a nested ItemsControl DataTemplate — that binding path did not reliably fire in this
        // WPF-hosted-in-WinForms (ElementHost) app, so the ✕ buttons appeared to do nothing.
        private void UndoPendingRow_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as FrameworkElement)?.DataContext is InvoiceLicenseReviewRow row)
                _vm.UndoPendingDecision(row);
        }

        private void PreviewButton_Click(object sender, RoutedEventArgs e)
        {
            var row = ReviewGrid.SelectedItems.Cast<InvoiceLicenseReviewRow>().FirstOrDefault();
            if (row == null)
            {
                WinForms.MessageBox.Show(GetOwner(), "Select an item first to preview its invoice.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }
            _vm.PreviewInvoice(row);
        }

        private void MarkAsLicensedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var rows = ReviewGrid.SelectedItems.Cast<InvoiceLicenseReviewRow>().ToList();
            if (rows.Count == 0)
            {
                WinForms.MessageBox.Show(GetOwner(), "Select one or more items first.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }
            _vm.MarkRowsAsLicensed(rows);
        }

        private void MarkAsNonLicensedMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var rows = ReviewGrid.SelectedItems.Cast<InvoiceLicenseReviewRow>().ToList();
            if (rows.Count == 0)
            {
                WinForms.MessageBox.Show(GetOwner(), "Select one or more items first.", "No Selection", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }
            _vm.MarkRowsAsNonLicensed(rows);
        }

        // ── Drag start (from the grid) ──────────────────────────────────────
        private void ReviewGrid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(null);
            // Only arm a drag if the press landed on an actual row (not the header / empty area).
            _dragArmed = FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) != null;
        }

        private void ReviewGrid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragArmed || e.LeftButton != MouseButtonState.Pressed) return;
            if (ReviewGrid.SelectedItems.Count == 0) return;

            var current = e.GetPosition(null);
            var diff = _dragStartPoint - current;
            if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
                Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
                return;

            _dragArmed = false;

            var rows = ReviewGrid.SelectedItems.Cast<InvoiceLicenseReviewRow>().ToList();
            if (rows.Count == 0) return;

            var data = new DataObject(DragDataFormat, rows);
            DragDrop.DoDragDrop(ReviewGrid, data, DragDropEffects.Move);
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T match) return match;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        // ── Drop targets ─────────────────────────────────────────────────────
        private void DropPanel_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DragDataFormat))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            e.Effects = DragDropEffects.Move;
            if (sender is Border border) border.Opacity = 0.7;
        }

        private void DropPanel_DragLeave(object sender, DragEventArgs e)
        {
            if (sender is Border border) border.Opacity = 1.0;
        }

        private void NonLicensedDropPanel_Drop(object sender, DragEventArgs e)
        {
            NonLicensedDropPanel.Opacity = 1.0;
            if (!(e.Data.GetData(DragDataFormat) is List<InvoiceLicenseReviewRow> rows)) return;
            _vm.MarkRowsAsNonLicensed(rows);
        }

        private void LicensedDropPanel_Drop(object sender, DragEventArgs e)
        {
            LicensedDropPanel.Opacity = 1.0;
            if (!(e.Data.GetData(DragDataFormat) is List<InvoiceLicenseReviewRow> rows)) return;
            _vm.MarkRowsAsLicensed(rows);
        }
    }

    /// <summary>Tints a review-grid row while a classification decision is staged but not yet
    /// saved — amber for a pending "Licensed" decision, red for a pending "NonLicensed" one.</summary>
    internal sealed class PendingDecisionToBrushConverter : IValueConverter
    {
        private static readonly SolidColorBrush LicensedBrush = new SolidColorBrush(Color.FromRgb(0xEA, 0xF7, 0xEE));
        private static readonly SolidColorBrush NonLicensedBrush = new SolidColorBrush(Color.FromRgb(0xFD, 0xEC, 0xEA));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            switch (value as string)
            {
                case "Licensed": return LicensedBrush;
                case "NonLicensed": return NonLicensedBrush;
                default: return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
