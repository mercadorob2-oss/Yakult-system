using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Pages.Vendor;
using Yakult.Inventory.App.WPF.Vendor.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Vendor.Views
{
    public partial class VendorPageView : UserControl
    {
        private readonly VendorPageViewModel _vm;

        public VendorPageView()
        {
            InitializeComponent();

            _vm = new VendorPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddVendor += OnRequestAddVendor;
            _vm.RequestEditVendor += OnRequestEditVendor;

            RebuildSortByOptions();
            _ = _vm.LoadVendorsAsync();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddVendor()
        {
            using (var dlg = new AddVendorDialog())
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _ = _vm.LoadVendorsAsync();
            }
        }

        private void OnRequestEditVendor(VendorDto selected)
        {
            using (var dlg = new EditVendorDialog(selected))
            {
                if (dlg.ShowDialog() == WinForms.DialogResult.OK)
                    _ = _vm.LoadVendorsAsync();
            }
        }

        private void SelectAllCheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            VendorsGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is VendorDto vendor)
                _vm.SetVendorSelected(vendor.VendorId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedVendors().Select(v => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = v.VendorId,
                Values = new[] { v.VendorId.ToString(), v.VendorName, v.TIN, v.Address }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Vendors", "vendors",
                new[] { "ID", "Vendor Name", "TIN", "Address" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var vendorId in dialog.RemovedIds)
                _vm.SetVendorSelected(vendorId, false);

            VendorsGrid.Items.Refresh();
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(VendorsGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(VendorsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        // Mirrors DgvVendors_ColumnHeaderMouseClick: toggle direction is driven by the clicked
        // column's own current sort indicator, and this path never touches Filter-By (unlike
        // the Sort-By dropdown, which resets Filter-By to Default — see VendorPageViewModel).
        private void VendorsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in VendorsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
