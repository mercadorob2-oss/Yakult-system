using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;

namespace Yakult.Inventory.App.Pages.Export
{
    internal class ExportSetsPreviewDialog : Form
    {
        // ── Palette: navy / slate / white — professional report ──────────────
        private static readonly Color AccentNavy  = Color.FromArgb( 30,  64, 111);
        private static readonly Color CharcoalText = Color.FromArgb( 33,  37,  41);
        private static readonly Color LightBg     = Color.FromArgb(248, 249, 250);
        private static readonly Color BorderColor  = Color.FromArgb(206, 212, 218);

        private readonly List<SetDto> _sets;
        private readonly Dictionary<int, List<SetDetailRequestDto>> _setItems;
        private WebBrowser _browser;

        internal ExportSetsPreviewDialog(List<SetDto> sets, SetRepository setRepository)
        {
            _sets = sets;
            _setItems = new Dictionary<int, List<SetDetailRequestDto>>();
            InitializeComponentAsync(setRepository);
        }

        private async void InitializeComponentAsync(SetRepository setRepository)
        {
            Text = "Sets Export Preview";
            Size = new Size(1280, 860);
            MinimumSize = new Size(960, 620);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = LightBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            BuildUi();

            // Fetch items for each set
            foreach (var set in _sets)
            {
                try
                {
                    var items = await setRepository.GetSetRequestsAsync(set.SetId);
                    _setItems[set.SetId] = items ?? new List<SetDetailRequestDto>();
                }
                catch
                {
                    _setItems[set.SetId] = new List<SetDetailRequestDto>();
                }
            }

            _browser.DocumentText = BuildPreviewHtml();
        }

