using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Helpers;

namespace Yakult.Inventory.App.Pages.Export
{
    internal class ExportInvoicePreviewDialog : Form
    {
        // ── Palette: navy / slate / white — professional report ────────────
        private static readonly Color AccentRed   = Color.FromArgb( 30,  64, 111);  // navy (header/buttons)
        private static readonly Color NearBlack   = Color.FromArgb( 33,  37,  41);  // charcoal body text
        private static readonly Color DirtyWhite  = Color.FromArgb(248, 249, 250);  // light gray background
        private static readonly Color BorderColor = Color.FromArgb(206, 212, 218);  // slate border

        private readonly List<DataRow> _headers;
        private readonly DataTable     _items;
        private WebBrowser _browser;

        internal ExportInvoicePreviewDialog(List<DataRow> headers, DataTable items)
        {
            _headers = headers;
            _items   = items;
            Text            = "Invoice Export Preview";
            Size            = new Size(1280, 860);
            MinimumSize     = new Size(960, 620);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = DirtyWhite;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;
            BuildUi();
        }

        protected override void OnLoad(EventArgs e) { base.OnLoad(e); _browser.DocumentText = BuildPreviewHtml(); }

        // ── UI ───────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AccentRed };
            header.Controls.Add(new Label { Text = $"Export Preview  \u2014  {_headers.Count} Invoice(s)  |  {_items.Rows.Count} Item(s)",
                Font = new Font("Arial", 13F, FontStyle.Bold), ForeColor = Color.White, AutoSize = true, Location = new Point(16, 10) });
            header.Controls.Add(new Label { Text = "Invoices are grouped by month. Review then click Export to PDF.",
                Font = new Font("Arial", 9F), ForeColor = Color.FromArgb(180, 210, 240), AutoSize = true, Location = new Point(16, 36) });

            var btnBar = new Panel { Dock = DockStyle.Bottom, Height = 54, BackColor = Color.White };
            btnBar.Paint += (s, ev) => { using (var p = new Pen(BorderColor)) ev.Graphics.DrawLine(p, 0, 0, btnBar.Width, 0); };

            var btnCancel = new Button { Text = "Cancel", Font = new Font("Arial", 9.5F), Size = new Size(90, 34),
                FlatStyle = FlatStyle.Flat, BackColor = Color.FromArgb(226, 232, 240), ForeColor = Color.FromArgb( 33,  37,  41), Cursor = Cursors.Hand };
            btnCancel.FlatAppearance.BorderSize = 0;
            btnCancel.Click += (s, ev) => Close();

            var btnExport = new Button { Text = "Export to PDF", Font = new Font("Arial", 9.5F, FontStyle.Bold),
                Size = new Size(140, 34), FlatStyle = FlatStyle.Flat, BackColor = AccentRed, ForeColor = Color.White, Cursor = Cursors.Hand };
            btnExport.FlatAppearance.BorderSize = 0;
            btnExport.Click += OnExportToPdf;

            btnBar.Layout += (s, ev) =>
            {
                btnExport.Location = new Point(btnBar.Width - btnExport.Width - 16, (btnBar.Height - btnExport.Height) / 2);
                btnCancel.Location = new Point(btnBar.Width - btnExport.Width - btnCancel.Width - 28, (btnBar.Height - btnCancel.Height) / 2);
            };
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnCancel);

            _browser = new WebBrowser { Dock = DockStyle.Fill, ScrollBarsEnabled = true,
                IsWebBrowserContextMenuEnabled = false, WebBrowserShortcutsEnabled = false,
                ScriptErrorsSuppressed = true, AllowNavigation = false };

            Controls.Add(_browser);
            Controls.Add(btnBar);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════════
        //  HTML PREVIEW (red / black / dirty-white palette)
        // ════════════════════════════════════════════════════════════════════

