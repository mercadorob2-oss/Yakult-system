using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Session;
using Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class VendorCartridgeRefillView : UserControl
    {
        private VendorCartridgeRefillViewModel _vm;
        private MenuItem _menuTransfer;
        private MenuItem _menuRemove;

        public VendorCartridgeRefillView()
        {
            InitializeComponent();

            _vm = new VendorCartridgeRefillViewModel();
            DataContext = _vm;

            SetupAuditContextMenu();

            CmbStatus.SelectedIndex = 0;

            Loaded   += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _ = _vm.LoadDataAsync();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) { }

        // ── Context menu for audit trail ──────────────────────────────────────────
        private void SetupAuditContextMenu()
        {
            var menu = new ContextMenu();

            _menuTransfer = new MenuItem { Header = "⇌  Transfer to Another Batch" };
            _menuTransfer.Click += async (s, e) => await OnMenuTransfer_Click();

            var sep = new Separator();

            _menuRemove = new MenuItem
            {
                Header     = "↩  Remove from Batch (Return to Step 1)",
                Foreground = new SolidColorBrush(
                    (Color)ColorConverter.ConvertFromString("#C00000"))
            };
            _menuRemove.Click += async (s, e) => await OnMenuRemove_Click();

            menu.Items.Add(_menuTransfer);
            menu.Items.Add(sep);
            menu.Items.Add(_menuRemove);
            menu.Opened += (s, e) =>
            {
                bool batchActive = _vm.SelectedBatch?.Status == "Active";
                _menuTransfer.IsEnabled = batchActive;
                _menuRemove.IsEnabled   = batchActive;
            };

            AuditGrid.ContextMenu = menu;

            AuditGrid.PreviewMouseRightButtonDown += (s, e) =>
            {
                var row = FindVisualParent<DataGridRow>(e.OriginalSource as DependencyObject);
                if (row != null)
                    AuditGrid.SelectedItem = row.Item;
            };
        }

        private static T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            while (child != null)
            {
                if (child is T p) return p;
                child = VisualTreeHelper.GetParent(child);
            }
            return null;
        }

        // ── Owner helper ──────────────────────────────────────────────────────────
        private void SetDialogOwner(Window dialog)
        {
            var wpfOwner = Window.GetWindow(this);
            if (wpfOwner != null)
            {
                dialog.Owner = wpfOwner;
            }
            else
            {
                var helper = new System.Windows.Interop.WindowInteropHelper(dialog);
                var wfForm = System.Windows.Forms.Form.ActiveForm;
                if (wfForm != null)
                    helper.Owner = wfForm.Handle;
            }
        }

        // ── Toolbar handlers ──────────────────────────────────────────────────────
        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
            => await _vm.LoadDataAsync();

        private async void BtnSendToVendor_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedBatch != null)
                await _vm.SendToVendorAsync(_vm.SelectedBatch);
        }

        private async void BtnDeleteBatch_Click(object sender, RoutedEventArgs e)
            => await _vm.DeleteBatchAsync();

        private async void BtnEditBatch_Click(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedBatch == null || _vm.SelectedBatch.Status != "Active") return;
            try
            {
                var details = await _vm.GetBatchDetailsAsync(_vm.SelectedBatch.BatchId);
                if (details == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var dialog = new EditBatchWindow(details);
                SetDialogOwner(dialog);

                if (dialog.ShowDialog() == true)
                {
                    if (dialog.VendorChanged)
                    {
                        await _vm.Service.UpdateBatchVendorAsync(details.BatchId, dialog.SelectedVendorId);
                        Logger.LogInfo($"VendorCartridgeRefillView: Updated vendor for batch {details.BatchId}");
                    }
                    await _vm.LoadDataAsync();
                    MessageBox.Show(
                        $"Batch updated successfully!\n\nBatch ID: {details.BatchId}\n" +
                        (dialog.VendorChanged ? "Vendor updated." : "No changes."),
                        "Batch Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else if (dialog.NeedsRefresh)
                {
                    await _vm.LoadDataAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error editing batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillView.BtnEditBatch_Click failed", ex);
            }
        }

        private async void BtnAssignReturns_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new AssignReturnsDialog();
                SetDialogOwner(dialog);
                if (dialog.ShowDialog() == true)
                    await _vm.LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillView.BtnAssignReturns_Click failed", ex);
            }
        }

        private async void BtnCreateBatch_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new CreateBatchDialog();
            SetDialogOwner(dialog);

            if (dialog.ShowDialog() != true || !dialog.Confirmed) return;

            var modelSummary = string.Join(", ", dialog.SelectedModelNames);

            if (MessageBox.Show(
                    $"Create a new refill batch?\n\n" +
                    $"Vendor : {dialog.SelectedVendorName}\n" +
                    $"Models : {modelSummary}\n\n" +
                    "Returns can be linked via 'Assign Returns' after the batch is created.\n\nProceed?",
                    "Confirm Batch Creation",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            var modelGroups = dialog.SelectedModelIds
                .Select((id, i) => new
                {
                    CartridgeModelId = id,
                    CartridgeModel   = dialog.SelectedModelNames[i],
                    TotalQty         = 0
                })
                .ToList();

            try
            {
                _vm.IsLoading = true;

                int newBatchId = await _vm.Service.CreateMultiModelRefillBatchAsync(
                    dialog.SelectedVendorId,
                    modelGroups,
                    AppSession.CurrentUserId,
                    string.IsNullOrEmpty(dialog.Remarks) ? null : dialog.Remarks);

                Logger.LogInfo(
                    $"VendorCartridgeRefillView: Created batch {newBatchId} — " +
                    $"vendor={dialog.SelectedVendorId}, models={dialog.SelectedModelIds.Count}");

                MessageBox.Show(
                    $"Refill batch created successfully!\n\n" +
                    $"Batch ID : {newBatchId}\n" +
                    $"Vendor   : {dialog.SelectedVendorName}\n" +
                    $"Models   : {modelSummary}\n\n" +
                    "Use 'Assign Returns' to link returned cartridges to this batch.",
                    "Batch Created", MessageBoxButton.OK, MessageBoxImage.Information);

                await _vm.LoadDataAsync();
            }
            catch (InvalidOperationException ioe)
            {
                MessageBox.Show(ioe.Message, "Cannot Create Batch",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                Logger.LogError("VendorCartridgeRefillView.BtnCreateBatch_Click: blocked", ioe);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("VendorCartridgeRefillView.BtnCreateBatch_Click failed", ex);
            }
            finally
            {
                _vm.IsLoading = false;
            }
        }

        // ── Filter handlers ───────────────────────────────────────────────────────
        private void CmbStatus_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (CmbStatus.SelectedItem is ComboBoxItem item && _vm != null)
                _vm.StatusFilter = item.Content?.ToString() ?? "All";
        }

        private void BtnClearFilters_Click(object sender, RoutedEventArgs e)
        {
            _vm.ClearFilters();
            CmbStatus.SelectedIndex = 0;
        }

        // ── DataGrid handlers ─────────────────────────────────────────────────────
        private async void BatchesGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var batch = BatchesGrid.SelectedItem as RefillEligibilityResult;
            _vm.SelectedBatch = batch;

            if (batch != null)
                await _vm.LoadAuditTrailAsync(batch.BatchId);
            else
                _vm.AuditTrailItems.Clear();
        }

        private async void BatchActionButton_Click(object sender, RoutedEventArgs e)
        {
            var batch = (sender as Button)?.Tag as RefillEligibilityResult;
            if (batch != null)
                await _vm.HandleRowActionAsync(batch);
        }

        // ── Pagination handlers ───────────────────────────────────────────────────
        private void BtnPrevPage_Click(object sender, RoutedEventArgs e) => _vm.GoToPrevPage();
        private void BtnNextPage_Click(object sender, RoutedEventArgs e) => _vm.GoToNextPage();

        // ── Audit context menu handlers ───────────────────────────────────────────
        private async Task OnMenuRemove_Click()
        {
            var row = AuditGrid.SelectedItem as VendorBatchAuditTrailDto;
            if (row != null)
                await _vm.RemoveFromBatchAsync(row);
        }

        private async Task OnMenuTransfer_Click()
        {
            var row = AuditGrid.SelectedItem as VendorBatchAuditTrailDto;
            if (row == null || _vm.SelectedBatch == null) return;

            List<VendorCartridgeBatchDto> activeBatches;
            try
            {
                activeBatches = await _vm.GetActiveBatchesExceptCurrentAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading batches:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (activeBatches.Count == 0)
            {
                MessageBox.Show(
                    "No other Active refill batches are available to transfer to.\n" +
                    "Create a new batch first via the '+ Create Batch' button.",
                    "No Target Batches", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new TransferBatchDialog(activeBatches, row, _vm.SelectedBatch.BatchId);
            SetDialogOwner(dlg);

            if (dlg.ShowDialog() == true && dlg.SelectedBatch != null)
                await _vm.TransferToBatchAsync(row, dlg.SelectedBatch);
        }
    }
}