        // ── UI ───────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AccentNavy };
            header.Controls.Add(new Label
            {
                Text      = $"Sets Export Preview  —  {_sets.Count} Set(s) selected",
                Font      = new Font("Arial", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            header.Controls.Add(new Label
            {
                Text      = "Review the selected sets below, then click Export to PDF.",
                Font      = new Font("Arial", 9F),
                ForeColor = Color.FromArgb(176, 210, 240),
                AutoSize  = true,
                Location  = new Point(16, 36)
            });

            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.White };
            btnBar.Paint += (s, ev) =>
            {
                using (var p = new Pen(BorderColor))
                    ev.Graphics.DrawLine(p, 0, 0, btnBar.Width, 0);
            };

            var btnCancel = new Button
            {
                Text      = "Cancel",
                Font      = new Font("Arial", 9.5F),
                Size      = new Size(90, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = CharcoalText,
                Cursor    = Cursors.Hand
            };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => Close();

            var btnExport = new Button
            {
                Text      = "Export to PDF",
                Font      = new Font("Arial", 9.5F, FontStyle.Bold),
                Size      = new Size(140, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = AccentNavy,
                ForeColor = Color.White,
                Cursor    = Cursors.Hand
            };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += OnExportToPdf;

            btnBar.Layout += (s, ev) =>
            {
                btnExport.Location = new Point(btnBar.Width - btnExport.Width - 16, (btnBar.Height - btnExport.Height) / 2);
                btnCancel.Location = new Point(btnBar.Width - btnExport.Width - btnCancel.Width - 28, (btnBar.Height - btnCancel.Height) / 2);
            };
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnCancel);

            _browser = new WebBrowser
            {
                Dock                            = DockStyle.Fill,
                ScrollBarsEnabled               = true,
                IsWebBrowserContextMenuEnabled  = false,
                WebBrowserShortcutsEnabled      = false,
                ScriptErrorsSuppressed          = true,
                AllowNavigation                 = false
            };

            Controls.Add(_browser);
            Controls.Add(btnBar);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HTML PREVIEW
        // ════════════════════════════════════════════════════════════════════

        private string BuildPreviewHtml()
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Sets Export Preview</title>
<style>
*{box-sizing:border-box;margin:0;padding:0;}
body{background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#212529;}
.page{background:#fff;margin:20px 24px 30px;border-radius:6px;box-shadow:0 2px 12px rgba(0,0,0,.12);overflow:hidden;}
.rpt-title{background:#1e406f;color:#fff;padding:18px 28px 14px;border-bottom:3px solid #2c6fad;}
.rpt-title h1{font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:700;margin:0 0 4px;letter-spacing:.3px;}
.rpt-title p{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#b0cce4;margin:0;}

/* ── Month section ──────────────────────────────────── */
.month-header{background:#f8f9fa;border-top:2px solid #1e406f;border-bottom:1px solid #dee2e6;padding:10px 24px;margin-top:6px;}
.month-header h2{font-family:Arial,Helvetica,sans-serif;font-size:14px;font-weight:700;color:#1e406f;margin:0;letter-spacing:.3px;}
.month-header .month-count{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-left:10px;font-weight:400;}

/* ── Table styles ───────────────────────────────────── */
.set-table{border-collapse:collapse;width:100%;background:#fff;}
.set-table th{background:#34495e;color:#fff;font-family:Arial,Helvetica,sans-serif;font-weight:700;font-size:10px;text-align:left;padding:8px 10px;white-space:nowrap;letter-spacing:.3px;text-transform:uppercase;border-right:1px solid #4a6274;}
.set-table th:last-child{border-right:none;}
.set-table td{font-family:Arial,Helvetica,sans-serif;padding:6px 10px;border-bottom:1px solid #e9ecef;font-size:11px;vertical-align:top;border-right:1px solid #f0f0f0;}
.set-table td:last-child{border-right:none;}
.set-table tr:nth-child(even) td{background:#f8f9fa;}
.set-table tr.set-border td{border-top:2px solid #dee2e6;}
td.set-code{font-weight:700;color:#1e406f;font-size:11px;}
td.emp-name{font-weight:600;color:#212529;}
td.item-name{font-weight:600;}
td.serial{font-family:Arial,Helvetica,sans-serif;font-size:10px;color:#495057;}
td.qty{text-align:center;font-weight:600;}
td.amount{text-align:right;font-weight:600;}
td.dispatch{text-align:center;white-space:nowrap;}
td.company{font-weight:600;color:#1e406f;}
td.empty-items{font-style:italic;color:#6c757d;padding:6px 10px;}

/* ── Summary footer ─────────────────────────────────── */
.rpt-footer{background:#f8f9fa;border-top:2px solid #dee2e6;padding:12px 24px;font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;}
.rpt-footer strong{color:#212529;}
</style></head><body><div class='page'>
<div class='rpt-title'>
  <h1>Sets Export Report</h1>
  <p>Generated: ");
            sb.Append($"{DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_sets.Count} Set(s)");
            sb.Append("</p></div>");

            // Sort sets by dispatch date (or CreatedAt fallback), then group by month
            var sorted = _sets
                .OrderBy(s => s.DispatchDate ?? s.CreatedAt)
                .ToList();

            var grouped = sorted
                .GroupBy(s =>
                {
                    var dt = s.DispatchDate ?? s.CreatedAt;
                    return new DateTime(dt.Year, dt.Month, 1);
                })
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                string monthLabel = group.Key.ToString("MMMM yyyy");
                int setCount = group.Count();

                // Month section header
                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='month-count'>({setCount} set{(setCount != 1 ? "s" : "")})</span></h2></div>");

                // Table for this month
                sb.Append("<table class='set-table'>");
                sb.Append("<thead><tr>");
                sb.Append("<th>Set Code</th>");
                sb.Append("<th>Employee</th>");
                sb.Append("<th>Item Count</th>");
                sb.Append("<th>Item Name</th>");
                sb.Append("<th>Model</th>");
                sb.Append("<th>Serial #</th>");
                sb.Append("<th>Item Type</th>");
                sb.Append("<th>Category</th>");
                sb.Append("<th>Qty</th>");
                sb.Append("<th>Date Dispatched</th>");
                sb.Append("<th>Amount</th>");
                sb.Append("<th>Company</th>");
                sb.Append("<th>Branch</th>");
                sb.Append("<th>Department</th>");
                sb.Append("</tr></thead><tbody>");

                bool firstSet = true;
                foreach (var set in group)
                {
                    string borderCls = !firstSet ? " class='set-border'" : "";
                    string dispatch = set.DispatchDate.HasValue ? set.DispatchDate.Value.ToString("MM/dd/yyyy") : "";

                    if (!_setItems.TryGetValue(set.SetId, out var items) || items == null || items.Count == 0)
                    {
                        sb.Append($"<tr{borderCls}>");
                        sb.Append($"<td class='set-code'>{Esc(set.SetCode)}</td>");
                        sb.Append($"<td class='emp-name'>{Esc(set.CurrentEmployeeName)}</td>");
                        sb.Append($"<td class='qty'>{set.ItemCount}</td>");
                        sb.Append("<td class='empty-items' colspan='6'>No items found</td>");
                        sb.Append($"<td class='dispatch'>{dispatch}</td>");
                        sb.Append("<td class='amount'>&mdash;</td>");
                        sb.Append($"<td class='company'>{Esc(set.CurrentCompanyName ?? set.Company)}</td>");
                        sb.Append($"<td>{Esc(set.CurrentBranchName)}</td>");
                        sb.Append($"<td>{Esc(set.CurrentDepartmentName)}</td>");
                        sb.Append("</tr>");
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            string rowCls = (firstItem && !firstSet) ? " class='set-border'" : "";
                            sb.Append($"<tr{rowCls}>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='set-code' rowspan='{items.Count}'>{Esc(set.SetCode)}</td>");
                                sb.Append($"<td class='emp-name' rowspan='{items.Count}'>{Esc(set.CurrentEmployeeName)}</td>");
                                sb.Append($"<td class='qty' rowspan='{items.Count}'>{set.ItemCount}</td>");
                            }

                            sb.Append($"<td class='item-name'>{Esc(item.ItemName)}</td>");
                            sb.Append($"<td>{Esc(item.ModelNumber)}</td>");
                            sb.Append($"<td class='serial'>{Esc(item.SerialNumber)}</td>");
                            sb.Append($"<td>{Esc(item.Status)}</td>");
                            sb.Append($"<td>{Esc(item.Category)}</td>");
                            sb.Append($"<td class='qty'>{item.Quantity}</td>");

                            if (firstItem)
                            {
                                sb.Append($"<td class='dispatch' rowspan='{items.Count}'>{dispatch}</td>");
                                sb.Append($"<td class='amount' rowspan='{items.Count}'>&mdash;</td>");
                                sb.Append($"<td class='company' rowspan='{items.Count}'>{Esc(set.CurrentCompanyName ?? set.Company)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(set.CurrentBranchName)}</td>");
                                sb.Append($"<td rowspan='{items.Count}'>{Esc(set.CurrentDepartmentName)}</td>");
                            }

                            sb.Append("</tr>");
                            firstItem = false;
                        }
                    }
                    firstSet = false;
                }

                sb.Append("</tbody></table>");
            }

            // Report footer
            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_sets.Count} set(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Items:</strong> {_sets.Sum(s => s.ItemCount)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Date Range:</strong> ");
            var dates = _sets.Where(s => s.DispatchDate.HasValue).Select(s => s.DispatchDate.Value).ToList();
            if (dates.Count > 0)
                sb.Append($"{dates.Min():MM/dd/yyyy} &ndash; {dates.Max():MM/dd/yyyy}");
            else
                sb.Append("N/A");
            sb.Append("</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        
        // ════════════════════════════════════════════════════════════════════
        //  PDF EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void OnExportToPdf(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog
            {
                Title      = "Save Sets Export",
                Filter     = "PDF files (*.pdf)|*.pdf",
                FileName   = $"Sets_Export_{DateTime.Now:yyyyMMdd_HHmmss}.pdf",
                DefaultExt = "pdf"
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    GenerateSetsPdf(dlg.FileName);
                    Close();
                    System.Diagnostics.Process.Start(dlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Export failed:\n{ex.Message}", "Error",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void GenerateSetsPdf(string outputPath)
        {
            // ── Layout constants ─────────────────────────────────────────────
            const double margin   = 24;
            const double pageW    = 841.89;   // A4 landscape
            const double pageH    = 595.28;
            const double tableW   = pageW - margin * 2;
            const double padX     = 4;
            const double padY     = 2;
            const double hTitle   = 34;    // report title block
            const double hMonth   = 22;    // month section header
            const double hHdr     = 18;    // table header row
            const double hRow     = 14;    // data row
            const double hFooter  = 20;    // report footer

            // Column widths for the 14 columns
            double[] cw = { 58, 82, 38, 110, 72, 68, 52, 62, 28, 62, 44, 72, 62, 62 };
            { double s = cw.Sum(); for (int i = 0; i < cw.Length; i++) cw[i] = cw[i] / s * tableW; }

            // Fonts — all Arial
            var fTitle     = new XFont("Arial", 14, XFontStyle.Bold);
            var fSubtitle  = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fMonth     = new XFont("Arial", 10, XFontStyle.Bold);
            var fMonthCnt  = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fHdr       = new XFont("Arial",  6.5, XFontStyle.Bold);
            var fCell      = new XFont("Arial",  7, XFontStyle.Regular);
            var fCellBold  = new XFont("Arial",  7, XFontStyle.Bold);
            var fFooter    = new XFont("Arial",  8, XFontStyle.Regular);
            var fFooterB   = new XFont("Arial",  8, XFontStyle.Bold);

            // Brushes & pens
            var bNavy      = new XSolidBrush(XColor.FromArgb( 30,  64, 111));
            var bMidBlue   = new XSolidBrush(XColor.FromArgb( 44, 111, 173));
            var bSlate     = new XSolidBrush(XColor.FromArgb( 52,  73,  94));
            var bMonthBg   = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bAlt       = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bBlack     = new XSolidBrush(XColor.FromArgb( 33,  37,  41));
            var bGray      = new XSolidBrush(XColor.FromArgb(108, 117, 125));
            var bSubFg     = new XSolidBrush(XColor.FromArgb(176, 204, 228));
            var bFooterBg  = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var pBorder    = new XPen(XColor.FromArgb(206, 212, 218), 0.4);
            var pStrong    = new XPen(XColor.FromArgb(108, 117, 125), 0.6);
            var pSetBorder = new XPen(XColor.FromArgb(206, 212, 218), 1.0);
            var pMonthTop  = new XPen(XColor.FromArgb( 30,  64, 111), 1.5);

            // ── Document & page state ────────────────────────────────────────
            var doc = new PdfDocument { Info = { Title = "Sets Export Report", Author = "Yakult.Inventory.App" } };
            PdfPage   page = null;
            XGraphics gfx  = null;
            double    y    = 0;

            double Bottom() => pageH - margin;

            void NewPage()
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size        = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y   = margin;
            }

            double ColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += cw[i]; return x; }

            void OuterV(double ry, double rh)
            {
                gfx.DrawLine(pStrong, margin,          ry, margin,          ry + rh);
                gfx.DrawLine(pStrong, margin + tableW, ry, margin + tableW, ry + rh);
            }

            void EmitColHeaders()
            {
                if (y + hHdr > Bottom()) NewPage();
                string[] names = { "SET CODE", "EMPLOYEE", "ITEMS", "ITEM NAME", "MODEL",
                                   "SERIAL #", "ITEM TYPE", "CATEGORY", "QTY", "DISPATCHED",
                                   "AMOUNT", "COMPANY", "BRANCH", "DEPARTMENT" };
                bool[]   right = { false, false, true, false, false,
                                   false, false, false, true, false,
                                   true, false, false, false };
                gfx.DrawRectangle(bSlate, margin, y, tableW, hHdr);
                for (int c = 0; c < names.Length; c++)
                {
                    var r = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hHdr - padY * 2);
                    gfx.DrawString(names[c], fHdr, XBrushes.White, r,
                        right[c] ? XStringFormats.CenterRight : XStringFormats.CenterLeft);
                    if (c < names.Length - 1)
                        gfx.DrawLine(new XPen(XColor.FromArgb(74, 98, 116), 0.3), ColX(c + 1), y, ColX(c + 1), y + hHdr);
                }
                OuterV(y, hHdr);
                gfx.DrawLine(pStrong, margin, y + hHdr, margin + tableW, y + hHdr);
                y += hHdr;
            }

            string TrimText(string text, XFont f, double maxW)
            {
                if (string.IsNullOrEmpty(text) || maxW <= 0) return "";
                if (gfx.MeasureString(text, f).Width <= maxW) return text;
                const string ell = "...";
                double ew = gfx.MeasureString(ell, f).Width;
                for (int i = text.Length - 1; i > 0; i--)
                { var t = text.Substring(0, i); if (gfx.MeasureString(t, f).Width + ew <= maxW) return t + ell; }
                return ell;
            }

            // ── First page — report title ────────────────────────────────────
            NewPage();
            gfx.DrawRectangle(bNavy,    margin, y, tableW, hTitle);
            gfx.DrawRectangle(bMidBlue, margin, y + hTitle, tableW, 3);
            gfx.DrawString("Sets Export Report", fTitle, XBrushes.White,
                new XRect(margin + 10, y, tableW - 10, hTitle * 0.62), XStringFormats.BottomLeft);
            string sub = $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_sets.Count} Set(s)";
            gfx.DrawString(sub, fSubtitle, bSubFg,
                new XRect(margin + 10, y + hTitle * 0.62, tableW - 10, hTitle * 0.38), XStringFormats.TopLeft);
            y += hTitle + 3 + 6;

            // ── Sort & group by month ────────────────────────────────────────
            var sorted = _sets
                .OrderBy(s => s.DispatchDate ?? s.CreatedAt)
                .ToList();

            var grouped = sorted
                .GroupBy(s =>
                {
                    var dt = s.DispatchDate ?? s.CreatedAt;
                    return new DateTime(dt.Year, dt.Month, 1);
                })
                .OrderBy(g => g.Key);

            int rowIdx = 0;

            foreach (var group in grouped)
            {
                // ── Month header ──
                if (y + hMonth + hHdr + hRow > Bottom()) NewPage();

                gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                gfx.DrawRectangle(bMonthBg, margin, y, tableW, hMonth);
                string monthLabel = group.Key.ToString("MMMM yyyy");
                int setCount = group.Count();
                gfx.DrawString(monthLabel, fMonth, bNavy,
                    new XRect(margin + 10, y, tableW * 0.5, hMonth), XStringFormats.CenterLeft);
                gfx.DrawString($"({setCount} set{(setCount != 1 ? "s" : "")})", fMonthCnt, bGray,
                    new XRect(margin + 10 + gfx.MeasureString(monthLabel, fMonth).Width + 8, y, 100, hMonth), XStringFormats.CenterLeft);
                gfx.DrawLine(pBorder, margin, y + hMonth, margin + tableW, y + hMonth);
                OuterV(y, hMonth);
                y += hMonth;

                EmitColHeaders();

                bool firstSetInGroup = true;
                foreach (var set in group)
                {
                    string dispatch = set.DispatchDate.HasValue ? set.DispatchDate.Value.ToString("MM/dd/yyyy") : "";

                    if (!_setItems.TryGetValue(set.SetId, out var items) || items == null || items.Count == 0)
                    {
                        if (y + hRow > Bottom()) { NewPage(); EmitColHeaders(); }

                        // Set separator line
                        if (!firstSetInGroup)
                            gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

                        if (rowIdx % 2 == 1)
                            gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);

                        string[] vals = {
                            set.SetCode ?? "",
                            set.CurrentEmployeeName ?? "",
                            set.ItemCount.ToString(),
                            "No items found",
                            "", "", "", "", "",
                            dispatch, "\u2014",
                            set.CurrentCompanyName ?? set.Company ?? "",
                            set.CurrentBranchName ?? "",
                            set.CurrentDepartmentName ?? ""
                        };
                        bool[] rAlign = { false, false, true, false, false,
                                          false, false, false, false, false,
                                          true, false, false, false };

                        for (int c = 0; c < vals.Length; c++)
                        {
                            var rect = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hRow - padY * 2);
                            var fmt  = rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft;
                            var font = (c == 0) ? fCellBold : (c == 3 ? fCell : fCell);
                            var brush = (c == 0) ? bNavy : (c == 3 ? bGray : bBlack);
                            gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, fmt);
                        }

                        gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                        for (int c = 1; c < cw.Length; c++)
                            gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + hRow);
                        OuterV(y, hRow);

                        y += hRow;
                        rowIdx++;
                    }
                    else
                    {
                        bool firstItem = true;
                        foreach (var item in items)
                        {
                            if (y + hRow > Bottom()) { NewPage(); EmitColHeaders(); }

                            // Set separator line
                            if (firstItem && !firstSetInGroup)
                                gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

                            if (rowIdx % 2 == 1)
                                gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);

                            string[] vals = {
                                firstItem ? (set.SetCode ?? "") : "",
                                firstItem ? (set.CurrentEmployeeName ?? "") : "",
                                firstItem ? set.ItemCount.ToString() : "",
                                item.ItemName ?? "",
                                item.ModelNumber ?? "",
                                item.SerialNumber ?? "",
                                item.Status ?? "",
                                item.Category ?? "",
                                item.Quantity.ToString(),
                                firstItem ? dispatch : "",
                                "\u2014",
                                firstItem ? (set.CurrentCompanyName ?? set.Company ?? "") : "",
                                firstItem ? (set.CurrentBranchName ?? "") : "",
                                firstItem ? (set.CurrentDepartmentName ?? "") : ""
                            };
                            bool[] rAlign = { false, false, true, false, false,
                                              false, false, false, true, false,
                                              true, false, false, false };

                            for (int c = 0; c < vals.Length; c++)
                            {
                                var rect = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hRow - padY * 2);
                                var fmt  = rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft;
                                var font = (c == 0 && firstItem) ? fCellBold : ((c == 1 && firstItem) ? fCellBold : ((c == 11 && firstItem) ? fCellBold : fCell));
                                var brush = (c == 0 && firstItem) ? bNavy : ((c == 11 && firstItem) ? bNavy : bBlack);
                                gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, fmt);
                            }

                            gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                            for (int c = 1; c < cw.Length; c++)
                                gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + hRow);
                            OuterV(y, hRow);

                            y += hRow;
                            rowIdx++;
                            firstItem = false;
                        }
                    }
                    firstSetInGroup = false;
                }

                // Close outer border at bottom of month section
                gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
            }

            // ── Report footer ────────────────────────────────────────────────
            if (y + hFooter > Bottom()) NewPage();
            gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
            gfx.DrawLine(pSetBorder, margin, y, margin + tableW, y);

            var dates = _sets.Where(s => s.DispatchDate.HasValue).Select(s => s.DispatchDate.Value).ToList();
            string dateRange = dates.Count > 0
                ? $"{dates.Min():MM/dd/yyyy} \u2013 {dates.Max():MM/dd/yyyy}"
                : "N/A";
            string footerText = $"Total: {_sets.Count} set(s)   \u00B7   Total Items: {_sets.Sum(s => s.ItemCount)}   \u00B7   Date Range: {dateRange}";

            double fx = margin + 10;
            gfx.DrawString("Total: ", fFooterB, bBlack,
                new XRect(fx, y, 40, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total: ", fFooterB).Width;
            string totalText = $"{_sets.Count} set(s)   \u00B7   ";
            gfx.DrawString(totalText, fFooter, bGray,
                new XRect(fx, y, 120, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(totalText, fFooter).Width;
            gfx.DrawString("Total Items: ", fFooterB, bBlack,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total Items: ", fFooterB).Width;
            string itemsText = $"{_sets.Sum(s => s.ItemCount)}   \u00B7   ";
            gfx.DrawString(itemsText, fFooter, bGray,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(itemsText, fFooter).Width;
            gfx.DrawString("Date Range: ", fFooterB, bBlack,
                new XRect(fx, y, 80, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Date Range: ", fFooterB).Width;
            gfx.DrawString(dateRange, fFooter, bGray,
                new XRect(fx, y, 150, hFooter), XStringFormats.CenterLeft);

            doc.Save(outputPath);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string Esc(string s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
