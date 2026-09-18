using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.ItemAudit;
using Yakult.Inventory.App.WPF.ItemMovementAudit.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.ItemMovementAudit.Views
{
    public partial class ItemMovementAuditPageView : UserControl
    {
        private readonly ItemMovementAuditPageViewModel _vm;

        public ItemMovementAuditPageView(bool autoLoad = true)
        {
            InitializeComponent();

            _vm = new ItemMovementAuditPageViewModel(autoLoad);
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestViewDetails += OnRequestViewDetails;
            _vm.RebuildSortByOptionsRequested += RebuildSortByOptions;

            RebuildSortByOptions();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);
        public void NotifyClosed() => _vm.MarkDisposed();
        public System.Threading.Tasks.Task LoadTimelineForSerialAsync(string serial) => _vm.LoadTimelineForSerialAsync(serial);

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(ItemsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(ItemsGrid);
            _vm.SetSortByOptionsOnce(options, defaultKey);
        }

        private void ItemsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == System.ComponentModel.ListSortDirection.Ascending
                ? System.ComponentModel.ListSortDirection.Descending
                : System.ComponentModel.ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in ItemsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }

        private void OnRequestViewDetails(ItemMovementAuditDto row)
        {
            if (row == null)
            {
                WinForms.MessageBox.Show(GetOwner(), "Please select a row to view details.", "No Selection",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            OpenDetailsDialog(row);
        }

        private void ItemsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (ItemsGrid.SelectedItem is ItemMovementAuditDto row)
                OpenDetailsDialog(row);
        }

        private void OpenDetailsDialog(ItemMovementAuditDto row)
        {
            var dlg = new ItemMovementAuditDetailsWindow(row, async serial => await _vm.LoadTimelineForSerialAsync(serial));
            SetWpfOwner(dlg);
            dlg.ShowDialog();
        }

        private static void SetWpfOwner(Window window)
        {
            var owner = WinForms.Form.ActiveForm;
            if (owner != null)
                new WindowInteropHelper(window).Owner = owner.Handle;
        }
    }
}
