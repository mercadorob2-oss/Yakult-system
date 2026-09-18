using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.Pages.Software
{
    public class SelectedItemSummary
    {
        public int    ItemId       { get; set; }
        public string Name         { get; set; }
        public string Category     { get; set; }
        public string SerialNumber { get; set; }
    }

    public partial class SelectedItemsReviewDialog : Window, IDisposable
    {
        private readonly ObservableCollection<SelectedItemSummary> _rows;

        public List<int> RemovedItemIds { get; } = new List<int>();

        public SelectedItemsReviewDialog(IEnumerable<SelectedItemSummary> items)
        {
            InitializeComponent();

            _rows = new ObservableCollection<SelectedItemSummary>(items ?? Enumerable.Empty<SelectedItemSummary>());
            ItemsGrid.ItemsSource = _rows;
            RefreshFooter();
        }

        // ── WinForms-compatible ShowDialog overloads ─────────────────────────
        public new WinForms.DialogResult ShowDialog()
        {
            bool? result = base.ShowDialog();
            return result == true ? WinForms.DialogResult.OK : WinForms.DialogResult.Cancel;
        }

        public WinForms.DialogResult ShowDialog(WinForms.IWin32Window owner)
        {
            if (owner != null)
                new System.Windows.Interop.WindowInteropHelper(this).Owner = owner.Handle;
            return ShowDialog();
        }

        public void Dispose() { }

        // ── Header chrome ─────────────────────────────────────────────────────
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

        // ── Row removal ──────────────────────────────────────────────────────
        private void BtnRemoveRow_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.Tag == null) return;

            int itemId = (int)btn.Tag;
            var row = _rows.FirstOrDefault(r => r.ItemId == itemId);
            if (row == null) return;

            _rows.Remove(row);
            RemovedItemIds.Add(itemId);
            RefreshFooter();
        }

        private void BtnRemoveAll_Click(object sender, RoutedEventArgs e)
        {
            RemovedItemIds.AddRange(_rows.Select(r => r.ItemId));
            _rows.Clear();
            RefreshFooter();
        }

        private void RefreshFooter()
        {
            TxtCount.Text = _rows.Count > 0
                ? $"{_rows.Count} item{(_rows.Count == 1 ? "" : "s")} selected"
                : "No items selected.";
            TxtEmptyState.Visibility = _rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            BtnRemoveAll.IsEnabled = _rows.Count > 0;
        }

        // ── Close ─────────────────────────────────────────────────────────────
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
