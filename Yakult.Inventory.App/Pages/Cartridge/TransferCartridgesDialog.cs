using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Allows bulk transfer of selected cartridge rows from one Active refill batch
    /// to another Active refill batch.
    ///
    /// Scenario supported: A batch contains 20 MOON123 + 12 SOLAR123 cartridges.
    /// The user checks any subset (e.g. 10 MOON123 rows + 7 SOLAR123 rows) and
    /// transfers them all to a chosen target batch in one operation.
    /// </summary>
    public class TransferCartridgesDialog : Form
    {
        private readonly VendorCartridgeBatchDto _sourceBatch;
        private readonly CartridgeRefillService  _refillService;

        private ComboBox  _cmbTarget;
        private DataGridView _dgv;
        private Label     _lblSummary;
        private Button    _btnTransfer;
        private Button    _btnCancel;

        private List<VendorBatchAuditTrailDto> _rows = new List<VendorBatchAuditTrailDto>();
        private readonly List<int> _targetBatchIds   = new List<int>();
        private CheckState _headerCheckState         = CheckState.Unchecked;

        /// <summary>True if at least one cartridge was successfully transferred.</summary>
        public bool AnyTransferred { get; private set; }

        private static readonly Color Teal = Color.FromArgb(0, 150, 136);

        public TransferCartridgesDialog(VendorCartridgeBatchDto sourceBatch)
        {
            _sourceBatch   = sourceBatch ?? throw new ArgumentNullException(nameof(sourceBatch));
            _refillService = new CartridgeRefillService();
            BuildUi();
            this.Load += async (s, e) => await LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════════════
        // UI CONSTRUCTION
        // ══════════════════════════════════════════════════════════════════════

        private void BuildUi()
        {
            Text            = $"Transfer Cartridges — Batch #{_sourceBatch.BatchId}";
            Size            = new Size(860, 620);
            MinimumSize     = new Size(700, 480);
            StartPosition   = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            MinimizeBox     = false;
            BackColor       = Color.White;

            // ── Header ──────────────────────────────────────────────────────
            var header = new Panel { Dock = DockStyle.Top, Height = 68, BackColor = Teal };
            var lblHeaderTitle = new Label
            {
                Text      = "Transfer Cartridges",
                Font      = new Font("Segoe UI", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(20, 10)
            };
            var lblHeaderSub = new Label
            {
                Text      = $"Batch #{_sourceBatch.BatchId}  •  {_sourceBatch.VendorName}",
                Font      = new Font("Segoe UI", 8.5F),
                ForeColor = Color.FromArgb(210, 240, 240),
                AutoSize  = true,
                Location  = new Point(22, 44)
            };
            // Add subtitle first so it renders beneath the title
            header.Controls.Add(lblHeaderSub);
            header.Controls.Add(lblHeaderTitle);

            // ── Target batch selector ────────────────────────────────────────
            var targetPanel = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 52,
                BackColor = Color.FromArgb(248, 249, 250)
            };
            targetPanel.Controls.Add(new Label
            {
                Text      = "Transfer to batch:",
                Location  = new Point(16, 16),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                BackColor = Color.Transparent
            });
            _cmbTarget = new ComboBox
            {
                Location      = new Point(190, 13),
                Width         = 640,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("Segoe UI", 9F),
                Anchor        = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            targetPanel.Controls.Add(_cmbTarget);
            targetPanel.Resize += (s, e) => _cmbTarget.Width = targetPanel.ClientSize.Width - 206;
            _cmbTarget.SelectedIndexChanged += (s, e) => UpdateSummaryAndButtons();

            // ── Select-all toolbar ───────────────────────────────────────────
            var toolbar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 36,
                BackColor = Color.White
            };

            var btnSelectAll = new Button
            {
                Text      = "Select All",
                Width     = 85,
                Height    = 26,
                Location  = new Point(16, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(52, 152, 219),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnSelectAll.FlatAppearance.BorderSize = 0;
            btnSelectAll.Click += (s, e) => SetAllChecked(true);

            var btnClearAll = new Button
            {
                Text      = "Clear All",
                Width     = 80,
                Height    = 26,
                Location  = new Point(110, 5),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(189, 195, 199),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Bold),
                Cursor    = Cursors.Hand
            };
            btnClearAll.FlatAppearance.BorderSize = 0;
            btnClearAll.Click += (s, e) => SetAllChecked(false);

            _lblSummary = new Label
            {
                Text      = "0 row(s) selected  •  0 unit(s)",
                Location  = new Point(205, 9),
                AutoSize  = true,
                Font      = new Font("Segoe UI", 8.5F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                BackColor = Color.Transparent
            };

            toolbar.Controls.Add(btnSelectAll);
            toolbar.Controls.Add(btnClearAll);
            toolbar.Controls.Add(_lblSummary);

            // ── DataGridView ─────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock                  = DockStyle.Fill,
                AutoGenerateColumns   = false,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly              = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect           = true,
                BackgroundColor       = Color.White,
                BorderStyle           = BorderStyle.None,
                GridColor             = Color.FromArgb(224, 224, 224),
                RowHeadersVisible     = false,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars            = ScrollBars.Both
            };
            _dgv.EnableHeadersVisualStyles                = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor = Teal;
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font      = new Font("Segoe UI", 9F, FontStyle.Bold);
            _dgv.ColumnHeadersHeightSizeMode             = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _dgv.ColumnHeadersHeight                     = 34;
            _dgv.DefaultCellStyle.Font                   = new Font("Segoe UI", 9F);
            _dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);
            _dgv.DefaultCellStyle.SelectionBackColor     = Color.FromArgb(178, 235, 242);
            _dgv.DefaultCellStyle.SelectionForeColor     = Color.Black;
            _dgv.RowTemplate.Height                      = 28;

            _dgv.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name             = "chkSelect",
                HeaderText       = "",
                Width            = 36,
                ReadOnly         = false,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "colModel",
                HeaderText = "Cartridge Model",
                Width      = 180,
                ReadOnly   = true
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colReqId",
                HeaderText       = "Req ID",
                Width            = 80,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colQty",
                HeaderText       = "Qty",
                Width            = 65,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name       = "colDate",
                HeaderText = "Returned At",
                Width      = 145,
                ReadOnly   = true
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name         = "colBy",
                HeaderText   = "Returned By",
                ReadOnly     = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
            });

            // Commit checkbox on click so CellValueChanged fires reliably
            _dgv.CellContentClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && _dgv.Columns[e.ColumnIndex].Name == "chkSelect")
                    _dgv.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            _dgv.CellValueChanged += Dgv_CellValueChanged;

            // Paint a native checkbox in the chkSelect column header
            _dgv.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1 || e.ColumnIndex != _dgv.Columns["chkSelect"].Index) return;
                e.PaintBackground(e.ClipBounds, true);
                CheckBoxState state;
                switch (_headerCheckState)
                {
                    case CheckState.Checked:       state = CheckBoxState.CheckedNormal;   break;
                    case CheckState.Indeterminate: state = CheckBoxState.MixedNormal;     break;
                    default:                       state = CheckBoxState.UncheckedNormal; break;
                }
                var glyphSize = CheckBoxRenderer.GetGlyphSize(e.Graphics, state);
                var pt = new Point(
                    e.CellBounds.X + (e.CellBounds.Width  - glyphSize.Width)  / 2,
                    e.CellBounds.Y + (e.CellBounds.Height - glyphSize.Height) / 2);
                CheckBoxRenderer.DrawCheckBox(e.Graphics, pt, state);
                e.Handled = true;
            };

            // Clicking the header checkbox toggles all rows
            _dgv.CellMouseClick += (s, e) =>
            {
                if (e.RowIndex != -1 || e.ColumnIndex != _dgv.Columns["chkSelect"].Index) return;
                bool newValue = _headerCheckState != CheckState.Checked;
                _dgv.CellValueChanged -= Dgv_CellValueChanged;
                try   { foreach (DataGridViewRow row in _dgv.Rows) row.Cells["chkSelect"].Value = newValue; }
                finally { _dgv.CellValueChanged += Dgv_CellValueChanged; }
                _dgv.EndEdit();
                _headerCheckState = newValue ? CheckState.Checked : CheckState.Unchecked;
                _dgv.InvalidateCell(_dgv.Columns["chkSelect"].Index, -1);
                UpdateSummaryAndButtons();
            };

            // ── Bottom button panel ──────────────────────────────────────────
            var bottom = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 52,
                BackColor = Color.FromArgb(250, 250, 250)
            };

            _btnCancel = new Button
            {
                Text      = "Close",
                Width     = 90,
                Height    = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(189, 195, 199),
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Right
            };
            _btnCancel.FlatAppearance.BorderSize = 0;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            _btnTransfer = new Button
            {
                Text      = "Transfer Selected",
                Width     = 145,
                Height    = 34,
                FlatStyle = FlatStyle.Flat,
                BackColor = Teal,
                ForeColor = Color.White,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Right,
                Enabled   = false
            };
            _btnTransfer.FlatAppearance.BorderSize = 0;
            _btnTransfer.Click += async (s, e) => await ExecuteTransferAsync();

            bottom.Controls.Add(_btnCancel);
            bottom.Controls.Add(_btnTransfer);
            bottom.Resize += (s, e) =>
            {
                _btnCancel.Location   = new Point(bottom.ClientSize.Width - _btnCancel.Width - 16, 9);
                _btnTransfer.Location = new Point(bottom.ClientSize.Width - _btnCancel.Width - _btnTransfer.Width - 26, 9);
            };

            // Dock order: Fill first, then Bottom, then Top controls top-to-bottom in reverse
            this.Controls.Add(_dgv);         // Fill
            this.Controls.Add(bottom);       // Bottom
            this.Controls.Add(toolbar);      // Top — 3rd visual row
            this.Controls.Add(targetPanel);  // Top — 2nd visual row
            this.Controls.Add(header);       // Top — topmost
        }

        // ══════════════════════════════════════════════════════════════════════
        // DATA LOADING
        // ══════════════════════════════════════════════════════════════════════

        private async Task LoadAsync()
        {
            _cmbTarget.Items.Clear();
            _targetBatchIds.Clear();
            _dgv.Rows.Clear();

            try
            {
                // Target batches — all Active batches except the source
                var activeBatches = await _refillService.GetAllActiveBatchesAsync();
                foreach (var b in activeBatches.Where(b => b.BatchId != _sourceBatch.BatchId))
                {
                    _targetBatchIds.Add(b.BatchId);
                    _cmbTarget.Items.Add(
                        $"Batch #{b.BatchId}  —  {b.VendorName}  —  {b.CartridgeModel}  ({b.ReturnedQty} unit(s))");
                }

                if (_cmbTarget.Items.Count == 0)
                    _cmbTarget.Items.Add("(No other active batches available)");

                // Cartridge rows from audit trail
                _rows = await _refillService.GetBatchAuditTrailAsync(_sourceBatch.BatchId);
                foreach (var row in _rows)
                {
                    int idx = _dgv.Rows.Add(
                        false,
                        row.CartridgeModel,
                        row.RequestId.HasValue ? row.RequestId.ToString() : "—",
                        row.ReturnedQty,
                        row.RequestDate.ToString("yyyy-MM-dd HH:mm"),
                        row.ReturnedByName ?? "—"
                    );
                    _dgv.Rows[idx].Tag = row;
                }

                UpdateSummaryAndButtons();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load batch data:\n{ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("TransferCartridgesDialog.LoadAsync failed", ex);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // CHECKBOX HELPERS
        // ══════════════════════════════════════════════════════════════════════

        private void SetAllChecked(bool check)
        {
            _dgv.CellValueChanged -= Dgv_CellValueChanged;
            try   { foreach (DataGridViewRow row in _dgv.Rows) row.Cells["chkSelect"].Value = check; }
            finally { _dgv.CellValueChanged += Dgv_CellValueChanged; }
            _dgv.EndEdit();
            UpdateHeaderCheckState();
            UpdateSummaryAndButtons();
        }

        private void Dgv_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && _dgv.Columns[e.ColumnIndex].Name == "chkSelect")
            {
                UpdateHeaderCheckState();
                UpdateSummaryAndButtons();
            }
        }

        private void UpdateHeaderCheckState()
        {
            int total = _dgv.Rows.Count;
            if (total == 0)
            {
                _headerCheckState = CheckState.Unchecked;
            }
            else
            {
                int checkedCount = 0;
                foreach (DataGridViewRow row in _dgv.Rows)
                    if (row.Cells["chkSelect"].Value is bool b && b) checkedCount++;

                _headerCheckState = checkedCount == 0    ? CheckState.Unchecked    :
                                    checkedCount == total ? CheckState.Checked      :
                                                            CheckState.Indeterminate;
            }
            _dgv.InvalidateCell(_dgv.Columns["chkSelect"].Index, -1);
        }

        private void UpdateSummaryAndButtons()
        {
            int rowCount = 0, unitCount = 0;
            foreach (DataGridViewRow r in _dgv.Rows)
            {
                if ((r.Cells["chkSelect"].Value as bool? ?? false) && r.Tag is VendorBatchAuditTrailDto dto)
                {
                    rowCount++;
                    unitCount += dto.ReturnedQty;
                }
            }
            bool hasTarget = _cmbTarget.SelectedIndex >= 0 && _cmbTarget.SelectedIndex < _targetBatchIds.Count;
            _lblSummary.Text      = $"{rowCount} row(s) selected  •  {unitCount} unit(s)";
            _btnTransfer.Enabled  = rowCount > 0 && hasTarget;
        }

        // ══════════════════════════════════════════════════════════════════════
        // TRANSFER EXECUTION
        // ══════════════════════════════════════════════════════════════════════

        private async Task ExecuteTransferAsync()
        {
            if (_cmbTarget.SelectedIndex < 0 || _cmbTarget.SelectedIndex >= _targetBatchIds.Count)
            {
                MessageBox.Show("Please select a target batch.", "Required",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var selectedRows = new List<VendorBatchAuditTrailDto>();
            foreach (DataGridViewRow r in _dgv.Rows)
            {
                if ((r.Cells["chkSelect"].Value as bool? ?? false) && r.Tag is VendorBatchAuditTrailDto dto)
                    selectedRows.Add(dto);
            }

            if (selectedRows.Count == 0) return;

            int targetBatchId = _targetBatchIds[_cmbTarget.SelectedIndex];
            int totalUnits    = selectedRows.Sum(r => r.ReturnedQty);
            int userId        = AppSession.CurrentUserId;

            // Group summary for confirmation message
            var modelSummary = selectedRows
                .GroupBy(r => r.CartridgeModel)
                .Select(g => $"  • {g.Key}: {g.Sum(r => r.ReturnedQty)} unit(s) ({g.Count()} row(s))")
                .ToList();

            var confirm = MessageBox.Show(
                $"Transfer to Batch #{targetBatchId}?\n\n" +
                string.Join("\n", modelSummary) +
                $"\n\nTotal: {totalUnits} unit(s) across {selectedRows.Count} row(s).\n\n" +
                "This action cannot be undone.",
                "Confirm Transfer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (confirm != DialogResult.Yes) return;

            _btnTransfer.Enabled = false;
            _btnCancel.Enabled   = false;

            int transferred = 0;
            var errors      = new List<string>();

            foreach (var row in selectedRows)
            {
                try
                {
                    int qty = await _refillService.TransferToBatchAsync(
                        _sourceBatch.BatchId,
                        targetBatchId,
                        row.RequestId,
                        row.CartridgeModelId,
                        row.EmptyCartridgeId,
                        userId);

                    transferred += qty;
                }
                catch (Exception ex)
                {
                    errors.Add($"EmptyCartridge #{row.EmptyCartridgeId} ({row.CartridgeModel}): {ex.Message}");
                    Logger.LogError(
                        $"TransferCartridgesDialog: transfer failed for EmptyCartridgeId={row.EmptyCartridgeId}", ex);
                }
            }

            AnyTransferred = transferred > 0;

            if (errors.Count == 0)
            {
                MessageBox.Show(
                    $"Successfully transferred {transferred} unit(s) to Batch #{targetBatchId}.",
                    "Transfer Complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    $"Transferred {transferred} unit(s) with {errors.Count} failure(s):\n\n" +
                    string.Join("\n", errors),
                    "Partial Transfer",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }

            // Reload so the grid reflects remaining rows in this batch
            await LoadAsync();
            _btnCancel.Enabled = true;
        }
    }
}
