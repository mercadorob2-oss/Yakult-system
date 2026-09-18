using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Yakult.Inventory.App.Helpers
{
    public static class TabularPdfGenerator
    {
        public sealed class Col
        {
            public Col(string title, double width, bool rightAlign = false, bool wrap = false, int maxLines = 1)
            {
                Title = title ?? string.Empty;
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

        public static void GenerateTablePdf(
            string title,
            string subject,
            IReadOnlyList<Col> columns,
            IReadOnlyList<string[]> rows,
            string outputPath)
        {
            if (string.IsNullOrWhiteSpace(title)) title = "Report";
            if (columns == null) throw new ArgumentNullException(nameof(columns));
            if (rows == null) throw new ArgumentNullException(nameof(rows));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safeColumns = columns.Where(c => c != null).ToList();
            if (safeColumns.Count == 0) throw new ArgumentException("At least one column is required.", nameof(columns));

            var safeRows = rows.Where(r => r != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = subject ?? string.Empty,
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
            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Rows: {safeRows.Count}";

            void DrawTop(string suffix = null)
            {
                var header = string.IsNullOrWhiteSpace(suffix) ? title : $"{title} {suffix}";
                gfx.DrawString(header, fontTitle, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
                y += 26;
                gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
                y += 18;
            }

            DrawTop();

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;
            var bottomLimit = page.Height - margin - 18;

            // Fit table to page width (shrink the widest wrap column first)
            var total = safeColumns.Sum(c => Math.Max(1, c.Width));
            if (total > tableWidth)
            {
                var excess = total - tableWidth;
                var candidate = safeColumns
                    .Where(c => c.Wrap)
                    .OrderByDescending(c => c.Width)
                    .FirstOrDefault() ?? safeColumns.OrderByDescending(c => c.Width).FirstOrDefault();

                if (candidate != null)
                    candidate.Width = Math.Max(60, candidate.Width - excess);
            }

            var penBorder = new XPen(XColor.FromArgb(220, 220, 220), 0.8);
            var brushHeaderBack = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var brushZebra = new XSolidBrush(XColor.FromArgb(251, 252, 253));

            const double headerH = 20;
            const double padX = 3;
            const double padY = 3;
            const double minRowH = 18;

            void NewPage()
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
                DrawTop("(cont.)");
                bottomLimit = page.Height - margin - 18;
                DrawHeaderRow();
            }

            void DrawHeaderRow()
            {
                if (y + headerH > bottomLimit)
                    NewPage();

                var x = tableLeft;
                gfx.DrawRectangle(brushHeaderBack, tableLeft, y, tableWidth, headerH);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, headerH);

                foreach (var c in safeColumns)
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

            var lineHeight = gfx.MeasureString("Ag", fontCell).Height;
            var zebra = false;

            foreach (var row in safeRows)
            {
                var values = new string[safeColumns.Count];
                for (var i = 0; i < values.Length; i++)
                    values[i] = i < row.Length ? (row[i] ?? string.Empty) : string.Empty;

                var rowH = minRowH;
                for (var i = 0; i < safeColumns.Count; i++)
                {
                    var col = safeColumns[i];
                    if (!col.Wrap) continue;
                    var contentW = col.Width - padX * 2;
                    var lines = WrapToLines(gfx, values[i], fontCell, contentW, col.MaxLines);
                    rowH = Math.Max(rowH, padY * 2 + lines.Count * lineHeight);
                }

                if (y + rowH > bottomLimit)
                    NewPage();

                if (zebra)
                    gfx.DrawRectangle(brushZebra, tableLeft, y, tableWidth, rowH);
                zebra = !zebra;

                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, rowH);
                var x = tableLeft;

                for (var i = 0; i < safeColumns.Count; i++)
                {
                    var col = safeColumns[i];
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
                            gfx.DrawString(line, fontCell, XBrushes.Black, new XRect(x + padX, ty, rect.Width, lineHeight), XStringFormats.TopLeft);
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
            var result = new List<string>();
            text = (text ?? string.Empty).Trim();
            if (text.Length == 0)
            {
                result.Add(string.Empty);
                return result;
            }

            // Normalize whitespace
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var line = string.Empty;

            foreach (var word in words)
            {
                var test = string.IsNullOrEmpty(line) ? word : line + " " + word;
                if (gfx.MeasureString(test, font).Width <= maxWidth)
                {
                    line = test;
                    continue;
                }

                if (!string.IsNullOrEmpty(line))
                {
                    result.Add(line);
                    line = word;
                    if (result.Count >= maxLines)
                        break;
                }
                else
                {
                    // single long word: trim to fit
                    result.Add(TrimToFit(gfx, word, font, maxWidth));
                    line = string.Empty;
                    if (result.Count >= maxLines)
                        break;
                }
            }

            if (result.Count < maxLines && !string.IsNullOrEmpty(line))
                result.Add(line);

            if (result.Count > maxLines)
                result = result.Take(maxLines).ToList();

            return result;
        }
    }
}