        private string BuildPreviewHtml()
        {
            var sorted = SortedHeaders();
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Invoice Export Preview</title>
<style>
*{box-sizing:border-box;margin:0;padding:0;}
body{background:#f0f2f5;font-family:Arial,sans-serif;font-size:12px;color:#212529;}
.page{background:#fff;margin:20px 24px 30px;border-radius:4px;box-shadow:0 1px 8px rgba(0,0,0,.10);overflow:hidden;}
table{width:100%;border-collapse:collapse;}
tr.yr-row td{background:#1e406f;color:#fff;font-size:13px;font-weight:700;text-align:center;padding:7px 0;letter-spacing:2px;}
tr.mo-row td{background:#2c6fad;color:#fff;font-size:11.5px;font-weight:700;text-align:center;padding:5px 0;letter-spacing:1px;}
tr.col-hdr td{background:#34495e;color:#fff;font-size:10.5px;font-weight:700;padding:8px;white-space:nowrap;border-right:1px solid #2c3e50;}
tr.col-hdr td.r{text-align:right;}tr.col-hdr td:last-child{border-right:none;}
tbody tr.item{border-bottom:1px solid #dee2e6;}
tbody tr.item:nth-child(even){background:#f8f9fa;}
td{padding:7px 8px;font-size:11px;vertical-align:top;border-right:1px solid #dee2e6;color:#212529;}
td:last-child{border-right:none;}td.r{text-align:right;}td.wrap{white-space:normal;}
td.shared{border-right:2px solid #adb5bd;}
tr.sum-row td{padding:5px 8px;font-size:11px;border-bottom:1px solid #dee2e6;}
tr.sum-row td.sum-lbl{text-align:right;font-weight:600;color:#495057;padding-right:14px;}
tr.sum-row td.sum-val{text-align:right;font-weight:700;color:#212529;}
tr.sum-row.total-row td.sum-lbl,tr.sum-row.total-row td.sum-val{background:#ebf5fb;font-size:12px;color:#1e406f;font-weight:800;border-top:1px solid #2c6fad;}
tr.sep-row td{background:#2c6fad;padding:4px 0;border:none;}
.badge{display:inline-block;padding:2px 8px;border-radius:3px;font-size:10px;font-weight:700;white-space:nowrap;}
.b-active{background:#d4edda;color:#155724;}.b-pending{background:#fff3cd;color:#856404;}
.b-inactive{background:#f8d7da;color:#721c24;}.b-default{background:#e9ecef;color:#495057;}
.rpt-title{background:#1e406f;color:#fff;padding:16px 24px 12px;border-bottom:3px solid #2c6fad;}
.rpt-title h1{font-size:20px;font-weight:700;margin:0 0 3px;letter-spacing:.3px;}
.rpt-title p{font-size:10.5px;color:#b0cce4;margin:0;}
</style></head><body><div class='page'>
<div class='rpt-title'>
  <h1>Invoice Report</h1>
  <p>Generated: ");
            sb.Append($"{DateTime.Now:MMMM dd, yyyy  h:mm tt}  \u00a0|\u00a0  {_headers.Count} Invoice(s)  \u00a0|\u00a0  {_items.Rows.Count} Item(s)");
            sb.Append(@"</p>
</div>
<table>");

            const int COLS = 10;
            int prevYear = -1, prevMonth = -1;

            foreach (var hdr in sorted)
            {
                object dvRaw = hdr["InvoiceDate"];
                DateTime? invDate = (dvRaw != null && dvRaw != DBNull.Value) ? (DateTime?)Convert.ToDateTime(dvRaw) : null;
                int year = invDate?.Year ?? 0, month = invDate?.Month ?? 0;

                if (year != prevYear)
                {
                    sb.Append($"<tr class='yr-row'><td colspan='{COLS}'>{(year > 0 ? year.ToString() : "Unknown")}</td></tr>");
                    prevYear = year; prevMonth = -1;
                }
                if (month != prevMonth)
                {
                    string mn = invDate.HasValue ? invDate.Value.ToString("MMMM") : "Unknown";
                    sb.Append($"<tr class='mo-row'><td colspan='{COLS}'>{mn}</td></tr>");
                    sb.Append("<tr class='col-hdr'><td>DATE</td><td>COMPANY</td><td>DOCUMENT #</td><td>STATUS</td><td>REFERENCE #</td><td class='r'>QTY</td><td>ITEM NAME</td><td>DURATION</td><td class='r'>AMOUNT</td><td>SITE</td></tr>");
                    prevMonth = month;
                }

                string setCode   = hdr["SetCode"]?.ToString()        ?? "";
                string company   = hdr["CompanyName"]?.ToString()    ?? "";
                string docNum    = hdr["DocumentNumber"]?.ToString()  ?? "";
                string refNum    = hdr["ReferenceNumber"]?.ToString() ?? "";
                string site      = hdr["Site"]?.ToString()           ?? "";
                string statusRaw = hdr["Status"]?.ToString()         ?? "";
                string dateStr   = invDate.HasValue ? invDate.Value.ToString("M/d/yyyy") : "";

                var groupItems = new List<DataRow>();
                foreach (DataRow it in _items.Rows)
                    if (string.Equals(ColVal(it, "SetCode"), setCode, StringComparison.OrdinalIgnoreCase))
                        groupItems.Add(it);

                int span = Math.Max(1, groupItems.Count);

                if (groupItems.Count == 0)
                {
                    sb.Append("<tr class='item'>");
                    sb.Append($"<td>{Esc(dateStr)}</td><td>{Esc(company)}</td><td>{Esc(docNum)}</td>");
                    sb.Append(BadgeTd(statusRaw, ""));
                    sb.Append($"<td>{Esc(refNum)}</td><td colspan='4' style='color:#94a3b8;font-style:italic'>No line items</td><td>{Esc(site)}</td></tr>");
                }
                else
                {
                    bool isFirst = true;
                    foreach (var it in groupItems)
                    {
                        sb.Append("<tr class='item'>");
                        if (isFirst)
                        {
                            string rs = span > 1 ? $" rowspan='{span}'" : "";
                            sb.Append($"<td{rs} class='shared'>{Esc(dateStr)}</td>");
                            sb.Append($"<td{rs} class='shared'>{Esc(company)}</td>");
                            sb.Append($"<td{rs} class='shared'>{Esc(docNum)}</td>");
                            sb.Append(BadgeTd(statusRaw, rs + " class='shared'"));
                            sb.Append($"<td{rs} class='shared'>{Esc(refNum)}</td>");
                        }
                        sb.Append($"<td class='r'>{Esc(FmtDR(it, "Quantity", "N0"))}</td>");
                        sb.Append($"<td class='wrap'>{Esc(ColVal(it, "ItemName"))}</td>");
                        var sd = SafeDate(it, "LineStartDate"); var ed = SafeDate(it, "LineEndDate");
                        string dur = (sd.HasValue && ed.HasValue) ? $"{sd.Value:yyyy-MM-dd}&nbsp;&nbsp;{ed.Value:yyyy-MM-dd}"
                                   : sd.HasValue ? sd.Value.ToString("yyyy-MM-dd") : "";
                        sb.Append($"<td style='white-space:nowrap'>{dur}</td><td></td>");
                        if (isFirst)
                        {
                            sb.Append($"<td{(span > 1 ? $" rowspan='{span}'" : "")} class='shared'>{Esc(site)}</td>");
                            isFirst = false;
                        }
                        sb.Append("</tr>");
                    }
                }

                AppendSumRowHtml(sb, "Subtotal",     SafeDecimal(hdr, "Subtotal").ToString("N2"),       false);
                AppendSumRowHtml(sb, "VAT",          SafeDecimal(hdr, "VatAmount").ToString("N2"),      false);
                AppendSumRowHtml(sb, "WHT",          SafeDecimal(hdr, "WhtAmount").ToString("N2"),      false);
                AppendSumRowHtml(sb, "Discount",     SafeDecimal(hdr, "DiscountAmount").ToString("N2"), false);
                AppendSumRowHtml(sb, "Total Amount", SafeDecimal(hdr, "TotalAmountDue").ToString("N2"), true);
                sb.Append($"<tr class='sep-row'><td colspan='{COLS}'></td></tr>");
            }

            sb.Append("</table></div></body></html>");
            return sb.ToString();
        }

        private static void AppendSumRowHtml(StringBuilder sb, string label, string val, bool isTotal)
        {
            string cls = isTotal ? "sum-row total-row" : "sum-row";
            sb.Append($"<tr class='{cls}'><td colspan='7'></td><td class='sum-lbl'>{Esc(label)}</td><td class='sum-val'>{Esc(val)}</td><td></td></tr>");
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF EXPORT — custom PdfSharp, row-by-row with proper page breaks
        // ════════════════════════════════════════════════════════════════════

        private void OnExportToPdf(object sender, EventArgs e)
        {
            using (var dlg = new SaveFileDialog { Title = "Save Invoice Export", Filter = "PDF files (*.pdf)|*.pdf",
                FileName = $"Invoice_Export_{DateTime.Now:yyyyMMdd_HHmmss}.pdf", DefaultExt = "pdf" })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try { GenerateInvoicePdf(dlg.FileName); Close(); System.Diagnostics.Process.Start(dlg.FileName); }
                catch (Exception ex) { MessageBox.Show($"Export failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }
        }

        private void GenerateInvoicePdf(string outputPath)
        {
            // ── Layout constants ─────────────────────────────────────────────
            const double margin    = 18;
            const double pageW     = 841.89;
            const double pageH     = 595.28;
            const double tableW    = pageW - margin * 2;
            const double padX      = 4;
            const double padY      = 3;
            const double hYearRow  = 22;
            const double hMonthRow = 18;
            const double hColHdr   = 20;
            const double hItemMin  = 16;
            const double hSumRow   = 15;
            const double hTotalRow = 18;
            const double hSep      = 7;
            const double lineH     = 10.5; // approx line height for 7.5pt font

            // 10 columns scaled to tableW
            double[] cw = { 60, 100, 76, 48, 68, 32, 148, 108, 72, 94 };
            { double s = cw.Sum(); for (int i = 0; i < cw.Length; i++) cw[i] = cw[i] / s * tableW; }

            // Fonts
            var fYear  = new XFont("Arial", 12, XFontStyle.Bold);
            var fMonth = new XFont("Arial", 10, XFontStyle.Bold);
            var fHdr   = new XFont("Arial",  8, XFontStyle.Bold);
            var fCell  = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fSum   = new XFont("Arial",  7.5, XFontStyle.Bold);
            var fTot   = new XFont("Arial",  8.5, XFontStyle.Bold);

            // Brushes — navy / slate / white professional report palette
            var bBlack    = new XSolidBrush(XColor.FromArgb( 33,  37,  41));  // charcoal — body text
            var bRed      = new XSolidBrush(XColor.FromArgb( 44, 111, 173));  // mid-blue — month banner
            var bDarkRed  = new XSolidBrush(XColor.FromArgb( 30,  64, 111));  // navy — year banner / total label
            var bBrightRed= new XSolidBrush(XColor.FromArgb( 52,  73,  94));  // slate — col header
            var bTotalBg  = new XSolidBrush(XColor.FromArgb(235, 245, 251));  // light blue — total row bg
            var bSumLbl   = new XSolidBrush(XColor.FromArgb( 73,  80,  87));  // slate gray — subtotal label
            var bGray     = new XSolidBrush(XColor.FromArgb(173, 181, 189));  // light slate — placeholder
            var pBorder   = new XPen(XColor.FromArgb(206, 212, 218), 0.5);    // light gray cell border
            var pStrong   = new XPen(XColor.FromArgb(108, 117, 125), 0.8);    // medium gray outer border

            // ── Document & page state ────────────────────────────────────────
            var doc = new PdfDocument { Info = { Title = "Invoice Export", Author = "Yakult.Inventory.App" } };
            PdfPage   page = null;
            XGraphics gfx  = null;
            double    y    = 0;
            int prevYear = -1, prevMonth = -1;

            double ColX(int c) { double x = margin; for (int i = 0; i < c; i++) x += cw[i]; return x; }
            double Bottom() => pageH - margin;

            void NewPage()
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }

            // ── Drawing helpers ──────────────────────────────────────────────

            void Banner(XBrush bg, XBrush fg, XFont f, string text, double h)
            {
                if (y + h > Bottom()) { NewPage(); prevYear = prevMonth = -1; }
                gfx.DrawRectangle(bg, margin, y, tableW, h);
                gfx.DrawString(text, f, fg, new XRect(margin, y, tableW, h), XStringFormats.Center);
                gfx.DrawLine(pStrong, margin, y + h, margin + tableW, y + h);
                y += h;
            }

            void EmitColHeaders()
            {
                if (y + hColHdr > Bottom()) NewPage();
                string[] names = {"DATE","COMPANY","DOCUMENT #","STATUS","REFERENCE #","QTY","ITEM NAME","DURATION","AMOUNT","SITE"};
                bool[]   right = {false,false,false,false,false,true,false,false,true,false};
                gfx.DrawRectangle(bRed, margin, y, tableW, hColHdr);
                for (int c = 0; c < names.Length; c++)
                {
                    var r = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hColHdr - padY * 2);
                    gfx.DrawString(names[c], fHdr, XBrushes.White, r, right[c] ? XStringFormats.TopRight : XStringFormats.TopLeft);
                    if (c < names.Length - 1) gfx.DrawLine(pBorder, ColX(c + 1), y, ColX(c + 1), y + hColHdr);
                }
                OuterV(y, hColHdr);
                gfx.DrawLine(pStrong, margin, y + hColHdr, margin + tableW, y + hColHdr);
                y += hColHdr;
            }

            void OuterV(double ry, double rh)
            {
                gfx.DrawLine(pStrong, margin, ry, margin, ry + rh);
                gfx.DrawLine(pStrong, margin + tableW, ry, margin + tableW, ry + rh);
            }

            string Trim(string text, XFont f, double maxW)
            {
                if (string.IsNullOrEmpty(text) || maxW <= 0) return "";
                if (gfx.MeasureString(text, f).Width <= maxW) return text;
                const string ell = "...";
                double ew = gfx.MeasureString(ell, f).Width;
                for (int i = text.Length - 1; i > 0; i--)
                { var t = text.Substring(0, i); if (gfx.MeasureString(t, f).Width + ew <= maxW) return t + ell; }
                return ell;
            }

            List<string> Wrap(string text, XFont f, double maxW, int maxLines)
            {
                var res = new List<string>();
                if (string.IsNullOrWhiteSpace(text)) { res.Add(""); return res; }
                var words = text.Trim().Split(' ');
                string line = "";
                foreach (var w in words)
                {
                    string test = string.IsNullOrEmpty(line) ? w : line + " " + w;
                    if (gfx.MeasureString(test, f).Width <= maxW) { line = test; continue; }
                    if (!string.IsNullOrEmpty(line)) { res.Add(line); if (res.Count >= maxLines) break; line = w; }
                    else { res.Add(Trim(w, f, maxW)); if (res.Count >= maxLines) break; }
                }
                if (res.Count < maxLines && !string.IsNullOrEmpty(line)) res.Add(line);
                return res;
            }

            double ItemRowH(DataRow item) // null = empty/no-items placeholder
            {
                if (item == null) return hItemMin;
                var wl = Wrap(ColVal(item, "ItemName"), fCell, cw[6] - padX * 2, 4);
                return Math.Max(hItemMin, padY * 2 + wl.Count * lineH);
            }

            void DrawItemRow(DataRow item, bool showShared,
                string dateStr, string company, string docNum, string statusRaw, string refNum, string site,
                double rh)
            {
                if (showShared)
                {
                    // Shared cols: Date(0) Company(1) Doc(2) Status(3) Ref(4) Site(9)
                    void Shared(string text, int col, XFont f = null, XBrush b = null)
                    {
                        f = f ?? fCell; b = b ?? bBlack;
                        var r = new XRect(ColX(col) + padX, y + padY, cw[col] - padX * 2, rh - padY * 2);
                        gfx.DrawString(Trim(text, f, r.Width), f, b, r, XStringFormats.TopLeft);
                    }
                    Shared(dateStr,   0);
                    Shared(company,   1);
                    Shared(docNum,    2);
                    Shared(statusRaw, 3, fSum, StatusBrush(statusRaw));
                    Shared(refNum,    4);
                    Shared(site,      9);
                }

                // Item-specific cols (5–8)
                if (item != null)
                {
                    // Qty (5) right
                    string qty = FmtDR(item, "Quantity", "N0");
                    if (!string.IsNullOrEmpty(qty))
                        gfx.DrawString(qty, fCell, bBlack, new XRect(ColX(5) + padX, y + padY, cw[5] - padX * 2, rh - padY * 2), XStringFormats.TopRight);

                    // Item Name (6) wrapped
                    var lines = Wrap(ColVal(item, "ItemName"), fCell, cw[6] - padX * 2, 4);
                    double ty = y + padY;
                    foreach (var ln in lines) { if (ty + lineH > y + rh) break; gfx.DrawString(ln, fCell, bBlack, new XRect(ColX(6) + padX, ty, cw[6] - padX * 2, lineH), XStringFormats.TopLeft); ty += lineH; }

                    // Duration (7)
                    var sd = SafeDate(item, "LineStartDate"); var ed = SafeDate(item, "LineEndDate");
                    string dur = (sd.HasValue && ed.HasValue) ? $"{sd.Value:yyyy-MM-dd}  {ed.Value:yyyy-MM-dd}"
                               : sd.HasValue ? sd.Value.ToString("yyyy-MM-dd") : "";
                    if (!string.IsNullOrEmpty(dur))
                        gfx.DrawString(Trim(dur, fCell, cw[7] - padX * 2), fCell, bBlack, new XRect(ColX(7) + padX, y + padY, cw[7] - padX * 2, rh - padY * 2), XStringFormats.TopLeft);
                }
                else
                {
                    // no items placeholder
                    gfx.DrawString("(no items)", fCell, bGray, new XRect(ColX(6) + padX, y + padY, cw[6] - padX * 2, rh - padY * 2), XStringFormats.TopLeft);
                }

                // Row borders
                gfx.DrawLine(pBorder, margin, y + rh, margin + tableW, y + rh); // bottom
                for (int c = 1; c < 10; c++) gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + rh); // vert
                OuterV(y, rh);
                y += rh;
            }

            void DrawSumRow(string label, string val, bool isTotal)
            {
                double h = isTotal ? hTotalRow : hSumRow;
                if (isTotal) gfx.DrawRectangle(bTotalBg, margin, y, tableW, h);
                var lf = isTotal ? fTot : fSum;
                var lb = isTotal ? bDarkRed : bSumLbl;
                gfx.DrawString(label, lf, lb, new XRect(ColX(7) + padX, y + padY, cw[7] - padX * 2, h - padY * 2), XStringFormats.TopRight);
                gfx.DrawString(val,   lf, isTotal ? bDarkRed : bBlack, new XRect(ColX(8) + padX, y + padY, cw[8] - padX * 2, h - padY * 2), XStringFormats.TopRight);
                gfx.DrawLine(pBorder, ColX(8), y, ColX(8), y + h);
                gfx.DrawLine(pBorder, ColX(9), y, ColX(9), y + h);
                gfx.DrawLine(pBorder, margin, y + h, margin + tableW, y + h);
                OuterV(y, h);
                y += h;
            }

            // ── Main render loop ─────────────────────────────────────────────
            NewPage();

            // ── Report title block (first page only) ────────────────────────
            {
                const double titleH = 30;
                var fTitle    = new XFont("Arial", 14, XFontStyle.Bold);
                var fSubtitle = new XFont("Arial",  7.5, XFontStyle.Regular);
                var bTitleBg  = new XSolidBrush(XColor.FromArgb( 30,  64, 111));
                var bAccent   = new XSolidBrush(XColor.FromArgb( 44, 111, 173));
                var bSubFg    = new XSolidBrush(XColor.FromArgb(176, 204, 228));
                gfx.DrawRectangle(bTitleBg, margin, y, tableW, titleH);
                gfx.DrawRectangle(bAccent,  margin, y + titleH, tableW, 3);
                gfx.DrawString("Invoice Report", fTitle, XBrushes.White,
                    new XRect(margin + 10, y, tableW - 10, titleH * 0.62), XStringFormats.BottomLeft);
                string subtitle = $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_headers.Count} Invoice(s)   |   {_items.Rows.Count} Item(s)";
                gfx.DrawString(subtitle, fSubtitle, bSubFg,
                    new XRect(margin + 10, y + titleH * 0.62, tableW - 10, titleH * 0.38), XStringFormats.TopLeft);
                y += titleH + 3 + 8; // title + accent bar + gap
            }

            foreach (var hdr in SortedHeaders())
            {
                object dvRaw = hdr["InvoiceDate"];
                DateTime? invDate = (dvRaw != null && dvRaw != DBNull.Value) ? (DateTime?)Convert.ToDateTime(dvRaw) : null;
                int yr = invDate?.Year ?? 0, mo = invDate?.Month ?? 0;

                string setCode   = hdr["SetCode"]?.ToString()        ?? "";
                string company   = hdr["CompanyName"]?.ToString()    ?? "";
                string docNum    = hdr["DocumentNumber"]?.ToString()  ?? "";
                string refNum    = hdr["ReferenceNumber"]?.ToString() ?? "";
                string site      = hdr["Site"]?.ToString()           ?? "";
                string statusRaw = hdr["Status"]?.ToString()         ?? "";
                string dateStr   = invDate.HasValue ? invDate.Value.ToString("M/d/yyyy") : "";

                var groupItems = new List<DataRow>();
                foreach (DataRow it in _items.Rows)
                    if (string.Equals(ColVal(it, "SetCode"), setCode, StringComparison.OrdinalIgnoreCase))
                        groupItems.Add(it);

                // ── Year / Month banners ─────────────────────────────────────
                if (yr != prevYear)
                {
                    Banner(bBlack, XBrushes.White, fYear, yr > 0 ? yr.ToString() : "Unknown Year", hYearRow);
                    prevYear = yr; prevMonth = -1;
                }
                if (mo != prevMonth)
                {
                    // New month → always start on a new page (unless we're at the very top)
                    if (prevMonth != -1) { NewPage(); prevYear = -1; }
                    if (yr != prevYear)
                    {
                        Banner(bBlack, XBrushes.White, fYear, yr > 0 ? yr.ToString() : "Unknown Year", hYearRow);
                        prevYear = yr;
                    }
                    string mn = invDate.HasValue ? invDate.Value.ToString("MMMM") : "Unknown";
                    Banner(bRed, XBrushes.White, fMonth, mn, hMonthRow);
                    EmitColHeaders();
                    prevMonth = mo;
                }

                // ── Draw items, one-by-one with page-break handling ──────────
                const double summaryBlockH = 4 * hSumRow + hTotalRow + hSep + 2;
                bool shownSharedThisPage = false;

                var items = groupItems.Count > 0 ? groupItems : new List<DataRow> { null };

                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    double rh = ItemRowH(it);
                    bool isLast = (i == items.Count - 1);

                    // Need space for row + (if last) summary block
                    double needed = rh + (isLast ? summaryBlockH : 0);

                    if (y + needed > Bottom())
                    {
                        // Start new page
                        NewPage();
                        prevYear = prevMonth = -1; // force re-emit banners on next invoice

                        // Re-emit year+month+col headers on continuation page
                        if (yr != prevYear)
                        {
                            Banner(bBlack, XBrushes.White, fYear, yr > 0 ? yr.ToString() : "Unknown", hYearRow);
                            prevYear = yr; prevMonth = -1;
                        }
                        if (mo != prevMonth)
                        {
                            string mn2 = invDate.HasValue ? invDate.Value.ToString("MMMM") : "Unknown";
                            Banner(bRed, XBrushes.White, fMonth, mn2, hMonthRow);
                            EmitColHeaders();
                            prevMonth = mo;
                        }

                        shownSharedThisPage = false; // re-show shared cols on new page
                    }

                    DrawItemRow(it, !shownSharedThisPage, dateStr, company, docNum, statusRaw, refNum, site, rh);
                    shownSharedThisPage = true;
                }

                // ── Summary rows (space already ensured for last item) ────────
                DrawSumRow("Subtotal",     SafeDecimal(hdr, "Subtotal").ToString("N2"),       false);
                DrawSumRow("VAT",          SafeDecimal(hdr, "VatAmount").ToString("N2"),      false);
                DrawSumRow("WHT",          SafeDecimal(hdr, "WhtAmount").ToString("N2"),      false);
                DrawSumRow("Discount",     SafeDecimal(hdr, "DiscountAmount").ToString("N2"), false);
                DrawSumRow("Total Amount", SafeDecimal(hdr, "TotalAmountDue").ToString("N2"), true);

                // ── Red separator ────────────────────────────────────────────
                gfx.DrawRectangle(bBrightRed, margin, y, tableW, hSep);
                y += hSep + 2;
            }

            doc.Save(outputPath);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private List<DataRow> SortedHeaders()
            => _headers
                .Select(r => { object d = r["InvoiceDate"]; return new { Row = r, Date = d != null && d != DBNull.Value ? (DateTime?)Convert.ToDateTime(d) : null }; })
                .OrderBy(x => x.Date ?? DateTime.MaxValue)
                .Select(x => x.Row)
                .ToList();

        private static XBrush StatusBrush(string s)
        {
            switch ((s ?? "").ToLowerInvariant())
            {
                case "active": case "completed": case "yes":   return new XSolidBrush(XColor.FromArgb( 21,  87,  36));  // green
                case "draft": case "pending": case "for approval": case "processing": case "submitted":
                    return new XSolidBrush(XColor.FromArgb(133,  77,  14));  // amber
                case "cancelled": case "rejected": case "expired": case "inactive":
                    return new XSolidBrush(XColor.FromArgb(114,  28,  36));  // red
                default: return new XSolidBrush(XColor.FromArgb( 73,  80,  87));
            }
        }

        private static string BadgeTd(string val, string tdAttrs)
        {
            string bc;
            switch ((val ?? "").ToLowerInvariant())
            {
                case "active": case "completed": case "yes": bc = "b-active"; break;
                case "draft": case "pending": case "for approval": case "processing": case "submitted": bc = "b-pending"; break;
                case "cancelled": case "rejected": case "expired": case "inactive": bc = "b-inactive"; break;
                default: bc = "b-default"; break;
            }
            string open = string.IsNullOrEmpty(tdAttrs) ? "<td>" : $"<td {tdAttrs}>";
            return $"{open}<span class='badge {bc}'>{Esc(val)}</span></td>";
        }

        private static decimal SafeDecimal(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return 0m;
            try { return Convert.ToDecimal(row[col]); } catch { return 0m; }
        }

        private static string ColVal(DataRow row, string col)
            => row.Table.Columns.Contains(col) && row[col] != DBNull.Value ? row[col].ToString() : "";

        private static string FmtDR(DataRow row, string col, string fmt)
            => row.Table.Columns.Contains(col) && row[col] != DBNull.Value
                ? Convert.ToDecimal(row[col]).ToString(fmt) : "0";

        private static DateTime? SafeDate(DataRow row, string col)
        {
            if (!row.Table.Columns.Contains(col) || row[col] == DBNull.Value) return null;
            try { return Convert.ToDateTime(row[col]); } catch { return null; }
        }

        private static string Esc(string s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
