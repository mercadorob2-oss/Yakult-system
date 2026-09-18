using System;
using System.Data;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages.Cartridge;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs;
using Yakult.Inventory.App.WPF.CartridgeManagement.ViewModels;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Views
{
    public partial class OutboundBatchesView : UserControl
    {
        private OutboundBatchesViewModel _vm;

        public OutboundBatchesView()
        {
            InitializeComponent();
            _vm         = new OutboundBatchesViewModel();
            DataContext = _vm;
            Loaded     += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
            => _ = _vm.LoadAsync();

        private void SetDialogOwner(Window dialog)
        {
            var wpfOwner = Window.GetWindow(this);
            if (wpfOwner != null)
            {
                dialog.Owner = wpfOwner;
            }
            else
            {
                var helper  = new WindowInteropHelper(dialog);
                var wfForm  = System.Windows.Forms.Form.ActiveForm;
                if (wfForm != null)
                    helper.Owner = wfForm.Handle;
            }
        }

        // ── Toolbar handlers ─────────────────────────────────────────────────

        private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error refreshing data:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchesView.BtnRefresh_Click failed", ex);
            }
        }

        private async void BtnNewBatch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new OutboundBatchAssignmentWpfDialog(nonRefillableOnly: false);
                SetDialogOwner(dialog);
                if (dialog.ShowDialog() == true)
                    await _vm.LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Unexpected error:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchesView.BtnNewBatch_Click failed", ex);
            }
        }

        private async void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var batch = _vm.SelectedBatch;
            if (batch == null) return;

            int cartridgeCount = _vm.AuditTrailCount;
            string cartridgeNote = cartridgeCount > 0
                ? $"\n\n  • {cartridgeCount} assigned cartridge(s) will be unassigned and returned to Pending."
                : "\n\n  • Batch has no assigned cartridges.";

            string confirm =
                $"Delete Batch #{batch.BatchId} ({batch.BatchPurpose})?\n\n" +
                $"  • Vendor: {batch.VendorName}\n" +
                $"  • Models: {(string.IsNullOrEmpty(batch.ModelSummary) ? "(none)" : batch.ModelSummary)}" +
                cartridgeNote + "\n\nThis action cannot be undone.";

            if (MessageBox.Show(confirm, $"Confirm Delete Batch #{batch.BatchId}",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            try
            {
                await _vm.DeleteAsync(batch);
                MessageBox.Show($"Batch #{batch.BatchId} deleted successfully.",
                    "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchesView.BtnDelete_Click failed", ex);
            }
        }

        private void BtnReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                OpenDisposedSoldReport();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening report:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchesView.BtnReport_Click failed", ex);
            }
        }

        // ── Finalize row button ──────────────────────────────────────────────

        private async void BtnFinalize_Click(object sender, RoutedEventArgs e)
        {
            if (!(((Button)sender).Tag is OutboundBatchDto batch)) return;

            string action = batch.BatchPurpose == "DISPOSE" ? "Disposed" : "Sold";

            string confirm =
                $"Mark Batch #{batch.BatchId} as {action}?\n\n" +
                $"  • Vendor: {batch.VendorName}\n" +
                $"  • Models: {(string.IsNullOrEmpty(batch.ModelSummary) ? "(see batch lines)" : batch.ModelSummary)}\n" +
                $"  • Total units: {batch.TotalQty}\n\n" +
                "This will update all linked cartridges and cannot be undone.";

            if (MessageBox.Show(confirm, $"Confirm Finalize Batch #{batch.BatchId}",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                await _vm.FinalizeAsync(batch);
                MessageBox.Show(
                    $"Batch #{batch.BatchId} finalized — all linked cartridges marked as {action}.",
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("no linked empty cartridges"))
            {
                var answer = MessageBox.Show(
                    $"Batch #{batch.BatchId} has no assigned cartridges and cannot be finalized.\n\n" +
                    "Would you like to delete this empty batch instead?",
                    $"Batch #{batch.BatchId} Is Empty",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (answer == MessageBoxResult.Yes)
                {
                    try
                    {
                        await _vm.DeleteAsync(batch);
                        MessageBox.Show($"Batch #{batch.BatchId} deleted.",
                            "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    catch (Exception dex)
                    {
                        MessageBox.Show($"Error deleting batch:\n{dex.Message}", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        Logger.LogError("OutboundBatchesView.BtnFinalize_Click (delete path) failed", dex);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error finalizing batch:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("OutboundBatchesView.BtnFinalize_Click failed", ex);
            }
        }

        // ── Dispose/Sold report (delegates to WinForms ReportLauncher) ───────

        private void OpenDisposedSoldReport()
        {
            using (var dlg = new DisposedSoldReportFilterDialog())
            {
                if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;

                string typeFilter = dlg.SelectedFilter == "All" ? null : dlg.SelectedFilter;

                var repo = new DisposedSoldReportRepository();
                var data = repo.GetReport(from: dlg.FromDate, to: dlg.ToDate, decisionType: typeFilter);

                if (data == null || data.Count == 0)
                {
                    MessageBox.Show("No records found for the selected filters.", "No Data",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var sigData = ReportLauncher.ShowSignatoryPicker();
                if (sigData == null) return;

                var colParams = ReportLauncher.ShowColumnSelection(
                    "Outbound Cartridges Report",
                    ReportColumnSelectionDialog.CartridgeDisposeSoldReportColumns());
                if (colParams == null) return;

                var dt = BuildDisposedSoldDataTable(data);

                using (var frm = ReportLauncher.CreateCartridgeDisposeSoldReportForm(dt, sigData, colParams))
                    frm?.ShowDialog();
            }
        }

        private static DataTable BuildDisposedSoldDataTable(System.Collections.Generic.List<DisposedSoldItemDto> rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("DecisionId",          typeof(int));
            dt.Columns.Add("BatchId",             typeof(int));
            dt.Columns.Add("DecidedAt",            typeof(DateTime));
            dt.Columns.Add("DecisionTypeName",     typeof(string));
            dt.Columns.Add("Quantity",             typeof(int));
            dt.Columns.Add("RecipientName",        typeof(string));
            dt.Columns.Add("SaleAmount",           typeof(decimal));
            dt.Columns.Add("Remarks",              typeof(string));
            dt.Columns.Add("ItemName",             typeof(string));
            dt.Columns.Add("ItemModelNumber",      typeof(string));
            dt.Columns.Add("CategoryName",         typeof(string));
            dt.Columns.Add("ConditionName",        typeof(string));
            dt.Columns.Add("DecidedByName",        typeof(string));
            dt.Columns.Add("CartridgeModelNumber", typeof(string));
            dt.Columns.Add("CartridgeBrand",       typeof(string));
            dt.Columns.Add("DisposalCompanyName",  typeof(string));
            dt.Columns.Add("VendorName",           typeof(string));

            foreach (var r in rows)
            {
                dt.Rows.Add(
                    r.DecisionId,
                    r.BatchId.HasValue      ? (object)r.BatchId.Value      : DBNull.Value,
                    r.DecidedAt,
                    r.DecisionTypeName      ?? (object)DBNull.Value,
                    r.Quantity,
                    r.RecipientName         ?? (object)DBNull.Value,
                    r.SaleAmount.HasValue   ? (object)r.SaleAmount.Value   : DBNull.Value,
                    r.Remarks               ?? (object)DBNull.Value,
                    r.ItemName              ?? (object)DBNull.Value,
                    r.ItemModelNumber       ?? (object)DBNull.Value,
                    r.CategoryName          ?? (object)DBNull.Value,
                    r.ConditionName         ?? (object)DBNull.Value,
                    r.DecidedByName         ?? (object)DBNull.Value,
                    r.CartridgeModelNumber  ?? (object)DBNull.Value,
                    r.CartridgeBrand        ?? (object)DBNull.Value,
                    r.DisposalCompanyName   ?? (object)DBNull.Value,
                    r.VendorName            ?? (object)DBNull.Value);
            }

            return dt;
        }
    }
}
