using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Models;

namespace Yakult.Inventory.App.Helpers
{
    public static class RenewalReportPdfGenerator
    {
        public static void GenerateSelectedRenewalsPdf(
            IReadOnlyList<RenewalDto> renewals,
            string outputPath,
            string title = "Selected Renewals")
        {
            if (renewals == null) throw new ArgumentNullException(nameof(renewals));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = renewals.Where(r => r != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Selected Renewals Export",
                    Author = "Yakult.Inventory.App"
                }
            };

            var page = doc.AddPage();
            page.Orientation = PdfSharp.PageOrientation.Landscape;
            page.Size = PdfSharp.PageSize.A4;

            var gfx = XGraphics.FromPdfPage(page);
            var fontTitle = new XFont("Segoe UI", 16, XFontStyle.Bold);
            var fontMeta = new XFont("Segoe UI", 9, XFontStyle.Regular);
            var fontHeader = new XFont("Segoe UI", 9, XFontStyle.Bold);
            var fontCell = new XFont("Segoe UI", 8, XFontStyle.Regular);

            const double margin = 18;
            var y = margin;

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Renewals: {safe.Count}";

            void DrawTop(string headerSuffix = null)
            {
                gfx.DrawString(string.IsNullOrWhiteSpace(headerSuffix) ? title : title + " " + headerSuffix,
                    fontTitle, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
                y += 26;
                gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
                y += 18;
            }

            DrawTop();

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;
            var bottomLimit = page.Height - margin - 18;

            var penBorder = new XPen(XColor.FromArgb(220, 220, 220), 0.8);
            var brushHeaderBack = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var brushZebra = new XSolidBrush(XColor.FromArgb(251, 252, 253));

            const double headerH = 20;
            const double padX = 3;
            const double padY = 3;
            const double minRowH = 18;

            void NewPage(string headerSuffix)
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
                DrawTop(headerSuffix);
                bottomLimit = page.Height - margin - 18;
            }

            var columns = new[]
            {
                new Col("Set Code", 75),
                new Col("Type", 80),
                new Col("Document #", 80),
                new Col("Company", 95, wrap: true, maxLines: 2),
                new Col("Vendor", 90, wrap: true, maxLines: 2),
                new Col("Site", 140, wrap: true, maxLines: 2),
                new Col("Start", 70),
                new Col("End", 70),
                new Col("Days", 45, rightAlign: true),
                new Col("Status", 75),
                new Col("Items", 45, rightAlign: true),
                new Col("Amount", 80, rightAlign: true),
                new Col("Renewed", 75),
            };

            void DrawHeaderRow()
            {
                if (y + headerH > bottomLimit)
                    NewPage("(cont.)");

                var x = tableLeft;
                gfx.DrawRectangle(brushHeaderBack, tableLeft, y, tableWidth, headerH);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, headerH);

                foreach (var c in columns)
                {
                    gfx.DrawLine(penBorder, x, y, x, y + headerH);
                    gfx.DrawString(c.Title, fontHeader, XBrushes.Black,
                        new XRect(x + padX, y + 3, c.Width - padX * 2, headerH - 6), XStringFormats.TopLeft);
                    x += c.Width;
                }
                gfx.DrawLine(penBorder, tableLeft + tableWidth, y, tableLeft + tableWidth, y + headerH);
                y += headerH;
            }

            DrawHeaderRow();

            var zebra = false;
            var lineHeight = gfx.MeasureString("Ag", fontCell).Height;

