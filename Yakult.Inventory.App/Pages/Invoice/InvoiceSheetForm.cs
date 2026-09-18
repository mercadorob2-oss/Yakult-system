using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Yakult.Inventory.App.Dialogs;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Invoice
{
    /// <summary>
    /// Interactive DataGridView preview of the Invoice Report data.
    /// Users resize/reorder/show-hide columns here, then click Export PDF to produce a
    /// ReportViewer-rendered PDF whose TablixColumn widths reflect those edits.
    /// Column reorder is supported for preview convenience; PDF column order always
    /// follows the original InvoiceReport.rdl structure.
    /// </summary>
    public class InvoiceSheetForm : Form
    {
        // Each entry maps index i → TablixColumn[i] in InvoiceReport.rdl.
        private static readonly SheetColumn[] ColumnDefs =
        {
            new SheetColumn("DATE",        "DocumentDate",    95,  "yyyy-MM-dd"),
            new SheetColumn("COMPANY",     "CompanyName",     85,  null),
            new SheetColumn("DOCUMENT #",  "DocumentNumber",  115, null),
            new SheetColumn("REFERENCE #", "ReferenceNumber", 100, null),
            new SheetColumn("STATUS",      "Status",           75, null),
            new SheetColumn("QTY",         "Quantity",         70, "0.##"),
            new SheetColumn("ITEM NAME",   "ItemName",        148, null),
            new SheetColumn("START DATE",  "StartDate",        95, "yyyy-MM-dd"),
            new SheetColumn("END DATE",    "EndDate",          95, "yyyy-MM-dd"),
            new SheetColumn("LINE TOTAL",  "LineTotal",        88, "#,0.00"),
            new SheetColumn("SUBTOTAL",    "Subtotal",         88, "#,0.00"),
            new SheetColumn("SITE",        "Site",            145, null),
        };

        // Parallel sample values shown in the column picker cards.
        private static readonly string[] ColumnSamples =
        {
            "2024-01-15",
            "Yakult Phils.",
            "INV-2024-001",
            "REF-001",
            "Active",
            "1.00",
            "HP LaserJet Pro",
            "2024-01-01",
            "2025-01-01",
            "₱5,200.00",
            "₱5,200.00",
            "Manila - Main",
        };

        private DataGridView _dgv;
        private readonly IEnumerable<string> _setCodes;

        public InvoiceSheetForm(IEnumerable<string> setCodes = null)
        {
            _setCodes = setCodes;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = "Invoice Sheet Preview";
            Size = new Size(1300, 740);
            MinimumSize = new Size(900, 520);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = new Font("Segoe UI", 9f);

            var toolbar = BuildToolbar();

            _dgv = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
                AllowUserToOrderColumns = true,
                RowHeadersVisible = false,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                GridColor = Color.FromArgb(220, 220, 220),
                ScrollBars = ScrollBars.Both,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(52, 152, 219),
                    ForeColor = Color.White,
                    Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                    Alignment = DataGridViewContentAlignment.MiddleCenter
                },
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Font = new Font("Segoe UI", 8.5f),
                    SelectionBackColor = Color.FromArgb(210, 225, 255),
                    SelectionForeColor = Color.Black,
                    Padding = new Padding(4, 2, 4, 2)
                },
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.FromArgb(232, 240, 248),
                    SelectionBackColor = Color.FromArgb(210, 225, 255),
                    SelectionForeColor = Color.Black
                }
            };

            BuildColumns();

            Controls.Add(_dgv);
            Controls.Add(toolbar);
        }

        private Panel BuildToolbar()
        {
            var toolbar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50,
                BackColor = Color.FromArgb(245, 247, 250),
                Padding = new Padding(10, 0, 10, 0)
            };

            var btnExport = MakeToolbarButton("📄  Export PDF",
                Color.FromArgb(52, 152, 219), Color.FromArgb(41, 128, 185));
            btnExport.Click += BtnExport_Click;

            var btnColumns = MakeToolbarButton("⚙  Columns",
                Color.FromArgb(108, 117, 125), Color.FromArgb(82, 90, 97));
            btnColumns.Location = new Point(btnExport.Right + 8, 10);
            btnColumns.Click += BtnColumns_Click;

            var lblHint = new Label
            {
                AutoSize = false,
                Text = "Resize columns to adjust PDF layout. Column order affects preview only.",
                Width = 460,
                Height = 30,
                Location = new Point(btnColumns.Right + 14, 12),
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = Color.FromArgb(100, 100, 100),
                TextAlign = ContentAlignment.MiddleLeft
            };

            toolbar.Controls.AddRange(new Control[] { btnExport, btnColumns, lblHint });
            return toolbar;
        }

        private static Button MakeToolbarButton(string text, Color back, Color hover)
        {
            var btn = new Button
            {
                Text = text,
                Size = new Size(138, 30),
                FlatStyle = FlatStyle.Flat,
                BackColor = back,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                Cursor = Cursors.Hand,
                Location = new Point(10, 10)
            };
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.MouseOverBackColor = hover;
            return btn;
        }

        private void BuildColumns()
        {
            _dgv.Columns.Clear();
            foreach (var def in ColumnDefs)
            {
                var col = new DataGridViewTextBoxColumn
                {
                    HeaderText = def.Header,
                    DataPropertyName = def.DataField,
                    Width = def.DefaultWidth,
                    MinimumWidth = 30,
                    Resizable = DataGridViewTriState.True,
                    SortMode = DataGridViewColumnSortMode.Automatic
                };
                if (!string.IsNullOrEmpty(def.Format))
                    col.DefaultCellStyle = new DataGridViewCellStyle { Format = def.Format };
                _dgv.Columns.Add(col);
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // _setCodes == null means "nothing checked" (show every invoice); a non-null
                // but empty collection means rows were checked that had no resolvable SetCode,
                // which must still filter down to zero rows rather than showing everything.
                DataTable dt = _setCodes != null
                    ? ReportLauncher.LoadInvoiceData(_setCodes)
                    : ReportLauncher.LoadInvoiceData();

                _dgv.DataSource = dt;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to load invoice data:\n" + ex.Message,
                    "Load Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Export ───────────────────────────────────────────────────────────

        private void BtnExport_Click(object sender, EventArgs e)
        {
            if (_dgv.DataSource == null)
            {
                MessageBox.Show(
                    "No data loaded. Cannot export.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var sigData = ReportLauncher.ShowSignatoryPicker();
            if (sigData == null) return;

            // Collect widths in original column definition order (0-11).
            // Hidden columns pass 0 so the helper collapses them in the RDLC.
            int[] widths = Enumerable.Range(0, _dgv.Columns.Count)
                .Select(i => _dgv.Columns[i].Visible
                    ? Math.Max(30, _dgv.Columns[i].Width)
                    : 0)
                .ToArray();

            string tempRdl = null;
            try
            {
                tempRdl = InvoiceSheetRdlHelper.CreateTempRdlWithWidths(widths);

                var sources = new List<(string, DataTable)>
                {
                    ("InvoiceData",   (DataTable)_dgv.DataSource),
                    ("SignatoryData", sigData)
                };

                var form = new ReportViewerForm(tempRdl, "Invoice Sheet Report", sources);
                form.FormClosed += (s, _) =>
                {
                    if (tempRdl != null && File.Exists(tempRdl))
                        try { File.Delete(tempRdl); } catch { }
                };
                tempRdl = null; // ownership transferred to FormClosed handler
                form.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Export failed:\n" + ex.Message,
                    "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);

                if (tempRdl != null && File.Exists(tempRdl))
                    try { File.Delete(tempRdl); } catch { }
            }
        }

        // ── Column picker ────────────────────────────────────────────────────

        private void BtnColumns_Click(object sender, EventArgs e)
        {
            // Build column defs using the current DGV visibility as the checked state.
            var colDefs = ColumnDefs
                .Select((def, i) => new ReportColumnDef(
                    "Show" + i.ToString(),
                    def.Header,
                    _dgv.Columns[i].Visible,
                    fitsPortrait: true,
                    sampleValue: ColumnSamples[i]))
                .ToArray();

            var result = ReportColumnSelectionDialog.Show(this, "Invoice Sheet Columns", colDefs);
            if (result == null) return;

            for (int i = 0; i < result.Length && i < _dgv.Columns.Count; i++)
            {
                bool visible = string.Equals(
                    result[i].Values[0], "true", StringComparison.OrdinalIgnoreCase);
                _dgv.Columns[i].Visible = visible;
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private sealed class SheetColumn
        {
            public SheetColumn(string header, string dataField, int defaultWidth, string format)
            {
                Header = header;
                DataField = dataField;
                DefaultWidth = defaultWidth;
                Format = format;
            }

            public string Header { get; }
            public string DataField { get; }
            public int DefaultWidth { get; }
            public string Format { get; }
        }
    }
}
