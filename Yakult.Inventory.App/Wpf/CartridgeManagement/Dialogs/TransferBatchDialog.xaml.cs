using System.Collections.Generic;
using System.Windows;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class TransferBatchDialog : Window
    {
        public VendorCartridgeBatchDto SelectedBatch { get; private set; }

        private readonly List<VendorCartridgeBatchDto> _batches;

        public TransferBatchDialog(
            List<VendorCartridgeBatchDto> batches,
            VendorBatchAuditTrailDto      selectedRow,
            int                           currentBatchId)
        {
            _batches = batches;
            InitializeComponent();

            TxtTransferContext.Text = $"{selectedRow.CartridgeModel}  ×  {selectedRow.ReturnedQty} unit(s)";
            TxtFromBatch.Text       = $"From Batch #{currentBatchId}  —  select a target batch below";

            foreach (var b in batches)
                CmbTargetBatch.Items.Add(
                    $"Batch #{b.BatchId}  —  {b.VendorName}  |  {b.CartridgeModel}  ({b.ReturnedQty} units)");

            if (CmbTargetBatch.Items.Count > 0)
                CmbTargetBatch.SelectedIndex = 0;
        }

        private void BtnTransfer_Click(object sender, RoutedEventArgs e)
        {
            if (CmbTargetBatch.SelectedIndex < 0)
            {
                MessageBox.Show("Please select a target batch.", "Required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            SelectedBatch = _batches[CmbTargetBatch.SelectedIndex];
            DialogResult  = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
