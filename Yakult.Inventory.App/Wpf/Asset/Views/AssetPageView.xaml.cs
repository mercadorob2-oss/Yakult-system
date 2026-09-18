using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Asset;
using Yakult.Inventory.App.WPF.Asset.ViewModels;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Asset.Views
{
    public partial class AssetPageView : UserControl
    {
        private readonly AssetPageViewModel _vm;

        public AssetPageView()
        {
            InitializeComponent();

            _vm = new AssetPageViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.ConfirmYesNo = (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.YesNo, WinForms.MessageBoxIcon.Question) == WinForms.DialogResult.Yes;
            _vm.RequestAddAsset += OnRequestAddAsset;
            _vm.RequestEditAsset += OnRequestEditAsset;

            RebuildSortByOptions();
            _vm.LoadAssets();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestAddAsset()
        {
            using (var dlg = new AddEditAssetDialog())
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadAssets();
            }
        }

        private void OnRequestEditAsset(AssetDto selected)
        {
            using (var dlg = new AddEditAssetDialog(selected))
            {
                if (dlg.ShowDialog(GetOwner()) == WinForms.DialogResult.OK)
                    _vm.LoadAssets();
            }
        }

        private void SelectAllCheckBox_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            bool newState = ((CheckBox)sender).IsChecked == true;
            _vm.SetPageSelected(newState);
            AssetsGrid.Items.Refresh();
        }

        private void RowSelectCheckBox_Changed(object sender, System.Windows.RoutedEventArgs e)
        {
            if (((CheckBox)sender).DataContext is AssetDto asset)
                _vm.SetAssetSelected(asset.AssetId, ((CheckBox)sender).IsChecked == true);
        }

        private void SelectedBadge_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            var summaries = _vm.GetSelectedAssets().Select(a => new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowSummary
            {
                Id = a.AssetId,
                Values = new[] { a.AssetId.ToString(), a.ModelNumber, a.SerialNumber, a.VendorName }
            });

            var dialog = new Yakult.Inventory.App.WPF.Shared.Controls.SelectedRowsReviewDialog(
                "Selected Assets", "assets",
                new[] { "ID", "Model Number", "Serial Number", "Vendor" },
                summaries);
            SetWpfOwner(dialog);
            dialog.ShowDialog();

            foreach (var assetId in dialog.RemovedIds)
                _vm.SetAssetSelected(assetId, false);

            AssetsGrid.Items.Refresh();
        }

        private static void SetWpfOwner(System.Windows.Window window)
        {
            var owner = GetOwner();
            if (owner != null) new System.Windows.Interop.WindowInteropHelper(window).Owner = owner.Handle;
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(AssetsGrid, isBooleanColumn: c => c.Header as string == "Active");
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(AssetsGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        // Mirrors DgvAssets_ColumnHeaderMouseClick: toggle direction is driven by the clicked
        // column's own current sort indicator, and this path never touches Filter-By (unlike
        // the Sort-By dropdown, which resets Filter-By to Default — see AssetPageViewModel).
        private void AssetsGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in AssetsGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
