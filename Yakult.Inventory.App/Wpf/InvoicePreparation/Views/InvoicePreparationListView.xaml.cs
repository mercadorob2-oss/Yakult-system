using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Software;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.WPF.InvoicePreparation.ViewModels;
using WinForms = System.Windows.Forms;

namespace Yakult.Inventory.App.WPF.InvoicePreparation.Views
{
    public partial class InvoicePreparationListView : UserControl
    {
        private readonly InvoicePreparationListViewModel _vm;
        private readonly InvoicePreparationRepository _repository = new InvoicePreparationRepository();

        public InvoicePreparationListView()
        {
            InitializeComponent();

            _vm = new InvoicePreparationListViewModel();
            DataContext = _vm;

            _vm.RequestInfo += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
            _vm.RequestWarning += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
            _vm.RequestError += (title, msg) => WinForms.MessageBox.Show(GetOwner(), msg, title, WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Error);
            _vm.RequestPreview += OnRequestPreview;
            _vm.RequestCreateInvoice += OnRequestCreateInvoice;

            _vm.LoadGroups();
        }

        private static WinForms.IWin32Window GetOwner() => WinForms.Form.ActiveForm;

        private void OnRequestPreview(int preparationId)
        {
            using (var preview = new InvoicePreparationDetailWindow(preparationId))
            {
                preview.ShowDialog(GetOwner());
            }
        }

        private void OnRequestCreateInvoice(List<int> preparationIds)
        {
            List<InvoicePreparationItemDto> items;
            try
            {
                items = _repository.GetItemsForGroups(preparationIds);
            }
            catch (System.Exception ex)
            {
                WinForms.MessageBox.Show(GetOwner(), ex.Message, "Cannot Create Invoice", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Warning);
                return;
            }

            if (items.Count == 0)
            {
                WinForms.MessageBox.Show(GetOwner(), "The selected group(s) have no items.", "No Items", WinForms.MessageBoxButtons.OK, WinForms.MessageBoxIcon.Information);
                return;
            }

            using (var dialog = new SoftwareServiceSetDialog())
            {
                dialog.PrefillItems(items);
                var result = dialog.ShowDialog(GetOwner());
                if (result != WinForms.DialogResult.OK || !dialog.CreatedSetId.HasValue) return;

                _vm.MarkGroupsInvoiced(preparationIds, dialog.CreatedSetId.Value);
            }
        }

        // Switching tabs clears every checkbox — otherwise a group checked on one tab
        // stays checked (but invisible, since it's a different DataGrid) when the other
        // tab is shown, which reads as a stuck/bugged checkbox to the user.
        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source != sender) return; // ignore bubbled selection events from inner controls (e.g. the DataGrids)
            _vm.ClearAllChecked();
        }

        private void GroupsGrid_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (((DataGrid)sender).SelectedItem is InvoicePreparationSummaryRow row)
                _vm.PreviewGroup(row.PreparationId);
        }

        // A DataGridTemplateColumn CheckBox's TwoWay IsChecked binding does not reliably
        // commit back to the row when clicked (see XAML comment) — read the toggled state
        // explicitly and write it back manually.
        private void RowCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox cb && cb.DataContext is InvoicePreparationSummaryRow row)
            {
                row.Selected = cb.IsChecked == true;
                _vm.NotifySelectionChanged();
            }
        }

        private void NotCompletedSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = _vm.NotCompletedSelectAllState != true;
            _vm.ToggleSelectAllNotCompleted(newState);
        }

        private void CompletedSelectAllCheckBox_Click(object sender, RoutedEventArgs e)
        {
            bool newState = _vm.CompletedSelectAllState != true;
            _vm.ToggleSelectAllCompleted(newState);
        }
    }
}
