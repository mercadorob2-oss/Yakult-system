using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Yakult.Inventory.App.Models.RepairPortal;

namespace Yakult.Inventory.App.Wpf.RepairPortal.Table.Views
{
    /// <summary>Review-and-unselect popup for Table View's "N selected" text — same shape as
    /// Pages\Software\SelectedItemsReviewDialog (custom chrome, per-row Remove + Remove All +
    /// Close), just with the Repair Table's own columns instead of that dialog's Items columns.
    /// The caller (RepairTicketTableView) reads RemovedTicketIds after ShowDialog() returns and
    /// unchecks exactly those rows — this dialog never touches the underlying checkbox state
    /// itself, it only reports which tickets the technician chose to drop from the selection.</summary>
    public partial class RepairTicketSelectionReviewDialog : Window
    {
        private readonly ObservableCollection<RepairTicketTableRow> _rows;

        public List<int> RemovedTicketIds { get; } = new List<int>();

        public RepairTicketSelectionReviewDialog(IEnumerable<RepairTicketListItem> tickets)
        {
            InitializeComponent();

            _rows = new ObservableCollection<RepairTicketTableRow>(
                (tickets ?? Enumerable.Empty<RepairTicketListItem>()).Select(t => new RepairTicketTableRow(t)));
            TicketsGrid.ItemsSource = _rows;
            RefreshFooter();
            SizeToRowCount();
        }

        // The DataGrid's row previously always stretched to fill the window's full 720px, leaving
        // a large empty gray gap below the grid whenever fewer than ~15 tickets were selected —
        // this sizes the window to the actual row count instead (still clamped to a sane range,
        // and still user-resizable via ResizeMode="CanResizeWithGrip" if more room is wanted).
        private void SizeToRowCount()
        {
            const double HeaderRowHeight = 52;
            const double FooterRowHeight = 62;
            const double CardChromeHeight = 48; // Card Margin(12+12) + Padding(12+12)
            const double GridColumnHeaderHeight = 36;
            const double GridRowHeight = 36;
            const double Buffer = 16;

            var contentHeight = _rows.Count * GridRowHeight + GridColumnHeaderHeight;
            var desired = HeaderRowHeight + FooterRowHeight + CardChromeHeight + contentHeight + Buffer;

            Height = Math.Max(MinHeight, Math.Min(desired, 720));
        }

        private void OnHeaderDrag(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;

            // A maximized custom-chrome window has no OS-drawn title bar to drag back down from —
            // restoring first (at the pointer's relative position) then starting DragMove keeps the
            // familiar "drag a maximized window to unsnap it" behavior working here too.
            if (WindowState == WindowState.Maximized && e.ClickCount == 1)
            {
                var pointerRatio = e.GetPosition(this).X / ActualWidth;
                WindowState = WindowState.Normal;
                Left = e.GetPosition(null).X - (RestoreBounds.Width * pointerRatio);
                Top = 0;
            }

            if (e.ClickCount == 2) ToggleMaximize();
            else DragMove();
        }

        private void OnMinimizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            Yakult.Inventory.App.Helpers.ModalMinimizeGuard.Minimize(this);
        }

        private void OnMaximizeClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            ToggleMaximize();
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            MaximizeGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            MaximizeGlyph.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }

        private void OnCloseClick(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            DialogResult = true;
        }

        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null) return;

            int ticketId = (int)btn.Tag;
            var row = _rows.FirstOrDefault(r => r.Ticket.RepairTicketId == ticketId);
            if (row == null) return;

            _rows.Remove(row);
            RemovedTicketIds.Add(ticketId);
            RefreshFooter();
        }

        private void BtnRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            RemovedTicketIds.AddRange(_rows.Select(r => r.Ticket.RepairTicketId));
            _rows.Clear();
            RefreshFooter();
        }

        // Bulk counterpart to the per-row Remove button — drops every checked row in one go so the
        // technician doesn't have to click Remove once per ticket when unselecting several at once.
        private void BtnRemoveSelected_Click(object sender, RoutedEventArgs e)
        {
            var selected = _rows.Where(r => r.IsSelected).ToList();
            if (selected.Count == 0) return;

            foreach (var row in selected)
            {
                _rows.Remove(row);
                RemovedTicketIds.Add(row.Ticket.RepairTicketId);
            }

            if (SelectAllCheckBox.IsChecked == true) SelectAllCheckBox.IsChecked = false;
            RefreshFooter();
        }

        private void SelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool select = SelectAllCheckBox.IsChecked == true;
            foreach (var row in _rows) row.IsSelected = select;
        }

        private void RefreshFooter()
        {
            TxtCount.Text = _rows.Count > 0
                ? $"{_rows.Count} ticket{(_rows.Count == 1 ? "" : "s")} selected"
                : "No tickets selected.";
            TxtEmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnRemoveAll.IsEnabled = _rows.Count > 0;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
