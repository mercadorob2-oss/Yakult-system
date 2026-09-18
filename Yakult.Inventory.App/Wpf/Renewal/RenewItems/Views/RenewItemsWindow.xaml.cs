using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Item;
using Yakult.Inventory.App.Wpf.Receipt;
using Yakult.Inventory.App.WPF.Renewal.RenewItems.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.Renewal.RenewItems.Views
{
    public partial class RenewItemsWindow : Window
    {
        private readonly RenewItemsViewModel _vm;

        public RenewItemsWindow(
            int originalSetId,
            string title,
            List<SetItemRenewalDto> items,
            List<ItemCatalogDto> catalog,
            decimal baseSubtotal, decimal baseVatPct, decimal baseWhtPct,
            decimal baseDiscount, decimal originalTotal,
            string originalSetCode = null)
        {
            InitializeComponent();

            _vm = new RenewItemsViewModel(originalSetId, title, items, catalog, baseSubtotal, baseVatPct, baseWhtPct, baseDiscount, originalTotal, originalSetCode);
            _vm.CloseRequested += success =>
            {
                DialogResult = success;
                Close();
            };
            _vm.RequestWarning += (t, m) => MessageBox.Show(this, m, t, MessageBoxButton.OK, MessageBoxImage.Warning);
            _vm.RequestInfo += (t, m) => MessageBox.Show(this, m, t, MessageBoxButton.OK, MessageBoxImage.Information);
            _vm.RequestError += (t, m) => MessageBox.Show(this, m, t, MessageBoxButton.OK, MessageBoxImage.Error);
            _vm.ConfirmYesNo = (t, m) => MessageBox.Show(this, m, t, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;
            // Pure WPF-to-WPF — no WinForms Form owner needed, unlike the WinForms callers of
            // ReceiptSetViewerLauncher.ShowForSet elsewhere in the app.
            _vm.RequestAttachReceipt += setId =>
            {
                var receiptWindow = new ReceiptSetViewerWindow(setId) { Owner = this };
                receiptWindow.ShowDialog();
            };
            // "Add Item" inside the Add-Row popup: the item doesn't exist in the catalog at
            // all yet, so create it via the normal item-creation dialog first, then refresh
            // the catalog so it's immediately searchable/selectable back in this window.
            _vm.RequestAddNewCatalogItem += () =>
            {
                var batchDialog = new BatchAddItemDialog { Owner = this };
                if (batchDialog.ShowDialog() == WinForms.DialogResult.OK)
                    _vm.RefreshCatalogAfterNewItem();
            };

            DataContext = _vm;
            ConfigureItemsGrouping();
        }

        /// <summary>Groups the items grid by Sub-Type Group (same key format as
        /// ViewInvoiceDetailPage's SubTypeGroupKeyConverter — see RenewItemRow.GroupDisplay),
        /// then by Parent Tag Group nested underneath (RenewItemRow.ParentTagGroupDisplay), with
        /// live re-grouping so a row moves to its new header(s) the moment "Apply to Checked"
        /// in the merged panel changes its SubType/ReferenceCode/ParentTag.</summary>
        private void ConfigureItemsGrouping()
        {
            var view = CollectionViewSource.GetDefaultView(_vm.Rows);
            view.GroupDescriptions.Clear();
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RenewItemRow.GroupDisplay)));
            view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RenewItemRow.ParentTagGroupDisplay)));

            if (view is ICollectionViewLiveShaping liveShaping && liveShaping.CanChangeLiveGrouping)
            {
                liveShaping.LiveGroupingProperties.Add(nameof(RenewItemRow.GroupDisplay));
                liveShaping.LiveGroupingProperties.Add(nameof(RenewItemRow.ParentTagGroupDisplay));
                liveShaping.IsLiveGrouping = true;
            }
        }

        /// <summary>Recurses through nested CollectionViewGroups to the leaf RenewItemRow items —
        /// with Parent Tag Group nested under Sub-Type Group, group.Items at the outer level
        /// contains child CollectionViewGroup objects, not rows directly.</summary>
        private static IEnumerable<RenewItemRow> LeafRows(CollectionViewGroup group)
        {
            foreach (var item in group.Items)
            {
                if (item is CollectionViewGroup childGroup)
                {
                    foreach (var row in LeafRows(childGroup))
                        yield return row;
                }
                else if (item is RenewItemRow row)
                {
                    yield return row;
                }
            }
        }

        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is RenewItemRow row)
                row.Selected = cb.IsChecked == true;
        }

        // Reflects the group's current mix of checked rows each time its header is generated
        // (e.g. on first render, or after live re-grouping moves a row here). Three-state only
        // for this initial read — user clicks always resolve to plain checked/unchecked, never
        // back to indeterminate, so the group can't get "stuck" showing a mixed state you can't
        // click your way out of.
        private void GroupHeaderCheckBox_Loaded(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb) || !(cb.DataContext is CollectionViewGroup group)) return;

            var rows = LeafRows(group).ToList();
            if (rows.Count == 0) return;

            bool allSelected = rows.All(r => r.Selected);
            bool noneSelected = rows.All(r => !r.Selected);

            cb.IsThreeState = !allSelected && !noneSelected;
            cb.IsChecked = allSelected ? true : noneSelected ? (bool?)false : null;
        }

        private void GroupHeaderCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is CheckBox cb) || !(cb.DataContext is CollectionViewGroup group)) return;

            bool newState = cb.IsChecked == true;
            cb.IsThreeState = false;
            cb.IsChecked = newState;

            foreach (var row in LeafRows(group))
                row.Selected = newState;
        }
    }
}
