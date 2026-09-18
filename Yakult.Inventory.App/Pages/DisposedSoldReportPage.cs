using System;
using System.Data;
using System.IO;
using System.Windows.Forms;
using Microsoft.Reporting.WinForms;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Helpers;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages
{
    /// <summary>
    /// Disposed & Sold Items report page.
    /// Lets the user filter by date range and decision type, then
    /// opens the built-in ReportViewer for preview or PDF export.
    /// </summary>
    public class DisposedSoldReportPage : UserControl
    {
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private ComboBox       cboType;
        private Button         btnPreview;
        private Button         btnExportPdf;
        private Label          lblStatus;

        private readonly DisposedSoldReportRepository _repo = new DisposedSoldReportRepository();

        public DisposedSoldReportPage()
        {
            Dock = DockStyle.Fill;
            BuildUi();
        }

        private void BuildUi()
        {
            BackColor = System.Drawing.Color.FromArgb(248, 250, 252);

            // ── Title ────────────────────────────────────────────────────────────
            var lblTitle = new Label
            {
                Text = "Disposed & Sold Items Report",
                Font = new System.Drawing.Font("Segoe UI", 13F, System.Drawing.FontStyle.Bold),
                ForeColor = System.Drawing.Color.FromArgb(27, 58, 107),
                AutoSize = true,
                Location = new System.Drawing.Point(24, 24)
            };

            // ── Filter panel ─────────────────────────────────────────────────────
            var filterPanel = new Panel
            {
                Location  = new System.Drawing.Point(24, 60),
                Size      = new System.Drawing.Size(820, 52),
                BackColor = System.Drawing.Color.White
            };
            filterPanel.Paint += (s, e) =>
            {
                using (var pen = new System.Drawing.Pen(System.Drawing.Color.FromArgb(226, 232, 240)))
                    e.Graphics.DrawRectangle(pen, 0, 0, filterPanel.Width - 1, filterPanel.Height - 1);
            };

            int x = 12;
            filterPanel.Controls.Add(FilterLbl("From:", x)); x += 42;
            dtpFrom = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Location = new System.Drawing.Point(x, 14) };
            dtpFrom.Value = new DateTime(DateTime.Today.Year, 1, 1);
            filterPanel.Controls.Add(dtpFrom); x += 118;

            filterPanel.Controls.Add(FilterLbl("To:", x)); x += 28;
            dtpTo = new DateTimePicker { Format = DateTimePickerFormat.Short, Width = 110, Location = new System.Drawing.Point(x, 14) };
            dtpTo.Value = DateTime.Today;
            filterPanel.Controls.Add(dtpTo); x += 118;

            filterPanel.Controls.Add(FilterLbl("Type:", x)); x += 40;
            cboType = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Width = 120,
                Location = new System.Drawing.Point(x, 14)
            };
            cboType.Items.AddRange(new object[] { "All", "DISPOSE", "SELL" });
            cboType.SelectedIndex = 0;
            filterPanel.Controls.Add(cboType); x += 130;

            btnPreview = new Button
            {
                Text = "Preview Report",
                Location = new System.Drawing.Point(x, 12),
                Size = new System.Drawing.Size(120, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8.5F),
                ForeColor = System.Drawing.Color.White,
                BackColor = System.Drawing.Color.FromArgb(27, 58, 107),
                Cursor = Cursors.Hand
            };
            btnPreview.FlatAppearance.BorderSize = 0;
            btnPreview.Click += (s, e) => RunReport(exportPdf: false);
            filterPanel.Controls.Add(btnPreview); x += 128;

            btnExportPdf = new Button
            {
                Text = "Export PDF",
                Location = new System.Drawing.Point(x, 12),
                Size = new System.Drawing.Size(100, 28),
                FlatStyle = FlatStyle.Flat,
                Font = new System.Drawing.Font("Segoe UI", 8.5F),
                ForeColor = System.Drawing.Color.FromArgb(27, 58, 107),
                BackColor = System.Drawing.Color.FromArgb(239, 246, 255),
                Cursor = Cursors.Hand
            };
            btnExportPdf.FlatAppearance.BorderColor = System.Drawing.Color.FromArgb(191, 219, 254);
            btnExportPdf.FlatAppearance.BorderSize = 1;
            btnExportPdf.Click += (s, e) => RunReport(exportPdf: true);
            filterPanel.Controls.Add(btnExportPdf);

            // ── Status label ─────────────────────────────────────────────────────
            lblStatus = new Label
            {
                AutoSize = true,
                Font = new System.Drawing.Font("Segoe UI", 8.5F),
                ForeColor = System.Drawing.Color.FromArgb(100, 116, 139),
                Location = new System.Drawing.Point(24, 122)
            };

            Controls.Add(lblTitle);
            Controls.Add(filterPanel);
            Controls.Add(lblStatus);
        }

        private static Label FilterLbl(string text, int x) => new Label
        {
            Text = text, AutoSize = true,
            Font = new System.Drawing.Font("Segoe UI", 8.5F),
            ForeColor = System.Drawing.Color.FromArgb(100, 116, 139),
            Location = new System.Drawing.Point(x, 18)
        };

        private void RunReport(bool exportPdf)
        {
            try
            {
                lblStatus.Text = "Loading data...";
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(100, 116, 139);
                Application.DoEvents();

                string typeFilter = cboType.SelectedItem?.ToString();
                if (typeFilter == "All") typeFilter = null;

                var data = _repo.GetReport(
                    from:         dtpFrom.Value.Date,
                    to:           dtpTo.Value.Date,
                    decisionType: typeFilter);

                if (data == null || data.Count == 0)
                {
                    lblStatus.Text = "No records found for the selected filters.";
                    lblStatus.ForeColor = System.Drawing.Color.FromArgb(180, 83, 0);
                    return;
                }

                lblStatus.Text = $"{data.Count} record(s) loaded.";
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(21, 128, 61);

                // Convert to DataTable (ReportViewer requires DataTable or IEnumerable)
                var dt = ToDataTable(data);

                if (exportPdf)
                    ExportToPdf(dt);
                else
                    ShowPreview(dt);
            }
            catch (Exception ex)
            {
                lblStatus.Text = $"Error: {ex.Message}";
                lblStatus.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28);
            }
        }

        private void ShowPreview(DataTable dt)
        {
            var frm = ReportLauncher.CreateCartridgeDisposeSoldReportForm(dt, ReportLauncher.BuildEmptySignatoryTable());
            frm?.ShowDialog(this);
        }

        private static void ExportToPdf(DataTable dt)
        {
            using (var dlg = new SaveFileDialog
            {
                Title      = "Save Report as PDF",
                Filter     = "PDF Files (*.pdf)|*.pdf",
                FileName   = $"DisposedSold_{DateTime.Today:yyyyMMdd}.pdf",
                DefaultExt = "pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;

                byte[] rdlcBytes = ReportLauncher.BuildCartridgeDisposeSoldRdlcBytes(null);
                using (var ms = new System.IO.MemoryStream(rdlcBytes))
                {
                    var localReport = new LocalReport();
                    localReport.LoadReportDefinition(ms);
                    localReport.DataSources.Add(new ReportDataSource("DisposedSoldDataSet", dt));
                    localReport.DataSources.Add(new ReportDataSource("SignatoryData", ReportLauncher.BuildEmptySignatoryTable()));

                    byte[] bytes = localReport.Render(
                        format: "PDF",
                        deviceInfo: null,
                        mimeType: out _,
                        encoding: out _,
                        fileNameExtension: out _,
                        streams: out _,
                        warnings: out _);

                    File.WriteAllBytes(dlg.FileName, bytes);
                }
                MessageBox.Show($"Report saved to:\n{dlg.FileName}", "Export Complete",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// Converts a list of DisposedSoldItemDto to DataTable with column names
        /// that exactly match the RDLC DataSet field names.
        /// </summary>
        private static DataTable ToDataTable(System.Collections.Generic.List<Models.DisposedSoldItemDto> rows)
        {
            var dt = new DataTable();
            dt.Columns.Add("DecisionId",           typeof(int));
            dt.Columns.Add("BatchId",              typeof(int));
            dt.Columns.Add("DecidedAt",             typeof(DateTime));
            dt.Columns.Add("DecisionTypeName",      typeof(string));
            dt.Columns.Add("Quantity",              typeof(int));
            dt.Columns.Add("RecipientName",         typeof(string));
            dt.Columns.Add("SaleAmount",            typeof(decimal));
            dt.Columns.Add("Remarks",               typeof(string));
            dt.Columns.Add("ItemName",              typeof(string));
            dt.Columns.Add("ItemModelNumber",       typeof(string));
            dt.Columns.Add("CategoryName",          typeof(string));
            dt.Columns.Add("ConditionName",         typeof(string));
            dt.Columns.Add("DecidedByName",         typeof(string));
            dt.Columns.Add("CartridgeModelNumber",  typeof(string));
            dt.Columns.Add("CartridgeBrand",        typeof(string));
            dt.Columns.Add("DisposalCompanyName",   typeof(string));
            dt.Columns.Add("VendorName",            typeof(string));

            foreach (var r in rows)
            {
                dt.Rows.Add(
                    r.DecisionId,
                    r.BatchId.HasValue ? (object)r.BatchId.Value : DBNull.Value,
                    r.DecidedAt,
                    r.DecisionTypeName   ?? (object)DBNull.Value,
                    r.Quantity,
                    r.RecipientName      ?? (object)DBNull.Value,
                    r.SaleAmount.HasValue ? (object)r.SaleAmount.Value : DBNull.Value,
                    r.Remarks            ?? (object)DBNull.Value,
                    r.ItemName           ?? (object)DBNull.Value,
                    r.ItemModelNumber    ?? (object)DBNull.Value,
                    r.CategoryName       ?? (object)DBNull.Value,
                    r.ConditionName      ?? (object)DBNull.Value,
                    r.DecidedByName      ?? (object)DBNull.Value,
                    r.CartridgeModelNumber  ?? (object)DBNull.Value,
                    r.CartridgeBrand        ?? (object)DBNull.Value,
                    r.DisposalCompanyName   ?? (object)DBNull.Value,
                    r.VendorName            ?? (object)DBNull.Value);
            }

            return dt;
        }
    }
}
