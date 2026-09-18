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
    public static class SetPdfGenerator
    {
        public static void GenerateSelectedSetsPdf(
            IReadOnlyList<SetDto> sets,
            string outputPath,
            string title = "Selected Sets",
            IReadOnlyDictionary<int, IReadOnlyList<SetDetailRequestDto>> itemsBySetId = null)
        {
            if (sets == null) throw new ArgumentNullException(nameof(sets));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = sets.Where(s => s != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Inventory Sets Export",
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

            gfx.DrawString(title, fontTitle, XBrushes.Black, new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
            y += 26;

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Sets: {safe.Count}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
            y += 18;

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;

            var columns = new[]
            {
                new Col("Set", 70),
                new Col("Type", 60),
                new Col("Status", 70),
                new Col("Company", 85, wrap: true, maxLines: 2),
                new Col("Employee", 105, wrap: true, maxLines: 2),
                new Col("Created", 80),
                new Col("Start", 70),
                new Col("End", 70),
                new Col("Items", 45, rightAlign: true),
                new Col("Days", 45, rightAlign: true),
                new Col("Doc #", 80),
                new Col("Ref #", 80),
            };

            var total = columns.Sum(c => c.Width);
            if (total > tableWidth)
            {
                var excess = total - tableWidth;
                // Reduce the biggest wrap columns first.
                foreach (var titleToReduce in new[] { "Employee", "Company" })
                {
                    var col = columns.FirstOrDefault(c => c.Title == titleToReduce);
                    if (col == null) continue;

                    var reducible = Math.Max(0, col.Width - 60);
                    var take = Math.Min(reducible, excess);
                    col.Width -= take;
                    excess -= take;
                    if (excess <= 0) break;
                }
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
            const double itemsIndent = 12;

            void DrawHeaderRow()
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
                DrawHeaderRow();
            }

            DrawHeaderRow();

            var bottomLimit = page.Height - margin - 24;
            var zebra = false;

            void DrawItemsTable(SetDto set, IReadOnlyList<SetDetailRequestDto> items)
            {
                if (set == null || items == null || items.Count == 0)
                    return;

                // Small spacer
                if (y + 12 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                y += 6;
                gfx.DrawString($"Items in {set.SetCode}:", fontSection, XBrushes.Black,
                    new XRect(tableLeft + itemsIndent, y, tableWidth - itemsIndent, 16), XStringFormats.TopLeft);
                y += 16;

                var itemsLeft = tableLeft + itemsIndent;
                var itemsWidth = tableWidth - itemsIndent;

                var itemCols = new[]
                {
                    new Col("Req", 45, rightAlign: true),
                    new Col("Item", 210, wrap: true, maxLines: 2),
                    new Col("Model", 90),
                    new Col("Serial", 95),
                    new Col("Category", 95),
                    new Col("Qty", 45, rightAlign: true),
                    new Col("Status", 80),
                };

                var itemTotal = itemCols.Sum(c => c.Width);
                if (itemTotal > itemsWidth)
                {
                    var excess = itemTotal - itemsWidth;
                    var itemCol = itemCols.FirstOrDefault(c => c.Title == "Item");
                    if (itemCol != null)
                        itemCol.Width = Math.Max(120, itemCol.Width - excess);
                }

                const double itemHeaderH = 18;

                void DrawItemHeader()
                {
                    if (y + itemHeaderH > bottomLimit)
                    {
                        NewPage();
                        bottomLimit = page.Height - margin - 24;
                    }

                    gfx.DrawRectangle(brushHeaderBack, itemsLeft, y, itemsWidth, itemHeaderH);
                    gfx.DrawRectangle(penBorder, itemsLeft, y, itemsWidth, itemHeaderH);

                    var x = itemsLeft;
                    foreach (var c in itemCols)
                    {
                        gfx.DrawLine(penBorder, x, y, x, y + itemHeaderH);
                        gfx.DrawString(c.Title, fontHeader, XBrushes.Black,
                            new XRect(x + padX, y + 2, c.Width - padX * 2, itemHeaderH - 4), XStringFormats.TopLeft);
                        x += c.Width;
                    }
                    gfx.DrawLine(penBorder, itemsLeft + itemsWidth, y, itemsLeft + itemsWidth, y + itemHeaderH);
                    y += itemHeaderH;
                }

                DrawItemHeader();

                var itemZebra = false;
                foreach (var it in items)
                {
                    var values = new[]
                    {
                        it == null ? "" : $"{it.ReqId}",
                        it?.ItemName ?? "",
                        it?.ModelNumber ?? "",
                        it?.SerialNumber ?? "",
                        it?.Category ?? "",
                        it == null ? "" : $"{it.Quantity}",
                        it?.Status ?? "",
                    };

                    var lineHeight = gfx.MeasureString("Ag", fontCell).Height;
                    var maxLines = 1;
                    for (var i = 0; i < itemCols.Length; i++)
                        if (itemCols[i].Wrap) maxLines = Math.Max(maxLines, itemCols[i].MaxLines);

                    var rowH = Math.Max(minRowH, (lineHeight * maxLines) + padY * 2);

                    if (y + rowH > bottomLimit)
                    {
                        NewPage();
                        bottomLimit = page.Height - margin - 24;
                        DrawItemHeader();
                    }

                    if (itemZebra) gfx.DrawRectangle(brushZebra, itemsLeft, y, itemsWidth, rowH);
                    itemZebra = !itemZebra;

                    gfx.DrawRectangle(penBorder, itemsLeft, y, itemsWidth, rowH);

                    var x = itemsLeft;
                    for (var ci = 0; ci < itemCols.Length; ci++)
                    {
                        var col = itemCols[ci];
                        var contentW = col.Width - padX * 2;
                        var rect = new XRect(x + padX, y + padY, contentW, rowH - padY * 2);

                        var text = (values[ci] ?? string.Empty).Trim();
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
                                gfx.DrawString(line, fontCell, XBrushes.Black, new XRect(x + padX, ty, contentW, lineHeight), XStringFormats.TopLeft);
                                ty += lineHeight;
                                if (ty > y + rowH - padY) break;
                            }
                        }

                        x += col.Width;
                        gfx.DrawLine(penBorder, x, y, x, y + rowH);
                    }

                    y += rowH;
                }

                // Spacer after items
                if (y + 6 <= bottomLimit)
                    y += 6;
            }

            void DrawSectionHeader(string sectionTitle, int count, int itemsSum)
            {
                if (y + 18 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                gfx.DrawRectangle(brushSection, tableLeft, y, tableWidth, 18);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, 18);

                gfx.DrawString($"Type: {sectionTitle}", fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + 2, tableWidth * 0.7, 16), XStringFormats.TopLeft);
                gfx.DrawString($"{count} set(s) • Items sum: {itemsSum}", fontSection, XBrushes.Black, new XRect(tableLeft, y + 2, tableWidth - padX, 16), XStringFormats.TopRight);
                y += 18;
                zebra = false;
            }

            void DrawSubtotal(int count, int itemsSum)
            {
                if (y + 18 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                gfx.DrawRectangle(brushSubtotal, tableLeft, y, tableWidth, 18);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, 18);
                gfx.DrawString($"Subtotal: {count} set(s)", fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + 2, tableWidth - padX * 2, 16), XStringFormats.TopLeft);
                gfx.DrawString($"{itemsSum}", fontSection, XBrushes.Black, new XRect(tableLeft, y + 2, columns.Take(9).Sum(c => c.Width) - padX, 16), XStringFormats.TopRight);
                y += 18;
            }

            var groups = safe
                .GroupBy(s => string.IsNullOrWhiteSpace(s.SetType) ? "Unspecified" : s.SetType.Trim())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in groups)
            {
                var groupRows = g
                    .OrderBy(s => s.SetCode, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(s => s.SetId)
                    .ToList();

                var itemsSum = groupRows.Sum(r => r?.ItemCount ?? 0);
                DrawSectionHeader(g.Key, groupRows.Count, itemsSum);

                foreach (var s in groupRows)
                {
                    var values = new[]
                    {
                        s?.SetCode ?? "",
                        s?.SetType ?? "",
                        s?.SetStatus ?? "",
                        s?.CurrentCompanyName ?? s?.Company ?? "",
                        s?.CurrentEmployeeName ?? "",
                        s == null ? "" : $"{s.CreatedAt:yyyy-MM-dd}",
                        s?.StartDate == null ? "" : $"{s.StartDate:yyyy-MM-dd}",
                        s?.EndDate == null ? "" : $"{s.EndDate:yyyy-MM-dd}",
                        s == null ? "" : $"{s.ItemCount}",
                        s?.DaysLeft == null ? "" : $"{s.DaysLeft}",
                        s?.DocumentNumber ?? "",
                        s?.ReferenceNumber ?? "",
                    };

                    var lineHeight = gfx.MeasureString("Ag", fontCell).Height;
                    var maxLines = 1;
                    for (var i = 0; i < columns.Length; i++)
                        if (columns[i].Wrap) maxLines = Math.Max(maxLines, columns[i].MaxLines);
                    var rowH = Math.Max(minRowH, (lineHeight * maxLines) + padY * 2);

                    if (y + rowH > bottomLimit)
                    {
                        NewPage();
                        bottomLimit = page.Height - margin - 24;
                    }

                    if (zebra) gfx.DrawRectangle(brushZebra, tableLeft, y, tableWidth, rowH);
                    zebra = !zebra;

                    gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, rowH);

                    var x = tableLeft;
                    for (var ci = 0; ci < columns.Length; ci++)
                    {
                        var col = columns[ci];
                        var contentW = col.Width - padX * 2;
                        var rect = new XRect(x + padX, y + padY, contentW, rowH - padY * 2);

                        var text = (values[ci] ?? string.Empty).Trim();
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
                                gfx.DrawString(line, fontCell, XBrushes.Black, new XRect(x + padX, ty, contentW, lineHeight), XStringFormats.TopLeft);
                                ty += lineHeight;
                                if (ty > y + rowH - padY) break;
                            }
                        }

                        x += col.Width;
                        gfx.DrawLine(penBorder, x, y, x, y + rowH);
                    }

                    y += rowH;

                    if (s != null && itemsBySetId != null && itemsBySetId.TryGetValue(s.SetId, out var items) && items != null && items.Count > 0)
                        DrawItemsTable(s, items);
                }

                if (y + 18 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                DrawSubtotal(groupRows.Count, itemsSum);
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
                    if (lines.Count >= maxLines) break;
                }
                else
                {
                    lines.Add(TrimToFit(gfx, w, font, maxWidth));
                    current = string.Empty;
                    if (lines.Count >= maxLines) break;
                }
            }

            if (lines.Count < maxLines && !string.IsNullOrEmpty(current))
                lines.Add(current);

            if (lines.Count > maxLines)
                lines = lines.Take(maxLines).ToList();

            // Add ellipsis to the last line if we truncated by maxLines
            if (lines.Count == maxLines && words.Length > 0)
            {
                var joined = string.Join(" ", lines);
                if (!string.Equals(joined, text, StringComparison.Ordinal))
                    lines[lines.Count - 1] = TrimToFit(gfx, lines[lines.Count - 1], font, maxWidth);
            }

            return lines;
        }
    }
}
