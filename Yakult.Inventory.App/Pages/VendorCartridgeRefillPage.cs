using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages
{
    /// <summary>
    /// Vendor Cartridge Refill Page - Batch-level vendor refill tracking
    /// This page is strictly for vendor refill batching and dispatch
    /// Does NOT show individual cartridges or affect issuing operations
    /// </summary>
    public partial class VendorCartridgeRefillPage : UserControl
    {
        private DataGridView dgvBatches;
        private Button btnRefresh;
        private Button btnSendToVendor;

        private readonly CartridgeRefillService _refillService;
        private List<RefillEligibilityResult> _batches;

        public VendorCartridgeRefillPage()
        {
            _refillService = new CartridgeRefillService();
            _batches = new List<RefillEligibilityResult>();
            BuildUi();
            _ = LoadBatchesAsync();
        }

        private void BuildUi()
        {
            // Set UserControl properties
            this.Dock = DockStyle.Fill;
            this.BackColor = Color.White;

            // 1) Header Panel - add to this.Controls FIRST (will dock at top)
            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 60,
                BackColor = Color.FromArgb(0, 150, 136),
                Padding = new Padding(15, 10, 15, 10)
            };

            var lblTitle = new Label
            {
                Text = "Vendor Cartridge Refill",
                Font = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize = true,
                Location = new Point(15, 10)
            };

            var lblSubtitle = new Label
            {
                Text = "Batch-level vendor refill tracking",
                Font = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(230, 230, 230),
                AutoSize = true,
                Location = new Point(15, 35)
            };

            headerPanel.Controls.Add(lblTitle);
            headerPanel.Controls.Add(lblSubtitle);

            // 2) Action Bar Panel
            var actionBarPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10, 7, 10, 7)
            };

            btnRefresh = new Button
            {
                Text = "⟳ Refresh",
                Width = 100,
                Height = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location = new Point(10, 6)
            };
            btnRefresh.FlatAppearance.BorderSize = 0;
            btnRefresh.Click += async (s, e) => await LoadBatchesAsync();

            btnSendToVendor = new Button
            {
                Text = "Send to Vendor",
                Width = 140,
                Height = 32,
                BackColor = Color.FromArgb(46, 204, 113),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                Enabled = false,
                Anchor = AnchorStyles.Top | AnchorStyles.Right,
                Location = new Point(200, 6)
            };
            btnSendToVendor.FlatAppearance.BorderSize = 0;
            btnSendToVendor.Click += BtnSendToVendor_Click;

            actionBarPanel.Controls.Add(btnRefresh);
            actionBarPanel.Controls.Add(btnSendToVendor);

            // 3) DataGridView (Dock=Fill, added LAST so it fills remaining space)
            dgvBatches = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible = false
            };

            // Style grid
            dgvBatches.EnableHeadersVisualStyles = false;
            dgvBatches.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 150, 136);
            dgvBatches.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            dgvBatches.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            dgvBatches.ColumnHeadersDefaultCellStyle.Padding = new Padding(5);
            dgvBatches.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            dgvBatches.DefaultCellStyle.SelectionBackColor = Color.FromArgb(0, 150, 136);
            dgvBatches.DefaultCellStyle.SelectionForeColor = Color.White;
            dgvBatches.DefaultCellStyle.Font = new Font("Segoe UI", 9F);

            // Define columns
            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText = "Batch ID",
                Width = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VendorName",
                HeaderText = "Vendor",
                FillWeight = 20
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText = "Cartridge Model",
                FillWeight = 25
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "OriginalQty",
                HeaderText = "Original Qty",
                Width = 100,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedQty",
                HeaderText = "Returned Qty",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });

            dgvBatches.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Status",
                HeaderText = "Status",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });

            // Wire up events
            dgvBatches.SelectionChanged += (s, e) =>
            {
                if (dgvBatches.SelectedRows.Count == 1)
                {
                    var batch = dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
                    btnSendToVendor.Enabled = batch != null && batch.Status == "Active";
                }
                else
                {
                    btnSendToVendor.Enabled = false;
                }
            };

            dgvBatches.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                    return;

                var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
                if (batch == null)
                    return;

                var columnName = dgvBatches.Columns[e.ColumnIndex].DataPropertyName;

                if (columnName == "Status" && e.Value != null)
                {
                    switch (e.Value.ToString())
                    {
                        case "Active":
                            e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219);
                            break;
                        case "SentForRefill":
                            e.CellStyle.ForeColor = Color.Gray;
                            break;
                    }
                }
            };

            // ADD CONTROLS TO this.Controls IN CORRECT ORDER FOR DOCKING
            // Fill control LAST, Top controls FIRST (in reverse visual order)
            this.Controls.Add(dgvBatches);       // Fill - added first, docks last
            this.Controls.Add(actionBarPanel);   // Top - added second
            this.Controls.Add(headerPanel);      // Top - added last, docks first (appears at top)
        }

        /// <summary>
        /// Loads batch-level refill data from service
        /// Re-evaluates eligibility and refreshes grid
        /// </summary>
        private async Task LoadBatchesAsync()
        {
            try
            {
                btnRefresh.Enabled = false;
                btnSendToVendor.Enabled = false;

                // Re-evaluate batch eligibility
                await _refillService.ProcessEligibleBatchesAsync();

                // Load batch data
                _batches = await _refillService.GetRefillEligibilityAsync();
                dgvBatches.DataSource = _batches;

                // Clear selection
                if (dgvBatches.Rows.Count > 0)
                {
                    dgvBatches.ClearSelection();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor refill data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.LoadBatchesAsync failed", ex);
            }
            finally
            {
                btnRefresh.Enabled = true;
            }
        }

        /// <summary>
        /// Handles selection changes - enables Send button only for Active batches
        /// </summary>
        private void DgvBatches_SelectionChanged(object sender, EventArgs e)
        {
            if (dgvBatches.SelectedRows.Count == 1)
            {
                var batch = dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
                if (batch != null)
                {
                    btnSendToVendor.Enabled = batch.Status == "Active";
                    return;
                }
            }

            btnSendToVendor.Enabled = false;
        }

        /// <summary>
        /// Formats cells based on status and values
        /// </summary>
        private void DgvBatches_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                return;

            var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
            if (batch == null)
                return;

            var columnName = dgvBatches.Columns[e.ColumnIndex].DataPropertyName;

            // Status column formatting
            if (columnName == "Status" && e.Value != null)
            {
                var status = e.Value.ToString();
                switch (status)
                {
                    case "Active":
                        e.CellStyle.ForeColor = Color.FromArgb(52, 152, 219); // Blue
                        break;
                    case "SentForRefill":
                        e.CellStyle.ForeColor = Color.FromArgb(230, 126, 34); // Orange
                        break;
                    case "Completed":
                        e.CellStyle.ForeColor = Color.FromArgb(149, 165, 166); // Gray
                        break;
                }
            }
        }

        /// <summary>
        /// Disable row selection if Status IN ('SentForRefill', 'Completed')
        /// </summary>
        private void DgvBatches_RowPrePaint(object sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= dgvBatches.Rows.Count)
                return;

            var batch = dgvBatches.Rows[e.RowIndex].DataBoundItem as RefillEligibilityResult;
            if (batch == null)
                return;

            // Disable selection for SentForRefill and Completed
            if (batch.Status == "SentForRefill" || batch.Status == "Completed")
            {
                dgvBatches.Rows[e.RowIndex].DefaultCellStyle.SelectionBackColor = dgvBatches.Rows[e.RowIndex].DefaultCellStyle.BackColor;
                dgvBatches.Rows[e.RowIndex].DefaultCellStyle.SelectionForeColor = dgvBatches.Rows[e.RowIndex].DefaultCellStyle.ForeColor;
            }
        }

        /// <summary>
        /// Sends selected batch to vendor for refill
        /// Only enabled when exactly one Active batch is selected
        /// </summary>
        private async void BtnSendToVendor_Click(object sender, EventArgs e)
        {
            if (dgvBatches.SelectedRows.Count != 1)
                return;

            var batch = dgvBatches.SelectedRows[0].DataBoundItem as RefillEligibilityResult;
            if (batch == null || batch.Status != "Active")
                return;

            // Confirmation dialog
            var confirmResult = MessageBox.Show(
                $"Send this batch to vendor for refill?\n\n" +
                $"Batch ID: {batch.BatchId}\n" +
                $"Vendor: {batch.VendorName}\n" +
                $"Model: {batch.CartridgeModel}\n" +
                $"Quantity: {batch.ReturnedQty} cartridges\n\n" +
                $"This will mark the batch as 'SentForRefill'.",
                "Confirm Send to Vendor",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirmResult != DialogResult.Yes)
                return;

            try
            {
                btnSendToVendor.Enabled = false;
                btnRefresh.Enabled = false;

                // Get full batch details
                var batchDetails = await _refillService.GetBatchByIdAsync(batch.BatchId);
                if (batchDetails == null)
                {
                    MessageBox.Show("Batch not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Send to vendor via service
                await _refillService.SendBatchForRefillAsync(
                    batch.BatchId,
                    batchDetails.VendorId,
                    batch.ReturnedQty,
                    AppSession.CurrentUserId,
                    $"Sent {batch.ReturnedQty} empty cartridges to {batch.VendorName} for refill");

                MessageBox.Show(
                    $"Successfully sent batch {batch.BatchId} to {batch.VendorName} for refill.\n\n" +
                    $"Quantity: {batch.ReturnedQty} cartridges",
                    "Success",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                // Reload batches
                await LoadBatchesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error sending batch to vendor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("VendorCartridgeRefillPage.BtnSendToVendor_Click failed", ex);
            }
            finally
            {
                btnSendToVendor.Enabled = true;
                btnRefresh.Enabled = true;
            }
        }
    }
}
