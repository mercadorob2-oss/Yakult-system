using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.WPF.CartridgeManagement.Dialogs
{
    public partial class EditBatchWindow : Window
    {
        public bool VendorChanged    { get; private set; }
        public int  SelectedVendorId { get; private set; }
        public bool NeedsRefresh     { get; private set; }

        private readonly VendorCartridgeBatchDto _batch;
        private readonly List<VendorItem>        _vendors = new List<VendorItem>();
        private readonly bool                    _vendorEditable;

        public EditBatchWindow(VendorCartridgeBatchDto batch)
        {
            _batch           = batch ?? throw new ArgumentNullException(nameof(batch));
            _vendorEditable  = batch.Status == "Active";
            SelectedVendorId = batch.VendorId;

            InitializeComponent();
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            PopulateFields();
            LoadVendors();
            await LoadBatchLinesAsync();
        }

        private void PopulateFields()
        {
            TxtSubtitle.Text    = $"Batch #{_batch.BatchId}";
            TxtBatchId.Text     = _batch.BatchId.ToString();
            TxtOriginalQty.Text = _batch.OriginalQty.ToString();

            TxtStatusBadge.Text    = _batch.Status;
            StatusBadge.Background = StatusToColor(_batch.Status);

            if (!string.IsNullOrWhiteSpace(_batch.Remarks))
            {
                TxtRemarks.Text       = _batch.Remarks;
                PnlRemarks.Visibility = Visibility.Visible;
            }

            if (!_vendorEditable)
            {
                CmbVendor.IsEnabled = false;
                TxtVendorHint.Text  = "Vendor can only be changed for Active batches.";
            }
            else
            {
                TxtVendorHint.Text     = "Select a different vendor to reassign this batch.";
                PnlTransfer.Visibility = Visibility.Visible;
            }
        }

        private static SolidColorBrush StatusToColor(string status)
        {
            switch (status)
            {
                case "Active":        return new SolidColorBrush(Color.FromRgb(0x3A, 0x8E, 0xF6));
                case "SentForRefill": return new SolidColorBrush(Color.FromRgb(0xE0, 0x70, 0x20));
                default:              return new SolidColorBrush(Color.FromRgb(0x6A, 0x7A, 0x8A));
            }
        }

        private void LoadVendors()
        {
            try
            {
                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    con.Open();

                    const string sql = @"
                        SELECT v.VendorID, v.VendorName
                        FROM dbo.Vendor v
                        LEFT JOIN dbo.ArchiveStatus arc
                            ON arc.EntityType = 'Vendor' AND arc.EntityId = v.VendorID AND arc.IsArchived = 1
                        WHERE v.IsActive = 1 AND v.IsRefiller = 1 AND arc.ArchiveId IS NULL
                        ORDER BY v.VendorName";

                    using (var cmd = new SqlCommand(sql, con))
                    using (var rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                            _vendors.Add(new VendorItem { VendorId = rdr.GetInt32(0), VendorName = rdr.GetString(1) });
                    }
                }

                CmbVendor.DisplayMemberPath = "VendorName";
                CmbVendor.ItemsSource       = _vendors;

                int selectIdx = 0;
                for (int i = 0; i < _vendors.Count; i++)
                {
                    if (_vendors[i].VendorId == _batch.VendorId) { selectIdx = i; break; }
                }
                if (_vendors.Count > 0) CmbVendor.SelectedIndex = selectIdx;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load vendors:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("EditBatchWindow.LoadVendors failed", ex);
            }
        }

        private async Task LoadBatchLinesAsync()
        {
            try
            {
                var lines = new List<BatchLineItem>();

                using (var con = new SqlConnection(DatabaseConfig.ConnectionString))
                {
                    await con.OpenAsync();

                    const string sql = @"
                        SELECT bl.BatchLineId, bl.CartridgeModelId,
                               ISNULL(cm.ModelNumber, '[Unknown Model]') AS ModelNumber,
                               bl.SentQty, bl.ReturnedQty
                        FROM dbo.VendorCartridgeBatchLine bl
                        LEFT JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = bl.CartridgeModelId
                        WHERE bl.BatchId = @BatchId
                        ORDER BY cm.ModelNumber";

                    using (var cmd = new SqlCommand(sql, con))
                    {
                        cmd.Parameters.AddWithValue("@BatchId", _batch.BatchId);
                        using (var rdr = await cmd.ExecuteReaderAsync())
                        {
                            while (await rdr.ReadAsync())
                            {
                                lines.Add(new BatchLineItem
                                {
                                    BatchLineId      = rdr.GetInt32(0),
                                    CartridgeModelId = rdr.GetInt32(1),
                                    ModelNumber      = rdr.GetString(2),
                                    SentQty          = rdr.GetInt32(3),
                                    ReturnedQty      = rdr.GetInt32(4)
                                });
                            }
                        }
                    }
                }

                TxtModelsLoading.Visibility = Visibility.Collapsed;

                if (lines.Count == 0)
                {
                    TxtModelsLoading.Text       = "No cartridge models found for this batch.";
                    TxtModelsLoading.Visibility = Visibility.Visible;
                }
                else
                {
                    ModelLinesList.ItemsSource = lines;
                    ModelsBorder.Visibility    = Visibility.Visible;
                    TxtModelCount.Text         = lines.Count.ToString();
                    ModelCountBadge.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                TxtModelsLoading.Text       = "Failed to load cartridge models.";
                TxtModelsLoading.Visibility = Visibility.Visible;
                Logger.LogError("EditBatchWindow.LoadBatchLinesAsync failed", ex);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (_vendorEditable && CmbVendor.SelectedItem == null)
            {
                MessageBox.Show("Please select a vendor.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            VendorChanged = false;

            if (_vendorEditable && CmbVendor.SelectedItem is VendorItem selected)
            {
                if (selected.VendorId != _batch.VendorId)
                {
                    VendorChanged    = true;
                    SelectedVendorId = selected.VendorId;
                }
            }

            if (!VendorChanged)
            {
                MessageBox.Show("No changes detected.", "No Changes",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private async void BtnTransferCartridges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var dlg = new TransferCartridgesWpfDialog(_batch);
                SetDialogOwner(dlg);
                dlg.ShowDialog();
                if (dlg.AnyTransferred)
                {
                    NeedsRefresh = true;
                    await LoadBatchLinesAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Transfer dialog:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Logger.LogError("EditBatchWindow.BtnTransferCartridges_Click failed", ex);
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
                var helper  = new System.Windows.Interop.WindowInteropHelper(dialog);
                var wfForm  = System.Windows.Forms.Form.ActiveForm;
                if (wfForm != null) helper.Owner = wfForm.Handle;
            }
        }

        private class VendorItem
        {
            public int    VendorId   { get; set; }
            public string VendorName { get; set; }
        }

        private class BatchLineItem
        {
            public int    BatchLineId      { get; set; }
            public int    CartridgeModelId { get; set; }
            public string ModelNumber      { get; set; }
            public int    SentQty          { get; set; }
            public int    ReturnedQty      { get; set; }
        }
    }
}
