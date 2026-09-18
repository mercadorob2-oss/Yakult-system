using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Pages;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Lists active DISPOSE and SELL outbound batches.
    /// Each batch groups cartridges being sent as one physical shipment to a disposal/sale company.
    /// Finalize button marks the batch and all linked cartridges as Disposed or Sold.
    /// </summary>
    public class OutboundBatchesPage : UserControl
    {
        private DataGridView _dgv;
        private DataGridView _dgvAuditTrail;  // Audit trail for selected batch (READ-ONLY)
        private Button       _btnRefresh;
        private Button       _btnNewBatch;
        private Label        _lblSummary;
        private Button       _btnDelete;

        private readonly CartridgeRefillService _service;
        private List<OutboundBatchDto>          _rows;
        private List<VendorBatchAuditTrailDto>  _auditTrail;

        private static readonly Color HeaderColor = Color.FromArgb(58, 134, 232);
        private static readonly Color Orange      = Color.FromArgb(230, 126, 34);
        private static readonly Color Green       = Color.FromArgb(39, 174, 96);

        public OutboundBatchesPage()
        {
            _service    = new CartridgeRefillService();
            _rows       = new List<OutboundBatchDto>();
            _auditTrail = new List<VendorBatchAuditTrailDto>();
            BuildUi();
            _ = LoadAsync();
        }

        private void BuildUi()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.White;

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 68,
                BackColor = Color.FromArgb(245, 247, 250)
            };
            header.Controls.Add(new Label
            {
                Text      = "Outbound Batches",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.FromArgb(30, 41, 59),
                AutoSize  = true,
                Location  = new Point(15, 8)
            });

            // ── Action bar ───────────────────────────────────────────────────
            var actionBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 45,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding   = new Padding(10, 7, 10, 7)
            };

            _btnRefresh = new Button
            {
                Text      = "⟳ Refresh",
                Width     = 100,
                Height    = 32,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(10, 6)
            };
            _btnRefresh.FlatAppearance.BorderSize = 0;
            _btnRefresh.Click += async (s, e) => await LoadAsync();

            _btnNewBatch = new Button
            {
                Text      = "+ New Outbound Batch",
                Width     = 170,
                Height    = 32,
                BackColor = Orange,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(120, 6)
            };
            _btnNewBatch.FlatAppearance.BorderSize = 0;
            _btnNewBatch.Click += BtnNewBatch_Click;

            _btnDelete = new Button
            {
                Text      = "🗑 Delete Batch",
                Width     = 130,
                Height    = 32,
                BackColor = Color.FromArgb(192, 57, 43),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(300, 6),
                Enabled   = false
            };
            _btnDelete.FlatAppearance.BorderSize = 0;
            _btnDelete.Click += async (s, e) => await BtnDelete_ClickAsync();

            _lblSummary = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Location  = new Point(634, 13)
            };

            var btnReport = new Button
            {
                Text      = "📄 Dispose/Sold Report",
                Width     = 185,
                Height    = 32,
                BackColor = Color.FromArgb(27, 58, 107),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(440, 6)
            };
            btnReport.FlatAppearance.BorderSize = 0;
            btnReport.Click += (s, e) => OpenDisposedSoldReport();

            actionBar.Controls.Add(_btnRefresh);
            actionBar.Controls.Add(_btnNewBatch);
            actionBar.Controls.Add(_btnDelete);
            actionBar.Controls.Add(btnReport);
            actionBar.Controls.Add(_lblSummary);

            // ── Audit Trail Section (READ-ONLY) ───────────────────────────────
            var auditPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 300,
                BackColor = Color.White,
                Padding   = new Padding(0)
            };

            var auditHeaderPanel = new Panel
            {
                Dock         = DockStyle.Top,
                AutoSize     = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor    = Color.White,
                Padding      = new Padding(10, 10, 10, 10)
            };

            var lblAuditTitle = new Label
            {
                Text      = "Batch Audit Trail (Read-Only)",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize  = true,
                Dock      = DockStyle.Top,
                Padding   = new Padding(0, 0, 0, 2)
            };
            auditHeaderPanel.Controls.Add(lblAuditTitle);

            var lblAuditSubtitle = new Label
            {
                Text      = "Cartridges assigned to the selected outbound batch  •  Select a batch below to view",
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(149, 165, 166),
                AutoSize  = true,
                Dock      = DockStyle.Top,
                Padding   = new Padding(0, 0, 0, 4)
            };
            auditHeaderPanel.Controls.Add(lblAuditSubtitle);

            _dgvAuditTrail = new DataGridView
            {
                Dock                        = DockStyle.Fill,
                AutoGenerateColumns         = false,
                AllowUserToAddRows          = false,
                AllowUserToDeleteRows       = false,
                AllowUserToResizeRows       = false,
                ReadOnly                    = true,
                SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect                 = false,
                AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor             = Color.White,
                BorderStyle                 = BorderStyle.FixedSingle,
                ColumnHeadersVisible        = true,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                ColumnHeadersHeight         = 36,
                RowHeadersVisible           = false
            };

            _dgvAuditTrail.EnableHeadersVisualStyles                   = false;
            _dgvAuditTrail.ColumnHeadersDefaultCellStyle.BackColor     = Color.FromArgb(52, 152, 219);
            _dgvAuditTrail.ColumnHeadersDefaultCellStyle.ForeColor     = Color.White;
            _dgvAuditTrail.ColumnHeadersDefaultCellStyle.Font          = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _dgvAuditTrail.ColumnHeadersDefaultCellStyle.Padding       = new Padding(5);
            _dgvAuditTrail.ColumnHeadersDefaultCellStyle.Alignment     = DataGridViewContentAlignment.MiddleLeft;
            _dgvAuditTrail.AlternatingRowsDefaultCellStyle.BackColor   = Color.FromArgb(232, 240, 248);
            _dgvAuditTrail.DefaultCellStyle.SelectionBackColor         = Color.FromArgb(52, 152, 219);
            _dgvAuditTrail.DefaultCellStyle.SelectionForeColor         = Color.White;
            _dgvAuditTrail.DefaultCellStyle.Font                       = new Font("Segoe UI", 9F);

            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "EmptyCartridgeId",
                HeaderText       = "Cartridge ID",
                Width            = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RequestId",
                HeaderText       = "Request ID",
                Width            = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RequestDate",
                HeaderText       = "Returned Date",
                Width            = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedQty",
                HeaderText       = "Qty",
                Width            = 65,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                    Font      = new Font("Segoe UI", 9F, FontStyle.Bold)
                }
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText       = "Cartridge Model",
                FillWeight       = 25
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Vendor",
                HeaderText       = "Vendor",
                FillWeight       = 20
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText       = "Batch ID",
                Width            = 75,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedByName",
                HeaderText       = "Returned By",
                Width            = 140
            });
            _dgvAuditTrail.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Remarks",
                HeaderText       = "Remarks",
                FillWeight       = 25,
                DefaultCellStyle = new DataGridViewCellStyle { ForeColor = Color.FromArgb(127, 140, 141) }
            });

            auditPanel.Controls.Add(_dgvAuditTrail);
            auditPanel.Controls.Add(auditHeaderPanel);

            // ── Separator ─────────────────────────────────────────────────────
            var separator = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 2,
                BackColor = Color.FromArgb(224, 224, 224)
            };

            // ── Batches section title ─────────────────────────────────────────
            var batchesTitlePanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 38,
                BackColor = Color.White,
                Padding   = new Padding(10, 10, 10, 0)
            };
            batchesTitlePanel.Controls.Add(new Label
            {
                Text      = "Active Outbound Batches",
                Font      = new Font("Segoe UI", 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(52, 73, 94),
                AutoSize  = true
            });

            // ── Grid ─────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock                        = DockStyle.Fill,
                AutoGenerateColumns         = false,
                AllowUserToAddRows          = false,
                AllowUserToDeleteRows       = false,
                AllowUserToResizeRows       = false,
                ReadOnly                    = true,
                SelectionMode               = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect                 = false,
                AutoSizeColumnsMode         = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor             = Color.White,
                BorderStyle                 = BorderStyle.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible           = false
            };

            _dgv.EnableHeadersVisualStyles                       = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor         = HeaderColor;
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor         = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font              = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _dgv.ColumnHeadersDefaultCellStyle.Padding           = new Padding(5);
            _dgv.AlternatingRowsDefaultCellStyle.BackColor       = Color.FromArgb(248, 248, 248);
            _dgv.DefaultCellStyle.SelectionBackColor             = HeaderColor;
            _dgv.DefaultCellStyle.SelectionForeColor             = Color.White;
            _dgv.DefaultCellStyle.Font                           = new Font("Segoe UI", 9F);

            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BatchId",
                HeaderText       = "Batch #",
                Width            = 75,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colPurpose",
                DataPropertyName = "BatchPurpose",
                HeaderText       = "Purpose",
                Width            = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "VendorName",
                HeaderText       = "Vendor",
                FillWeight       = 30
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ModelSummary",
                HeaderText       = "Models",
                FillWeight       = 40
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalQty",
                HeaderText       = "Total Qty",
                Width            = 80,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CreatedDate",
                HeaderText       = "Created",
                Width            = 130
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Remarks",
                HeaderText       = "Remarks",
                FillWeight       = 20
            });

            var colFinalize = new DataGridViewButtonColumn
            {
                Name                        = "colFinalize",
                HeaderText                  = "Action",
                Text                        = "Finalize",
                UseColumnTextForButtonValue = true,
                Width                       = 90,
                FlatStyle                   = FlatStyle.Flat,
                DefaultCellStyle            = new DataGridViewCellStyle
                {
                    BackColor = HeaderColor,
                    ForeColor = Color.White,
                    Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                }
            };
            _dgv.Columns.Add(colFinalize);

            _dgv.CellFormatting += DgvCellFormatting;
            _dgv.CellClick      += async (s, e) => await DgvCellClickAsync(e);

            // Load audit trail when a batch row is selected; enable Delete when a row is selected
            _dgv.SelectionChanged += async (s, e) =>
            {
                if (_dgv.SelectedRows.Count == 1)
                {
                    var batch = _dgv.SelectedRows[0].DataBoundItem as OutboundBatchDto;
                    if (batch != null)
                    {
                        _btnDelete.Enabled = true;
                        await LoadAuditTrailAsync(batch.BatchId);
                    }
                }
                else
                {
                    _btnDelete.Enabled = false;
                    _auditTrail.Clear();
                    _dgvAuditTrail.DataSource = null;
                }
            };

            // Docking order: Fill first, then Top controls in reverse visual order
            // (last added Top control occupies the topmost position)
            Controls.Add(_dgv);              // Fill — fills remaining space
            Controls.Add(batchesTitlePanel); // Top
            Controls.Add(separator);         // Top
            Controls.Add(auditPanel);        // Top
            Controls.Add(actionBar);         // Top
            Controls.Add(header);            // Top — docks at very top
        }

        // ── Cell formatting ───────────────────────────────────────────────────

        private void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

            var row = _rows[e.RowIndex];
            var col = _dgv.Columns[e.ColumnIndex];

            if (col.DataPropertyName == "CreatedDate" && e.Value is DateTime dt)
            {
                e.Value = dt.ToString("yyyy-MM-dd HH:mm");
                e.FormattingApplied = true;
            }

            if (col.Name == "colPurpose")
            {
                bool isDispose = row.BatchPurpose == "DISPOSE";
                e.CellStyle.BackColor          = isDispose ? Color.FromArgb(255, 224, 178) : Color.FromArgb(200, 230, 201);
                e.CellStyle.ForeColor          = isDispose ? Color.FromArgb(180, 80, 0)    : Color.FromArgb(27, 94, 32);
                e.CellStyle.SelectionBackColor = isDispose ? Orange : Green;
                e.CellStyle.SelectionForeColor = Color.White;
                e.FormattingApplied = true;
            }

            if (col.Name == "colFinalize")
            {
                e.CellStyle.BackColor          = HeaderColor;
                e.CellStyle.ForeColor          = Color.White;
                e.CellStyle.SelectionBackColor = HeaderColor;
                e.CellStyle.SelectionForeColor = Color.White;
                e.FormattingApplied = true;
            }
        }

        // ── Cell click ────────────────────────────────────────────────────────

        private async Task DgvCellClickAsync(DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;
            if (_dgv.Columns[e.ColumnIndex].Name != "colFinalize") return;

            var batch  = _rows[e.RowIndex];
            string action = batch.BatchPurpose == "DISPOSE" ? "Disposed" : "Sold";

            string confirm =
                $"Mark Batch #{batch.BatchId} as {action}?\n\n" +
                $"  • Vendor: {batch.VendorName}\n" +
                $"  • Models: {(string.IsNullOrEmpty(batch.ModelSummary) ? "(see batch lines)" : batch.ModelSummary)}\n" +
                $"  • Total units: {batch.TotalQty}\n\n" +
                "This will update all linked cartridges and cannot be undone.";

            if (MessageBox.Show(confirm, $"Confirm Finalize Batch #{batch.BatchId}",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;

            await ExecuteFinalizeAsync(batch.BatchId, action);
        }

        private async Task ExecuteFinalizeAsync(int batchId, string action)
        {
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

            _btnRefresh.Enabled  = false;
            _btnNewBatch.Enabled = false;
            _btnDelete.Enabled   = false;
            try
            {
                await _service.FinalizeOutboundBatchAsync(batchId, userId);

                MessageBox.Show(
                    $"Batch #{batchId} finalized — all linked cartridges marked as {action}.",
                    "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadAsync();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("no linked empty cartridges"))
            {
                // Batch is empty — offer to delete it instead of showing an unhelpful error
                var answer = MessageBox.Show(
                    $"Batch #{batchId} has no assigned cartridges and cannot be finalized.\n\n" +
                    "Would you like to delete this empty batch instead?",
                    $"Batch #{batchId} Is Empty",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (answer == DialogResult.Yes)
                    await ExecuteDeleteAsync(batchId);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError($"OutboundBatchesPage.ExecuteFinalizeAsync(batchId={batchId}) failed", ex);
            }
            finally
            {
                _btnRefresh.Enabled  = true;
                _btnNewBatch.Enabled = true;
                _btnDelete.Enabled   = _dgv.SelectedRows.Count == 1;
            }
        }

        private async Task BtnDelete_ClickAsync()
        {
            if (_dgv.SelectedRows.Count != 1) return;
            var batch = _dgv.SelectedRows[0].DataBoundItem as OutboundBatchDto;
            if (batch == null) return;

            int cartridgeCount = _auditTrail.Count;
            string cartridgeNote = cartridgeCount > 0
                ? $"\n\n  • {cartridgeCount} assigned cartridge(s) will be unassigned and returned to Pending."
                : "\n\n  • Batch has no assigned cartridges.";

            string confirm =
                $"Delete Batch #{batch.BatchId} ({batch.BatchPurpose})?\n\n" +
                $"  • Vendor: {batch.VendorName}\n" +
                $"  • Models: {(string.IsNullOrEmpty(batch.ModelSummary) ? "(none)" : batch.ModelSummary)}" +
                cartridgeNote + "\n\nThis action cannot be undone.";

            if (MessageBox.Show(confirm, $"Confirm Delete Batch #{batch.BatchId}",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            await ExecuteDeleteAsync(batch.BatchId);
        }

        private async Task ExecuteDeleteAsync(int batchId)
        {
            int userId = AppSession.CurrentUserId > 0 ? AppSession.CurrentUserId : 1;

            _btnRefresh.Enabled  = false;
            _btnNewBatch.Enabled = false;
            _btnDelete.Enabled   = false;
            try
            {
                await _service.DeleteOutboundBatchAsync(batchId, userId);

                MessageBox.Show(
                    $"Batch #{batchId} deleted successfully.",
                    "Deleted", MessageBoxButtons.OK, MessageBoxIcon.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting batch: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError($"OutboundBatchesPage.ExecuteDeleteAsync(batchId={batchId}) failed", ex);
            }
            finally
            {
                _btnRefresh.Enabled  = true;
                _btnNewBatch.Enabled = true;
                _btnDelete.Enabled   = _dgv.SelectedRows.Count == 1;
            }
        }

        // ── New batch button ──────────────────────────────────────────────────

        private void BtnNewBatch_Click(object sender, EventArgs e)
        {
            using (var dlg = new OutboundBatchAssignmentDialog())
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _ = LoadAsync();
            }
        }

        // ── Data loading ──────────────────────────────────────────────────────

        private async Task LoadAsync()
        {
            try
            {
                _btnRefresh.Enabled  = false;
                _btnNewBatch.Enabled = false;
                _btnDelete.Enabled   = false;

                _rows           = await _service.GetOutboundBatchesAsync();
                _dgv.DataSource = _rows;

                // Clear audit trail on refresh
                _auditTrail.Clear();
                _dgvAuditTrail.DataSource = null;
                if (_dgv.Rows.Count > 0)
                    _dgv.ClearSelection();

                if (_rows.Count == 0)
                {
                    _lblSummary.Text      = "No active outbound batches.";
                    _lblSummary.ForeColor = Color.FromArgb(100, 100, 100);
                }
                else
                {
                    int totalQty = _rows.Sum(r => r.TotalQty);
                    _lblSummary.Text      = $"{_rows.Count} active outbound batch(es)  •  {totalQty} total unit(s)";
                    _lblSummary.ForeColor = Orange;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("OutboundBatchesPage.LoadAsync failed", ex);
            }
            finally
            {
                _btnRefresh.Enabled  = true;
                _btnNewBatch.Enabled = true;
                // Delete stays disabled until user selects a row
            }
        }

        // ── Audit trail loading ───────────────────────────────────────────────

        /// <summary>
        /// Loads the audit trail for the selected outbound batch (READ-ONLY).
        /// Shows all empty cartridges assigned to the batch via EmptyCartridge.VendorBatchId.
        /// </summary>
        private async Task LoadAuditTrailAsync(int batchId)
        {
            try
            {
                var raw = await _service.GetBatchAuditTrailAsync(batchId);

                _auditTrail = raw
                    .OrderByDescending(x => x.RequestDate)
                    .ThenByDescending(x => x.EmptyCartridgeId)
                    .ToList();

                _dgvAuditTrail.DataSource = _auditTrail;
                _dgvAuditTrail.ClearSelection();
                _dgvAuditTrail.CurrentCell = null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading audit trail: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("OutboundBatchesPage.LoadAuditTrailAsync failed", ex);
            }
        }

        private void OpenDisposedSoldReport()
        {
            using (var dlg = new DisposedSoldReportFilterDialog())
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;

                string typeFilter = dlg.SelectedFilter == "All" ? null : dlg.SelectedFilter;

                var repo = new DisposedSoldReportRepository();
                var data = repo.GetReport(from: dlg.FromDate, to: dlg.ToDate, decisionType: typeFilter);

                if (data == null || data.Count == 0)
                {
                    MessageBox.Show("No records found for the selected filters.", "No Data",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var sigData = ReportLauncher.ShowSignatoryPicker();
                if (sigData == null) return;

                var colParams = ReportLauncher.ShowColumnSelection(
                    "Outbound Cartridges Report",
                    Dialogs.ReportColumnSelectionDialog.CartridgeDisposeSoldReportColumns());
                if (colParams == null) return;

                var dt = BuildDisposedSoldDataTable(data);

                using (var frm = ReportLauncher.CreateCartridgeDisposeSoldReportForm(dt, sigData, colParams))
                    frm?.ShowDialog(this);
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
