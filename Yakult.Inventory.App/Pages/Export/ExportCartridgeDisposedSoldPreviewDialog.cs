using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Pages.Export
{
    internal class ExportCartridgeDisposedSoldPreviewDialog : Form
    {
        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color AccentNavy   = Color.FromArgb( 30,  64, 111);
        private static readonly Color CharcoalText = Color.FromArgb( 33,  37,  41);
        private static readonly Color LightBg      = Color.FromArgb(248, 249, 250);
        private static readonly Color BorderColor  = Color.FromArgb(206, 212, 218);

        private readonly List<DisposedSoldItemDto> _items;
        private WebBrowser _browser;

        internal ExportCartridgeDisposedSoldPreviewDialog(List<DisposedSoldItemDto> items)
        {
            _items = items;

            Text            = "Cartridge Disposed/Sold — Export Preview";
            Size            = new Size(1100, 800);
            MinimumSize     = new Size(860, 580);
            StartPosition   = FormStartPosition.CenterParent;
            BackColor       = LightBg;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox     = true;

            BuildUi();
            _browser.DocumentText = BuildPreviewHtml();
        }

        // ── UI ───────────────────────────────────────────────────────────────

        private void BuildUi()
        {
            int disposedCount  = _items.Count(i => i.IsDisposed);
            int soldCount      = _items.Count(i => i.IsSold);

            var header = new Panel { Dock = DockStyle.Top, Height = 58, BackColor = AccentNavy };
            header.Controls.Add(new Label
            {
                Text      = $"Cartridge Disposed/Sold Export Preview  —  {_items.Count} Item(s)  |  {disposedCount} Disposed  |  {soldCount} Sold",
                Font      = new Font("Arial", 13F, FontStyle.Bold),
                ForeColor = Color.White,
                AutoSize  = true,
                Location  = new Point(16, 10)
            });
            header.Controls.Add(new Label
            {
                Text      = "Review disposed and sold cartridge items below, then click Export to PDF.",
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
                Size      = new Size(100, 34),
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(226, 232, 240),
                ForeColor = Color.FromArgb(30, 41, 59),
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
                btnCancel.Location = new Point(btnExport.Left - btnCancel.Width - 12, (btnBar.Height - btnCancel.Height) / 2);
            };
            btnBar.Controls.Add(btnExport);
            btnBar.Controls.Add(btnCancel);

            _browser = new WebBrowser { Dock = DockStyle.Fill };
            Controls.Add(_browser);
            Controls.Add(btnBar);
            Controls.Add(header);
        }

        // ════════════════════════════════════════════════════════════════════
        //  Recipient / Company helper
        // ════════════════════════════════════════════════════════════════════

        private static string GetRecipientCompany(DisposedSoldItemDto item)
        {
            if (!string.IsNullOrWhiteSpace(item.RecipientName))
                return item.RecipientName;
            if (!string.IsNullOrWhiteSpace(item.VendorName))
                return item.VendorName;
            if (!string.IsNullOrWhiteSpace(item.DisposalCompanyName))
                return item.DisposalCompanyName;
            return "";
        }

        // ════════════════════════════════════════════════════════════════════
        //  HTML PREVIEW
        // ════════════════════════════════════════════════════════════════════

        private string BuildPreviewHtml()
        {
            var sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html><html><head><meta charset='UTF-8'><title>Cartridge Disposed/Sold Export</title>
<style>
*{box-sizing:border-box;margin:0;padding:0;}
body{background:#f0f2f5;font-family:Arial,Helvetica,sans-serif;font-size:12px;color:#212529;}
.page{background:#fff;margin:20px 24px 30px;border-radius:6px;box-shadow:0 2px 12px rgba(0,0,0,.12);overflow:hidden;}
.rpt-title{background:#1e406f;color:#fff;padding:18px 28px 14px;border-bottom:3px solid #2c6fad;}
.rpt-title h1{font-family:Arial,Helvetica,sans-serif;font-size:22px;font-weight:700;margin:0 0 4px;letter-spacing:.3px;}
.rpt-title p{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#b0cce4;margin:0;}

/* ── Month section ──────────────────────────────────── */
.month-header{background:#e8ecf1;border-top:3px solid #1e406f;border-bottom:1px solid #dee2e6;padding:10px 24px;margin-top:6px;}
.month-header h2{font-family:Arial,Helvetica,sans-serif;font-size:15px;font-weight:700;color:#1e406f;margin:0;letter-spacing:.3px;}
.month-header .month-count{font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;margin-left:10px;font-weight:400;}

/* ── Table styles ───────────────────────────────────── */
.rtbl{border-collapse:collapse;width:100%;background:#fff;}
.rtbl th{background:#34495e;color:#fff;font-family:Arial,Helvetica,sans-serif;font-weight:700;font-size:10px;text-align:left;padding:8px 10px;white-space:nowrap;letter-spacing:.3px;text-transform:uppercase;border-right:1px solid #4a6274;}
.rtbl th:last-child{border-right:none;}
.rtbl td{font-family:Arial,Helvetica,sans-serif;padding:6px 10px;border-bottom:1px solid #e9ecef;font-size:11px;vertical-align:top;border-right:1px solid #f0f0f0;}
.rtbl td:last-child{border-right:none;}
.rtbl tr:nth-child(even) td{background:#f8f9fa;}
td.batch-id{font-weight:700;color:#1e406f;}
td.type-dispose{font-weight:600;color:#991b1b;background:#fee2e2;}
td.type-sell{font-weight:600;color:#15803d;background:#dcfce7;}
td.model{font-weight:600;}
td.qty{text-align:center;font-weight:600;}
td.recipient{font-weight:500;}

/* ── Summary footer ─────────────────────────────────── */
.rpt-footer{background:#f8f9fa;border-top:2px solid #dee2e6;padding:12px 24px;font-family:Arial,Helvetica,sans-serif;font-size:11px;color:#6c757d;}
.rpt-footer strong{color:#212529;}
</style></head><body><div class='page'>
<div class='rpt-title'>
  <h1>Cartridge Disposed/Sold Export Report</h1>
  <p>Generated: ");
            sb.Append($"{DateTime.Now:MMMM dd, yyyy  h:mm tt}  &nbsp;|&nbsp;  {_items.Count} Item(s)");
            sb.Append("</p></div>");

            // Group by month of DecidedAt, earliest first
            var monthlyBuckets = _items
                .OrderBy(i => i.DecidedAt)
                .GroupBy(i => new DateTime(i.DecidedAt.Year, i.DecidedAt.Month, 1))
                .OrderBy(mb => mb.Key);

            foreach (var monthBucket in monthlyBuckets)
            {
                string monthLabel = monthBucket.Key.ToString("MMMM yyyy");
                int count = monthBucket.Count();

                sb.Append($"<div class='month-header'><h2>{Esc(monthLabel)}<span class='month-count'>({count} item{(count != 1 ? "s" : "")})</span></h2></div>");

                sb.Append("<table class='rtbl'>");
                sb.Append("<thead><tr>");
                sb.Append("<th>Batch ID</th>");
                sb.Append("<th>Date</th>");
                sb.Append("<th>Type</th>");
                sb.Append("<th>Cartridge Model</th>");
                sb.Append("<th>Qty</th>");
                sb.Append("<th>Condition</th>");
                sb.Append("<th>Recipient / Company</th>");
                sb.Append("</tr></thead><tbody>");

                foreach (var item in monthBucket.OrderBy(i => i.DecidedAt))
                {
                    string typeCls = item.IsDisposed ? "type-dispose" : (item.IsSold ? "type-sell" : "");

                    sb.Append("<tr>");
                    sb.Append($"<td class='batch-id'>{(item.BatchId.HasValue ? item.BatchId.Value.ToString() : "\u2014")}</td>");
                    sb.Append($"<td>{item.DecidedAt:MM/dd/yyyy}</td>");
                    sb.Append($"<td class='{typeCls}'>{Esc(item.DecisionTypeName)}</td>");
                    sb.Append($"<td class='model'>{Esc(item.CartridgeModelNumber)}</td>");
                    sb.Append($"<td class='qty'>{item.Quantity}</td>");
                    sb.Append($"<td>{Esc(item.ConditionName)}</td>");
                    sb.Append($"<td class='recipient'>{Esc(GetRecipientCompany(item))}</td>");
                    sb.Append("</tr>");
                }

                sb.Append("</tbody></table>");
            }

            // Report footer
            sb.Append("<div class='rpt-footer'>");
            sb.Append($"<strong>Total:</strong> {_items.Count} item(s) &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Disposed:</strong> {_items.Count(i => i.IsDisposed)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Sold:</strong> {_items.Count(i => i.IsSold)} &nbsp;&middot;&nbsp; ");
            sb.Append($"<strong>Total Qty:</strong> {_items.Sum(i => i.Quantity)}");
            sb.Append("</div>");

            sb.Append("</div></body></html>");
            return sb.ToString();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PDF EXPORT
        // ════════════════════════════════════════════════════════════════════

        private void GenerateDisposedSoldPdf(string outputPath)
        {
            const double margin  = 36;
            const double pageW   = 841.89;   // A4 landscape
            const double pageH   = 595.28;
            const double tableW  = pageW - margin * 2;
            const double padX    = 4;
            const double padY    = 2;
            const double hTitle  = 34;
            const double hMonth  = 22;
            const double hHdr    = 18;
            const double hRow    = 14;
            const double hFooter = 20;

            // 7 columns: Batch ID | Date | Type | Cartridge Model | Qty | Condition | Recipient/Company
            double[] cw = { 70, 80, 70, 160, 50, 120, 220 };
            { double s = cw.Sum(); for (int i = 0; i < cw.Length; i++) cw[i] = cw[i] / s * tableW; }

            // Fonts — all Arial
            var fTitle    = new XFont("Arial", 14, XFontStyle.Bold);
            var fSubtitle = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fMonth    = new XFont("Arial", 10, XFontStyle.Bold);
            var fMonthCnt = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fHdr      = new XFont("Arial",  7, XFontStyle.Bold);
            var fCell     = new XFont("Arial",  7.5, XFontStyle.Regular);
            var fCellBold = new XFont("Arial",  7.5, XFontStyle.Bold);
            var fFooter   = new XFont("Arial",  8, XFontStyle.Regular);
            var fFooterB  = new XFont("Arial",  8, XFontStyle.Bold);

            // Brushes & pens
            var bNavy     = new XSolidBrush(XColor.FromArgb( 30,  64, 111));
            var bMidBlue  = new XSolidBrush(XColor.FromArgb( 44, 111, 173));
            var bSlate    = new XSolidBrush(XColor.FromArgb( 52,  73,  94));
            var bMonthBg  = new XSolidBrush(XColor.FromArgb(232, 236, 241));
            var bAlt      = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bBlack    = new XSolidBrush(XColor.FromArgb( 33,  37,  41));
            var bGray     = new XSolidBrush(XColor.FromArgb(108, 117, 125));
            var bSubFg    = new XSolidBrush(XColor.FromArgb(176, 204, 228));
            var bFooterBg = new XSolidBrush(XColor.FromArgb(248, 249, 250));
            var bDispose  = new XSolidBrush(XColor.FromArgb(153,  27,  27));
            var bSell     = new XSolidBrush(XColor.FromArgb( 21, 128,  61));
            var pBorder   = new XPen(XColor.FromArgb(206, 212, 218), 0.4);
            var pStrong   = new XPen(XColor.FromArgb(108, 117, 125), 0.6);
            var pMonthTop = new XPen(XColor.FromArgb( 30,  64, 111), 1.5);

            var doc = new PdfDocument { Info = { Title = "Cartridge Disposed/Sold Export", Author = "Yakult.Inventory.App" } };
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
                string[] names = { "BATCH ID", "DATE", "TYPE", "CARTRIDGE MODEL", "QTY", "CONDITION", "RECIPIENT / COMPANY" };
                bool[]   right = { false, false, false, false, true, false, false };
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
            gfx.DrawString("Cartridge Disposed/Sold Export Report", fTitle, XBrushes.White,
                new XRect(margin + 10, y, tableW - 10, hTitle * 0.62), XStringFormats.BottomLeft);
            string sub = $"Generated: {DateTime.Now:MMMM dd, yyyy  h:mm tt}   |   {_items.Count} Item(s)";
            gfx.DrawString(sub, fSubtitle, bSubFg,
                new XRect(margin + 10, y + hTitle * 0.62, tableW - 10, hTitle * 0.38), XStringFormats.TopLeft);
            y += hTitle + 3 + 6;

            // ── Group by month, earliest first ───────────────────────────────
            var monthlyBuckets = _items
                .OrderBy(i => i.DecidedAt)
                .GroupBy(i => new DateTime(i.DecidedAt.Year, i.DecidedAt.Month, 1))
                .OrderBy(mb => mb.Key);

            int rowIdx = 0;

            foreach (var monthBucket in monthlyBuckets)
            {
                var monthItems = monthBucket.OrderBy(i => i.DecidedAt).ToList();

                // ── Month header ──
                if (y + hMonth + hHdr + hRow > Bottom()) NewPage();

                string monthLabel = monthBucket.Key.ToString("MMMM yyyy");
                int count = monthItems.Count;

                gfx.DrawLine(pMonthTop, margin, y, margin + tableW, y);
                gfx.DrawRectangle(bMonthBg, margin, y, tableW, hMonth);
                gfx.DrawString(monthLabel, fMonth, bNavy,
                    new XRect(margin + 10, y, tableW * 0.5, hMonth), XStringFormats.CenterLeft);
                gfx.DrawString($"({count} item{(count != 1 ? "s" : "")})", fMonthCnt, bGray,
                    new XRect(margin + 10 + gfx.MeasureString(monthLabel, fMonth).Width + 8, y, 100, hMonth), XStringFormats.CenterLeft);
                gfx.DrawLine(pBorder, margin, y + hMonth, margin + tableW, y + hMonth);
                OuterV(y, hMonth);
                y += hMonth;

                EmitColHeaders();

                foreach (var item in monthItems)
                {
                    if (y + hRow > Bottom()) { NewPage(); EmitColHeaders(); }

                    if (rowIdx % 2 == 1)
                        gfx.DrawRectangle(bAlt, margin, y, tableW, hRow);

                    string[] vals = {
                        item.BatchId.HasValue ? item.BatchId.Value.ToString() : "\u2014",
                        item.DecidedAt.ToString("MM/dd/yyyy"),
                        item.DecisionTypeName ?? "",
                        item.CartridgeModelNumber ?? "",
                        item.Quantity.ToString(),
                        item.ConditionName ?? "",
                        GetRecipientCompany(item)
                    };
                    bool[] rAlign = { false, false, false, false, true, false, false };

                    for (int c = 0; c < vals.Length; c++)
                    {
                        var rect = new XRect(ColX(c) + padX, y + padY, cw[c] - padX * 2, hRow - padY * 2);
                        var fmt  = rAlign[c] ? XStringFormats.TopRight : XStringFormats.TopLeft;

                        XFont font;
                        XBrush brush;

                        if (c == 0) { font = fCellBold; brush = bNavy; }
                        else if (c == 2)
                        {
                            font = fCellBold;
                            brush = item.IsDisposed ? bDispose : (item.IsSold ? bSell : bBlack);
                        }
                        else if (c == 3) { font = fCellBold; brush = bBlack; }
                        else { font = fCell; brush = bBlack; }

                        gfx.DrawString(TrimText(vals[c], font, rect.Width), font, brush, rect, fmt);
                    }

                    gfx.DrawLine(pBorder, margin, y + hRow, margin + tableW, y + hRow);
                    for (int c = 1; c < cw.Length; c++)
                        gfx.DrawLine(pBorder, ColX(c), y, ColX(c), y + hRow);
                    OuterV(y, hRow);

                    y += hRow;
                    rowIdx++;
                }

                gfx.DrawLine(pStrong, margin, y, margin + tableW, y);
            }

            // ── Report footer ────────────────────────────────────────────────
            if (y + hFooter > Bottom()) NewPage();
            gfx.DrawRectangle(bFooterBg, margin, y, tableW, hFooter);
            gfx.DrawLine(new XPen(XColor.FromArgb(206, 212, 218), 1.0), margin, y, margin + tableW, y);

            double fx = margin + 10;
            gfx.DrawString("Total: ", fFooterB, bBlack,
                new XRect(fx, y, 40, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total: ", fFooterB).Width;
            string t1 = $"{_items.Count} item(s)   \u00B7   ";
            gfx.DrawString(t1, fFooter, bGray, new XRect(fx, y, 120, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(t1, fFooter).Width;
            gfx.DrawString("Disposed: ", fFooterB, bBlack, new XRect(fx, y, 70, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Disposed: ", fFooterB).Width;
            string t2 = $"{_items.Count(i => i.IsDisposed)}   \u00B7   ";
            gfx.DrawString(t2, fFooter, bGray, new XRect(fx, y, 60, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(t2, fFooter).Width;
            gfx.DrawString("Sold: ", fFooterB, bBlack, new XRect(fx, y, 40, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Sold: ", fFooterB).Width;
            string t3 = $"{_items.Count(i => i.IsSold)}   \u00B7   ";
            gfx.DrawString(t3, fFooter, bGray, new XRect(fx, y, 60, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString(t3, fFooter).Width;
            gfx.DrawString("Total Qty: ", fFooterB, bBlack, new XRect(fx, y, 70, hFooter), XStringFormats.CenterLeft);
            fx += gfx.MeasureString("Total Qty: ", fFooterB).Width;
            gfx.DrawString($"{_items.Sum(i => i.Quantity)}", fFooter, bGray, new XRect(fx, y, 60, hFooter), XStringFormats.CenterLeft);

            doc.Save(outputPath);
        }

        // ── Event handlers ───────────────────────────────────────────────────

        private void OnExportToPdf(object sender, EventArgs e)
        {
            try
            {
                using (var sfd = new SaveFileDialog())
                {
                    sfd.Filter   = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*";
                    sfd.FileName = $"CartridgeDisposedSold_{DateTime.Now:yyyyMMdd_HHmmss}.pdf";
                    sfd.Title    = "Save Cartridge Disposed/Sold Export";

                    if (sfd.ShowDialog() != DialogResult.OK) return;

                    GenerateDisposedSoldPdf(sfd.FileName);
                    MessageBox.Show("Export saved successfully!", "Export Complete",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error exporting to PDF:\n{ex.Message}", "Export Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private static string Esc(string s)
            => (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
