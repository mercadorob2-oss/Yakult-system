using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Yakult.Inventory.App.WPF.Renewal.RenewalWorkspace.Views;
using Yakult.Inventory.App.WPF.Shared.Helpers;
using Yakult.Inventory.App.WPF.Warranty.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Warranty.Views
{
    public partial class WarrantyPageView : UserControl
    {
        private readonly WarrantyPageViewModel _vm;

        public WarrantyPageView()
        {
            InitializeComponent();

            _vm = new WarrantyPageViewModel();
            DataContext = _vm;

            _vm.ErrorOccurred += OnErrorOccurred;
            _vm.RequestOpenRenewalDetail += OnRequestOpenRenewalDetail;

            RebuildSortByOptions();
            _ = _vm.LoadWarrantyItemsAsync();
        }

        public void ApplyInitialSearch(string query) => _vm.ApplyInitialSearch(query);

        private void OnErrorOccurred(string message)
        {
            var owner = WinForms.Form.ActiveForm;
            WinForms.MessageBox.Show(owner, $"Error loading warranty items:\n\n{message}", "Error",
                WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
        }

        /// <summary>Opens the Renewal Workspace when the user double-clicks a warranty item —
        /// this creates the first renewal record for items that need renewal tracking. Ported
        /// verbatim from GridWarranty_CellDoubleClick, including the still-unimplemented
        /// OnRenewalCreated navigation TODO from the original.</summary>
        private void OnRequestOpenRenewalDetail(int itemId)
        {
            try
            {
                var renewalDetailWindow = new RenewalWorkspaceWindow(itemId, isItemIdConstructor: true);
                SetDialogOwner(renewalDetailWindow);

                renewalDetailWindow.OnRenewalCreated = () =>
                {
                    WinForms.MessageBox.Show(
                        "Renewal created successfully!\n\n" +
                        "The item will now appear in the Renewals page for future renewal management.",
                        "Renewal Created",
                        WinForms.MessageBoxButtons.OK,
                        WinForms.MessageBoxIcon.Information);

                    // TODO: Add actual navigation to ViewRenewalsPage via MainForm callback if needed
                };

                renewalDetailWindow.ShowDialog();
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show($"Error opening renewal details:\n\n{ex.Message}", "Error",
                    WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            }
        }

        private void SetDialogOwner(Window dialog)
        {
            var wpfOwner = Window.GetWindow(this);
            if (wpfOwner != null)
            {
                dialog.Owner = wpfOwner;
            }
            else
            {
                var helper = new WindowInteropHelper(dialog);
                var wfForm = WinForms.Form.ActiveForm;
                if (wfForm != null)
                    helper.Owner = wfForm.Handle;
            }
        }

        private void WarrantyGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (WarrantyGrid.SelectedItem is WarrantyItemDto selected)
                _vm.RaiseOpenRenewalDetail(selected.ItemId);
        }

        private void RebuildSortByOptions()
        {
            var options = ListSortHelper.BuildOptions(WarrantyGrid);
            var defaultKey = ListSortHelper.ResolveDefaultSortColumnKey(WarrantyGrid);
            _vm.SetSortByOptions(options, defaultKey);
        }

        // Mirrors the original GridWarranty_ColumnHeaderMouseClick: the toggle direction is
        // driven by the clicked column's OWN current sort indicator (not a separately stored
        // "last sorted column"), so a column showing no indicator always starts Ascending, and
        // clicking the currently-Ascending column flips it to Descending (and vice versa).
        private void WarrantyGrid_Sorting(object sender, DataGridSortingEventArgs e)
        {
            e.Handled = true;
            var key = ListSortHelper.SortKey(e.Column);
            if (string.IsNullOrEmpty(key)) return;

            var direction = e.Column.SortDirection == ListSortDirection.Ascending
                ? ListSortDirection.Descending
                : ListSortDirection.Ascending;

            _vm.SortByColumn(key, direction);

            foreach (var col in WarrantyGrid.Columns) col.SortDirection = null;
            e.Column.SortDirection = direction;
        }
    }
}
