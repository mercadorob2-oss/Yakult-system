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

namespace Yakult.Inventory.App.Pages.Cartridge
{
    /// <summary>
    /// Read-only audit page for non-refillable empty cartridges that have been
    /// closed (Sold or Disposed) by IT.  No inventory or status changes are
    /// possible from this page — it is purely informational.
    ///
    /// Instantiate with status = "Sold" or status = "Disposed".
    /// </summary>
    public class ClosedCartridgesPage : UserControl
    {
        private readonly string _status;   // "Sold" | "Disposed"

        private DataGridView _dgv;
        private Button       _btnRefresh;
        private Label        _lblSummary;

        private readonly CartridgeRefillService _service;
        private List<ClosedEmptyCartridgeDto>   _rows;

        public ClosedCartridgesPage(string status)
        {
            _status  = status;
            _service = new CartridgeRefillService();
            _rows    = new List<ClosedEmptyCartridgeDto>();
            BuildUi();
            _ = LoadAsync();
        }

        private void BuildUi()
        {
            Dock      = DockStyle.Fill;
            BackColor = Color.White;

            const int headerHeight = 68;
            Color headerColor = Color.FromArgb(0, 150, 136); // same teal as all other cartridge pages

            // ── Header ───────────────────────────────────────────────────────
            var header = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = headerHeight,
                BackColor = headerColor
            };
            header.Controls.Add(new Label
            {
                Text      = _status == "Disposed"
                    ? "Disposed Empty Cartridges"
                    : "Sold Empty Cartridges",
                Font      = new Font("Segoe UI", 16F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(15, 8)
            });
            header.Controls.Add(new Label
            {
                Text      = _status == "Disposed"
                    ? "Non-refillable cartridges disposed of by IT  •  Read-only audit view"
                    : "Non-refillable cartridges sold by IT  •  Read-only audit view",
                Font      = new Font("Segoe UI", 9F),
                ForeColor = Color.FromArgb(230, 230, 230),
                AutoSize  = true,
                Location  = new Point(15, 44)
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

            _lblSummary = new Label
            {
                Text      = "",
                Font      = new Font("Segoe UI", 9F, FontStyle.Italic),
                ForeColor = Color.FromArgb(100, 100, 100),
                AutoSize  = true,
                Location  = new Point(120, 13)
            };

            actionBar.Controls.Add(_btnRefresh);
            actionBar.Controls.Add(_lblSummary);

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
            _dgv.ColumnHeadersDefaultCellStyle.BackColor         = headerColor;
            _dgv.ColumnHeadersDefaultCellStyle.ForeColor         = Color.White;
            _dgv.ColumnHeadersDefaultCellStyle.Font              = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _dgv.ColumnHeadersDefaultCellStyle.Padding           = new Padding(5);
            _dgv.AlternatingRowsDefaultCellStyle.BackColor       = Color.FromArgb(248, 248, 248);
            _dgv.DefaultCellStyle.SelectionBackColor             = headerColor;
            _dgv.DefaultCellStyle.SelectionForeColor             = Color.White;
            _dgv.DefaultCellStyle.Font                           = new Font("Segoe UI", 9F);

            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "EmptyCartridgeId",
                HeaderText       = "ID",
                Width            = 65,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "CartridgeModel",
                HeaderText       = "Cartridge Model",
                FillWeight       = 30
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Quantity",
                HeaderText       = "Qty",
                Width            = 60,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReturnedAt",
                HeaderText       = "Returned At",
                Width            = 135
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ClosedAt",
                HeaderText       = _status == "Disposed" ? "Disposed At" : "Sold At",
                Width            = 135
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ReqId",
                HeaderText       = "Req ID",
                Width            = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter }
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "DisposalCompanyName",
                HeaderText       = "Disposal Company",
                FillWeight       = 25
            });
            _dgv.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Remarks",
                HeaderText       = "Remarks",
                FillWeight       = 45
            });

            _dgv.CellFormatting += (s, e) =>
            {
                if (e.RowIndex < 0) return;
                string col = _dgv.Columns[e.ColumnIndex].DataPropertyName;

                if ((col == "ReturnedAt" || col == "ClosedAt") && e.Value is DateTime dt)
                    e.Value = dt.ToString("yyyy-MM-dd HH:mm");

                if ((col == "ReqId" || col == "DisposalCompanyName") &&
                    (e.Value == null || e.Value == DBNull.Value || e.Value.ToString() == ""))
                    e.Value = "—";
            };

            // Docking order: Fill first, then Top panels
            Controls.Add(_dgv);
            Controls.Add(actionBar);
            Controls.Add(header);
        }

        private async Task LoadAsync()
        {
            try
            {
                _btnRefresh.Enabled = false;

                _rows = _status == "Disposed"
                    ? await _service.GetDisposedEmptiesAsync()
                    : await _service.GetSoldEmptiesAsync();

                _dgv.DataSource = _rows;

                if (_rows.Count == 0)
                {
                    _lblSummary.Text      = "No records found.";
                    _lblSummary.ForeColor = Color.FromArgb(100, 100, 100);
                }
                else
                {
                    int totalUnits = _rows.Sum(r => r.Quantity);
                    _lblSummary.Text      = $"{_rows.Count} record(s)  •  {totalUnits} unit(s)";
                    _lblSummary.ForeColor = Color.FromArgb(39, 174, 96);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Logger.LogError($"ClosedCartridgesPage({_status}).LoadAsync failed", ex);
            }
            finally
            {
                _btnRefresh.Enabled = true;
            }
        }
    }
}
