using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Pages;

namespace Yakult.Inventory.App.Helpers
{
    public static class ItemPdfGenerator
    {
        public static void GenerateSelectedItemsPdf(IReadOnlyList<ItemDto> items, string outputPath, string title = "Selected Items")
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safeItems = items.Where(i => i != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Inventory Items Export",
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
            var fontSection = new XFont("Segoe UI", 9, XFontStyle.Bold);

            const double margin = 18;
            var y = margin;

            // Header
            gfx.DrawString(title, fontTitle, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
            y += 26;

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Items: {safeItems.Count}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
            y += 18;

            // Table layout
            var tableLeft = margin;
            var tableRight = page.Width - margin;
            var tableWidth = tableRight - tableLeft;

            var columns = new[]
            {
                new Col("ID", 40, rightAlign: true),
                new Col("Name", 155, wrap: true, maxLines: 3),
                new Col("Category", 80),
                new Col("Type", 55),
                new Col("Model", 75),
                new Col("Serial", 75),
                new Col("Stock", 45, rightAlign: true),
                new Col("Date Created", 75),
                new Col("Condition", 60),
                new Col("Vendor", 80),
                new Col("Active", 45)
            };

            // Ensure the table fits (shrink Name if needed)
            var total = columns.Sum(c => c.Width);
            if (total > tableWidth)
            {
                var excess = total - tableWidth;
                var nameCol = columns.FirstOrDefault(c => c.Title == "Name");
                if (nameCol != null)
                    nameCol.Width = Math.Max(80, nameCol.Width - excess);
            }

            var penBorder = new XPen(XColor.FromArgb(220, 220, 220), 0.8);
            var brushHeaderBack = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var brushZebra = new XSolidBrush(XColor.FromArgb(251, 252, 253));
            var brushSection = new XSolidBrush(XColor.FromArgb(235, 245, 255));
            var brushSubtotal = new XSolidBrush(XColor.FromArgb(240, 240, 240));

            const double headerH = 20;
            const double padX = 3;
            const double padY = 3;
            const double minRowH = 18;

            void DrawTableHeader()
            {
                var x = tableLeft;
                gfx.DrawRectangle(brushHeaderBack, tableLeft, y, tableWidth, headerH);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, headerH);

                foreach (var c in columns)
                {
                    gfx.DrawLine(penBorder, x, y, x, y + headerH);
                    gfx.DrawString(c.Title, fontHeader, XBrushes.Black, new XRect(x + padX, y + 3, c.Width - padX * 2, headerH - 6), XStringFormats.TopLeft);
                    x += c.Width;
                }
                gfx.DrawLine(penBorder, tableLeft + tableWidth, y, tableLeft + tableWidth, y + headerH);
                y += headerH;
            }

            void DrawSectionRow(string category, int itemCount, int stockSum)
            {
                var text = $"Category: {(string.IsNullOrWhiteSpace(category) ? "(Uncategorized)" : category)}";
                var right = $"{itemCount} item(s) • Stock sum: {stockSum}";

                const double h = 18;
                gfx.DrawRectangle(brushSection, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                gfx.DrawString(text, fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + padY, tableWidth * 0.7, h - padY * 2), XStringFormats.TopLeft);
                gfx.DrawString(right, fontSection, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                y += h;
            }

            void DrawSubtotalRow(int itemCount, int stockSum)
            {
                const double h = 18;
                gfx.DrawRectangle(brushSubtotal, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);

                var x = tableLeft;
                foreach (var c in columns)
                {
                    gfx.DrawLine(penBorder, x, y, x, y + h);
                    x += c.Width;
                }
                gfx.DrawLine(penBorder, tableLeft + tableWidth, y, tableLeft + tableWidth, y + h);

                // Write subtotal text in Name column and stock sum in Stock column
                var nameColLeft = tableLeft + columns[0].Width;
                gfx.DrawString($"Subtotal: {itemCount} item(s)", fontHeader, XBrushes.Black, new XRect(nameColLeft + padX, y + padY, columns[1].Width - padX * 2, h - padY * 2), XStringFormats.TopLeft);

                var stockColIndex = Array.FindIndex(columns, c => c.Title == "Stock");
                if (stockColIndex >= 0)
                {
                    var stockLeft = tableLeft + columns.Take(stockColIndex).Sum(c => c.Width);
                    gfx.DrawString(stockSum.ToString(), fontHeader, XBrushes.Black, new XRect(stockLeft + padX, y + padY, columns[stockColIndex].Width - padX * 2, h - padY * 2), XStringFormats.TopRight);
                }

                y += h;
            }

            void NewPage()
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;

                gfx.DrawString(title, fontTitle, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
                y += 26;
                gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
                y += 18;

                DrawTableHeader();
            }

            DrawTableHeader();

            var bottomLimit = page.Height - margin - 24;

            var groups = safeItems
                .GroupBy(i => (i.Category ?? string.Empty).Trim())
                .OrderBy(g => string.IsNullOrWhiteSpace(g.Key) ? "~~~" : g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var zebra = false;

            foreach (var g in groups)
            {
                var groupItems = g
                    .OrderBy(i => (i.Name ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.ItemId)
                    .ToList();

                var groupStockSum = groupItems.Sum(i => i.StockOnHand);

                // Ensure we have room for section header + at least one row
                if (y + 18 + minRowH > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                DrawSectionRow(g.Key, groupItems.Count, groupStockSum);

                foreach (var item in groupItems)
                {
                    // Pre-compute wrapped lines to determine row height
                    var cellTexts = new[]
                    {
                        item.ItemId.ToString(),
                        item.Name,
                        item.Category,
                        item.ItemType,
                        item.ModelNumber,
                        item.SerialNumber,
                        item.StockOnHand.ToString(),
                        item.DateCreated.ToString("yyyy-MM-dd"),
                        item.ConditionName,
                        item.VendorName,
                        item.Active ? "Yes" : "No"
                    };

                    var lineHeight = gfx.MeasureString("Ag", fontCell).Height;
                    var maxLines = 1;
                    for (var ci = 0; ci < columns.Length; ci++)
                    {
                        if (!columns[ci].Wrap)
                            continue;

                        var lines = WrapToLines(gfx, (cellTexts[ci] ?? string.Empty).Trim(), fontCell, columns[ci].Width - padX * 2, columns[ci].MaxLines);
                        if (lines.Count > maxLines)
                            maxLines = lines.Count;
                    }

                    var rowH = Math.Max(minRowH, padY * 2 + maxLines * lineHeight);

                    if (y + rowH > bottomLimit)
                    {
                        NewPage();
                        bottomLimit = page.Height - margin - 24;
                    }

                    if (zebra)
                        gfx.DrawRectangle(brushZebra, tableLeft, y, tableWidth, rowH);
                    zebra = !zebra;

                    var x = tableLeft;
                    gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, rowH);

                    void Cell(string text, int colIndex)
                    {
                        var col = columns[colIndex];
                        text = (text ?? string.Empty).Trim();

                        var contentW = col.Width - padX * 2;
                        var contentH = rowH - padY * 2;
                        var rect = new XRect(x + padX, y + padY, contentW, contentH);

                        if (!col.Wrap)
                        {
                            text = TrimToFit(gfx, text, fontCell, contentW);
                            gfx.DrawString(text, fontCell, XBrushes.Black, rect, col.RightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
                        }
                        else
                        {
                            var lines = WrapToLines(gfx, text, fontCell, contentW, col.MaxLines);
                            var ty = y + padY;
                            foreach (var line in lines)
                            {
                                var lineRect = new XRect(x + padX, ty, contentW, lineHeight);
                                gfx.DrawString(line, fontCell, XBrushes.Black, lineRect, col.RightAlign ? XStringFormats.TopRight : XStringFormats.TopLeft);
                                ty += lineHeight;
                                if (ty > y + rowH - padY)
                                    break;
                            }
                        }

                        x += col.Width;
                        gfx.DrawLine(penBorder, x, y, x, y + rowH);
                    }

                    for (var ci = 0; ci < columns.Length; ci++)
                        Cell(cellTexts[ci], ci);

                    y += rowH;
                }

                if (y + 18 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                DrawSubtotalRow(groupItems.Count, groupStockSum);
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
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            if (maxWidth <= 0)
                return string.Empty;

            var w = gfx.MeasureString(text, font).Width;
            if (w <= maxWidth)
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
            if (string.IsNullOrWhiteSpace(text))
                return lines;

            if (maxWidth <= 0)
            {
                lines.Add(string.Empty);
                return lines;
            }

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
                    // Single long word: trim it
                    var trimmed = TrimToFit(gfx, w, font, maxWidth);
                    lines.Add(trimmed);
                    current = string.Empty;
                    if (lines.Count >= maxLines)
                        break;
                }
            }

            if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
                lines.Add(current);

            // If we exceeded, ensure last line has ellipsis
            if (lines.Count > maxLines)
                lines = lines.Take(maxLines).ToList();

            if (lines.Count == maxLines)
            {
                // Ensure last line fits and indicates truncation when needed
                var rebuilt = string.Join(" ", words);
                var displayed = string.Join(" ", lines);
                if (!string.Equals(rebuilt, displayed, StringComparison.Ordinal))
                {
                    var last = lines[maxLines - 1];
                    lines[maxLines - 1] = TrimToFit(gfx, last, font, maxWidth);
                }
            }

            return lines;
        }
    }
}