            foreach (var r in safe)
            {
                var values = new[]
                {
                    r.SetCode ?? string.Empty,
                    r.SetType ?? string.Empty,
                    r.DocumentNumber ?? string.Empty,
                    r.CompanyName ?? string.Empty,
                    r.VendorName ?? string.Empty,
                    r.SiteDisplay ?? string.Empty,
                    r.StartDate == null ? "" : r.StartDate.Value.ToString("yyyy-MM-dd"),
                    r.EndDate == null ? "" : r.EndDate.Value.ToString("yyyy-MM-dd"),
                    r.DaysUntilExpiry.HasValue ? r.DaysUntilExpiry.Value.ToString() : "",
                    r.ExpiryStatus ?? string.Empty,
                    r.ItemCount.ToString(),
                    r.TotalAmountDue.ToString("N2"),
                    r.RenewedDate == null ? "" : r.RenewedDate.Value.ToString("yyyy-MM-dd"),
                };

                double rowH = minRowH;
                for (int i = 0; i < columns.Length; i++)
                {
                    if (!columns[i].Wrap) continue;
                    var contentW = columns[i].Width - padX * 2;
                    var lines = WrapToLines(gfx, values[i], fontCell, contentW, columns[i].MaxLines);
                    rowH = Math.Max(rowH, padY * 2 + lines.Count * lineHeight);
                }

                if (y + rowH > bottomLimit)
                {
                    NewPage("(cont.)");
                    DrawHeaderRow();
                }

                if (zebra)
                    gfx.DrawRectangle(brushZebra, tableLeft, y, tableWidth, rowH);
                zebra = !zebra;

                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, rowH);
                var x = tableLeft;

                for (int i = 0; i < columns.Length; i++)
                {
                    var col = columns[i];
                    var text = values[i] ?? string.Empty;
                    var rect = new XRect(x + padX, y + padY, col.Width - padX * 2, rowH - padY * 2);

                    if (!col.Wrap)
                    {
                        var fit = TrimToFit(gfx, text, fontCell, rect.Width);
                        gfx.DrawString(fit, fontCell, XBrushes.Black, rect,
                            col.RightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
                    }
                    else
                    {
                        var lines = WrapToLines(gfx, text, fontCell, rect.Width, col.MaxLines);
                        var ty = y + padY;
                        foreach (var line in lines)
                        {
                            gfx.DrawString(line, fontCell, XBrushes.Black, new XRect(x + padX, ty, rect.Width, lineHeight),
                                XStringFormats.TopLeft);
                            ty += lineHeight;
                            if (ty > y + rowH - padY) break;
                        }
                    }

                    x += col.Width;
                    gfx.DrawLine(penBorder, x, y, x, y + rowH);
                }

                y += rowH;
            }

            doc.Save(outputPath);
        }

        public static void TryOpen(string filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                    return;
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch
            {
            }
        }

        private sealed class Col
        {
            public Col(string title, double width, bool rightAlign = false, bool wrap = false, int maxLines = 1)
            {
                Title = title;
                Width = width;
                RightAlign = rightAlign;
                Wrap = wrap;
                MaxLines = Math.Max(1, maxLines);
            }

            public string Title { get; }
            public double Width { get; set; }
            public bool RightAlign { get; }
            public bool Wrap { get; }
            public int MaxLines { get; }
        }

        private static string TrimToFit(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0)
                return string.Empty;

            if (gfx.MeasureString(text, font).Width <= maxWidth)
                return text;

            const string ellipsis = "…";
            var eW = gfx.MeasureString(ellipsis, font).Width;
            if (eW > maxWidth)
                return string.Empty;

            var s = text;
            while (s.Length > 1)
            {
                s = s.Substring(0, s.Length - 1);
                if (gfx.MeasureString(s, font).Width + eW <= maxWidth)
                    return s + ellipsis;
            }
            return ellipsis;
        }

        private static List<string> WrapToLines(XGraphics gfx, string text, XFont font, double maxWidth, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0)
                return lines;

            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;

            foreach (var w in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? w : current + " " + w;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth)
                {
                    current = candidate;
                    continue;
                }

                if (!string.IsNullOrEmpty(current))
                {
                    lines.Add(current);
                    current = w;
                    if (lines.Count >= maxLines)
                        break;
                }
                else
                {
                    lines.Add(TrimToFit(gfx, w, font, maxWidth));
                    current = string.Empty;
                    if (lines.Count >= maxLines)
                        break;
                }
            }

            if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
                lines.Add(current);

            return lines;
        }
    }
}
