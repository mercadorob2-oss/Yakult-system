using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Models;
using Yakult.Inventory.App.Services;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Grouped list of returned non-refillable empty cartridges.
    /// Rows are grouped by ReqId + CartridgeModel for presentation only —
    /// underlying EmptyCartridge rows remain individual records in the database.
    ///
    /// Bulk Dispose / Bulk Sell have been removed — use the Outbound Batch workflow instead.
    /// Click "Assign to Outbound Batch" to open the batch assignment dialog.
    ///
    /// TEMPORARY: Each group is now split into a GOOD row and a DAMAGED row (when both
    /// conditions are present).
    /// Revert by removing ConditionType from DTO and reverting GetNonRefillableGroupedAsync.
    /// </summary>
    public class NonRefillableCartridgesPage : UserControl
    {
        private DataGridView _dgv;
        private Button       _btnRefresh;
        private Button       _btnAssignBatch;
        private Label        _lblSummary;

        private readonly CartridgeRefillService      _service;
        private List<NonRefillableGroupedDto>        _rows;

        public NonRefillableCartridgesPage()
        {
            _service = new CartridgeRefillService();
            _rows    = new List<NonRefillableGroupedDto>();
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
                Height    = 76,
                BackColor = Color.FromArgb(0, 150, 136)
            };
            var lblHeaderSub = new Label
            {
                Text      = "Returned cartridges whose model is not eligible for vendor refill  •  Grouped by request  •  Assign to an outbound batch for disposal or resale",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(230, 230, 230),
                AutoSize  = true,
                Location  = new Point(15, 50)
            };
            var lblHeaderTitle = new Label
            {
                Text      = "Non-Refillable Empty Cartridges",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(15, 10)
            };
            // Add subtitle first so title renders on top (correct z-order)
            header.Controls.Add(lblHeaderSub);
            header.Controls.Add(lblHeaderTitle);

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

            _btnAssignBatch = new Button
            {
                Text      = "Assign to Outbound Batch",
                Width     = 200,
                Height    = 32,
                BackColor = Color.FromArgb(230, 126, 34),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor    = Cursors.Hand,
                Font      = new Font("Segoe UI", 9F, FontStyle.Bold),
                Location  = new Point(120, 6)
            };
            _btnAssignBatch.FlatAppearance.BorderSize = 0;
            _btnAssignBatch.Click += BtnAssignBatch_Click;

            _lblSummary = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Location  = new Point(330, 13)
            };

            actionBar.Controls.Add(_btnRefresh);
            actionBar.Controls.Add(_btnAssignBatch);
            actionBar.Controls.Add(_lblSummary);

            // ── Grid ─────────────────────────────────────────────────────────
            _dgv = new DataGridView
            {
                Dock                          = DockStyle.Fill,
                AutoGenerateColumns           = false,
                AllowUserToAddRows            = false,
                AllowUserToDeleteRows         = false,
                AllowUserToResizeRows         = false,
                ReadOnly                      = true,
                SelectionMode                 = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect                   = false,
                AutoSizeColumnsMode           = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor               = Color.White,
                BorderStyle                   = BorderStyle.None,
                ColumnHeadersHeightSizeMode   = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                RowHeadersVisible             = false
            };

            _dgv.EnableHeadersVisualStyles                         = false;
            _dgv.ColumnHeadersDefaultCellStyle.BackColor           = Color.FromArgb(0, 150, 136);
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor           = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font                = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _dgv.ColumnHeadersDefaultCellStyle.Padding             = new Padding(5);
            _dgv.AlternatingRowsDefaultCellStyle.BackColor         = Color.FromArgb(248, 248, 248);
            _dgv.DefaultCellStyle.SelectionBackColor               = Color.FromArgb(0, 150, 136);
            _dgv.DefaultCellStyle.SelectionForeColor               = Color.White;
            _dgv.DefaultCellStyle.Font                             = new Font("Segoe UI", 9F);

            // Data columns
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReqId",
                HeaderText       = "Req ID",
                Width            = 75,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText       = "Cartridge Model",
                FillWeight       = 40,
                ReadOnly         = true
            });
            // TEMPORARY: condition column — shows GOOD / DAMAGED per fan-out row
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name             = "colConditionType",
                DataPropertyName = "ConditionType",
                HeaderText       = "Condition",
                Width            = 90,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TotalQuantity",
                HeaderText       = "Total Qty",
                Width            = 80,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 9F, FontStyle.Bold) }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "RowCount",
                HeaderText       = "Rows",
                Width            = 60,
                ReadOnly         = true,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedAt",
                HeaderText       = "Earliest Return",
                Width            = 140,
                ReadOnly         = true
            });

            _dgv.CellFormatting += DgvCellFormatting;

            // Add in docking order: Fill first, then Top panels
            Controls.Add(_dgv);
            Controls.Add(actionBar);
            Controls.Add(header);
        }

        // ── Cell formatting ───────────────────────────────────────────────────

        private void DgvCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _rows.Count) return;

            var row  = _rows[e.RowIndex];
            var col  = _dgv.Columns[e.ColumnIndex];
            bool isDamaged = row.ConditionType == "DAMAGED";

            // Format date
            if (col.DataPropertyName == "ReturnedAt" && e.Value is DateTime dt)
                e.Value = dt.ToString("yyyy-MM-dd HH:mm");

            // Format ReqId null
            if (col.DataPropertyName == "ReqId" && (e.Value == null || e.Value == DBNull.Value))
                e.Value = "—";

            // TEMPORARY: colour-code the Condition cell
            if (col.Name == "colConditionType")
            {
                if (isDamaged)
                {
                    e.Value                          = "Damaged";
                    e.CellStyle.BackColor            = Color.FromArgb(255, 236, 210);
                    e.CellStyle.ForeColor            = Color.FromArgb(180, 80, 0);
                    e.CellStyle.SelectionBackColor   = Color.FromArgb(200, 120, 50);
                    e.CellStyle.SelectionForeColor   = Color.White;
                }
                else
                {
                    e.Value                          = "Good";
                    e.CellStyle.BackColor            = Color.FromArgb(212, 237, 218);
                    e.CellStyle.ForeColor            = Color.FromArgb(21, 87, 36);
                    e.CellStyle.SelectionBackColor   = Color.FromArgb(60, 140, 80);
                    e.CellStyle.SelectionForeColor   = Color.White;
                }
                e.FormattingApplied = true;
            }
        }

        // ── Outbound batch assignment ─────────────────────────────────────────

        private void BtnAssignBatch_Click(object sender, EventArgs e)
        {
            using (var dlg = new OutboundBatchAssignmentDialog(nonRefillableOnly: true))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                    _ = LoadAsync();
            }
        }

        private async Task LoadAsync()
        {
            try
            {
                _btnRefresh.Enabled = false;

                _rows           = await _service.GetNonRefillableGroupedAsync();
                _dgv.DataSource = _rows;

                if (_rows.Count == 0)
                {
                    _lblSummary.Text      = "No records found.";
                    _lblSummary.ForeColor = Color.FromArgb(100, 100, 100);
                }
                else
                {
                    int totalUnits  = _rows.Sum(r => r.TotalQuantity);
                    // Count distinct groups (ReqId + CartridgeModelId), not fan-out rows
                    int groupCount  = _rows.Select(r => (r.ReqId, r.CartridgeModelId)).Distinct().Count();
                    _lblSummary.Text      = $"{groupCount} group(s)  •  {totalUnits} unit(s) under IT inventory";
                    _lblSummary.ForeColor = Color.FromArgb(39, 174, 96);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError("NonRefillableCartridgesPage.LoadAsync failed", ex);
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }
    }
}
