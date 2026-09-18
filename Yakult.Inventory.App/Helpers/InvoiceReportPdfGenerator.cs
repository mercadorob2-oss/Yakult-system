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
    public static class InvoiceReportPdfGenerator
    {
        public sealed class InvoiceItemRow
        {
            public string ItemName { get; set; }
            public string ModelNumber { get; set; }
            public string SerialNumber { get; set; }
            public string UnitOfMeasure { get; set; }
            public decimal Quantity { get; set; }
            public decimal UnitPrice { get; set; }
            public decimal Amount { get; set; }
            public DateTime? LineStartDate { get; set; }
            public DateTime? LineEndDate { get; set; }
            public DateTime? CreatedAt { get; set; }
        }

        public static void GenerateInvoiceReportsPdf(IReadOnlyList<SetDto> invoices, string outputPath, string title = "Invoice Reports")
        {
            if (invoices == null) throw new ArgumentNullException(nameof(invoices));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = invoices.Where(i => i != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Invoice Reports Export",
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

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Invoices: {safe.Count}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
            y += 18;

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;

            var columns = new[]
            {
                new Col("Doc #", 85),
                new Col("Ref #", 85),
                new Col("Inv Date", 75),
                new Col("Start", 70),
                new Col("End", 70),
                new Col("Company", 120, wrap: true, maxLines: 2),
                new Col("Status", 70),
                new Col("Days", 55, rightAlign: true),
                new Col("Total Due", 85, rightAlign: true),
            };

            var total = columns.Sum(c => c.Width);
            if (total > tableWidth)
            {
                var excess = total - tableWidth;
                var companyCol = columns.FirstOrDefault(c => c.Title == "Company");
                if (companyCol != null)
                    companyCol.Width = Math.Max(80, companyCol.Width - excess);
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
            var lineHeight = gfx.MeasureString("Ag", fontCell).Height;

            void DrawSectionHeader(string company, int count, decimal totalDue)
            {
                const double h = 18;
                if (y + h > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                gfx.DrawRectangle(brushSection, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                gfx.DrawString($"Company: {company}", fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + padY, tableWidth * 0.7, h - padY * 2), XStringFormats.TopLeft);
                gfx.DrawString($"{count} invoice(s) • Total Due: {totalDue:N2}", fontSection, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                y += h;
                zebra = false;
            }

            void DrawSubtotal(int count, decimal totalDue)
            {
                const double h = 18;
                if (y + h > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                gfx.DrawRectangle(brushSubtotal, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                gfx.DrawString($"Subtotal: {count} invoice(s)", fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + padY, tableWidth * 0.7, h - padY * 2), XStringFormats.TopLeft);
                gfx.DrawString($"{totalDue:N2}", fontSection, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                y += h;
            }

            var groups = safe
                .GroupBy(x => string.IsNullOrWhiteSpace(x.Company) ? "Unspecified" : x.Company.Trim())
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var g in groups)
            {
                var rows = g
                    .OrderByDescending(x => x.DocumentDate ?? DateTime.MinValue)
                    .ThenByDescending(x => x.SetId)
                    .ToList();

                var groupTotal = rows.Sum(x => x?.TotalAmountDue ?? 0m);
                DrawSectionHeader(g.Key, rows.Count, groupTotal);

                foreach (var inv in rows)
                {
                    var daysLeft = inv?.DaysLeft;
                    var daysText = daysLeft.HasValue ? (daysLeft.Value < 0 ? $"Expired {Math.Abs(daysLeft.Value)}" : daysLeft.Value.ToString()) : "";

                    var values = new[]
                    {
                        inv?.DocumentNumber ?? "",
                        inv?.ReferenceNumber ?? "",
                        inv?.DocumentDate == null ? "" : inv.DocumentDate.Value.ToString("yyyy-MM-dd"),
                        inv?.StartDate == null ? "" : inv.StartDate.Value.ToString("yyyy-MM-dd"),
                        inv?.EndDate == null ? "" : inv.EndDate.Value.ToString("yyyy-MM-dd"),
                        inv?.Company ?? "",
                        inv?.Status ?? "",
                        daysText,
                        inv == null ? "" : inv.TotalAmountDue.ToString("N2")
                    };

                    var maxLines = 1;
                    for (var ci = 0; ci < columns.Length; ci++)
                    {
                        if (!columns[ci].Wrap) continue;
                        var lines = WrapToLines(gfx, (values[ci] ?? string.Empty).Trim(), fontCell, columns[ci].Width - padX * 2, columns[ci].MaxLines);
                        if (lines.Count > maxLines) maxLines = lines.Count;
                    }
                    var rowH = Math.Max(minRowH, padY * 2 + maxLines * lineHeight);

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
                }

                DrawSubtotal(rows.Count, groupTotal);
            }

            doc.Save(outputPath);
        }

        public static void GenerateSelectedInvoicesPdf(
            IReadOnlyList<SetDto> invoices,
            string outputPath,
            string title = "Selected Invoices",
            IReadOnlyDictionary<int, IReadOnlyList<InvoiceItemRow>> itemsBySetId = null)
        {
            if (invoices == null) throw new ArgumentNullException(nameof(invoices));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = invoices.Where(i => i != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Selected Invoices Export",
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

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Invoices: {safe.Count}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
            y += 18;

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;

            var penBorder = new XPen(XColor.FromArgb(220, 220, 220), 0.8);
            var brushHeaderBack = new XSolidBrush(XColor.FromArgb(245, 247, 250));
            var brushZebra = new XSolidBrush(XColor.FromArgb(251, 252, 253));
            var brushSection = new XSolidBrush(XColor.FromArgb(235, 245, 255));
            var brushSubtotal = new XSolidBrush(XColor.FromArgb(240, 240, 240));

            const double padX = 3;
            const double padY = 3;
            const double minRowH = 18;

            void NewPage(string headerSuffix = null)
            {
                page = doc.AddPage();
                page.Orientation = PdfSharp.PageOrientation.Landscape;
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;

                gfx.DrawString(string.IsNullOrWhiteSpace(headerSuffix) ? title : title + " " + headerSuffix, fontTitle, XBrushes.Black,
                    new XRect(margin, y, page.Width - margin * 2, 24), XStringFormats.TopLeft);
                y += 26;
                gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
                y += 18;
            }

            double bottomLimit = page.Height - margin - 24;

            void DrawInvoiceHeader(SetDto inv)
            {
                const double h = 44;
                if (y + h > bottomLimit)
                {
                    NewPage("(cont.)");
                    bottomLimit = page.Height - margin - 24;
                }

                gfx.DrawRectangle(brushSection, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);

                var docNo = inv?.DocumentNumber ?? "";
                var refNo = inv?.ReferenceNumber ?? "";
                var company = inv?.Company ?? "";
                var site = inv?.Site ?? inv?.CurrentBranchName ?? "";
                var category = inv?.SetType ?? "";
                var status = inv?.Status ?? "";

                var bits = new List<string>();
                if (!string.IsNullOrWhiteSpace(site)) bits.Add($"Site: {site}");
                if (!string.IsNullOrWhiteSpace(category)) bits.Add($"Category: {category}");
                if (!string.IsNullOrWhiteSpace(status)) bits.Add($"Status: {status}");

                var leftRaw = $"{docNo}  •  Ref: {refNo}  •  {company}";
                if (bits.Count > 0)
                    leftRaw += "  •  " + string.Join("  •  ", bits);

                var left = TrimToFit(gfx, leftRaw, fontSection, tableWidth - padX * 2);
                var date = inv?.DocumentDate == null ? "" : inv.DocumentDate.Value.ToString("yyyy-MM-dd");
                var start = inv?.StartDate == null ? "" : inv.StartDate.Value.ToString("yyyy-MM-dd");
                var end = inv?.EndDate == null ? "" : inv.EndDate.Value.ToString("yyyy-MM-dd");
                var right = $"Inv: {date}  •  {start} → {end}";

                gfx.DrawString(left, fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + 4, tableWidth, 18), XStringFormats.TopLeft);
                gfx.DrawString(right, fontMeta, XBrushes.Black, new XRect(tableLeft + padX, y + 22, tableWidth * 0.75, 18), XStringFormats.TopLeft);

                var totals = $"Subtotal: {inv?.Subtotal ?? 0m:N2}   Total Due: {inv?.TotalAmountDue ?? 0m:N2}";
                gfx.DrawString(totals, fontSection, XBrushes.Black, new XRect(tableLeft, y + 22, tableWidth - padX, 18), XStringFormats.TopRight);

                y += h;
            }

            void DrawItemsTable(IReadOnlyList<InvoiceItemRow> items)
            {
                if (items == null || items.Count == 0)
                    return;

                var columns = new[]
                {
                    new Col("Item", 230, wrap: true, maxLines: 2),
                    new Col("Model", 90),
                    new Col("Serial", 95),
                    new Col("UOM", 55),
                    new Col("Qty", 50, rightAlign: true),
                    new Col("Unit", 70, rightAlign: true),
                    new Col("Amount", 80, rightAlign: true),
                    new Col("Start", 75),
                    new Col("End", 75),
                };

                var total = columns.Sum(c => c.Width);
                if (total > tableWidth)
                {
                    var excess = total - tableWidth;
                    var itemCol = columns.FirstOrDefault(c => c.Title == "Item");
                    if (itemCol != null)
                        itemCol.Width = Math.Max(140, itemCol.Width - excess);
                }

                const double headerH = 20;
                var x = tableLeft;

                if (y + headerH > bottomLimit)
                {
                    NewPage("(cont.)");
                    bottomLimit = page.Height - margin - 24;
                }

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

                var zebra = false;
                var lineHeight = gfx.MeasureString("Ag", fontCell).Height;

                foreach (var it in items)
                {
                    var values = new[]
                    {
                        it?.ItemName ?? "",
                        it?.ModelNumber ?? "",
                        it?.SerialNumber ?? "",
                        it?.UnitOfMeasure ?? "",
                        it == null ? "" : it.Quantity.ToString("0.##"),
                        it == null ? "" : it.UnitPrice.ToString("N2"),
                        it == null ? "" : it.Amount.ToString("N2"),
                        it?.LineStartDate == null ? "" : it.LineStartDate.Value.ToString("yyyy-MM-dd"),
                        it?.LineEndDate == null ? "" : it.LineEndDate.Value.ToString("yyyy-MM-dd"),
                    };

                    var maxLines = 1;
                    for (var ci = 0; ci < columns.Length; ci++)
                    {
                        if (!columns[ci].Wrap) continue;
                        var lines = WrapToLines(gfx, (values[ci] ?? string.Empty).Trim(), fontCell, columns[ci].Width - padX * 2, columns[ci].MaxLines);
                        if (lines.Count > maxLines) maxLines = lines.Count;
                    }

                    var rowH = Math.Max(minRowH, padY * 2 + maxLines * lineHeight);
                    if (y + rowH > bottomLimit)
                    {
                        NewPage("(cont.)");
                        bottomLimit = page.Height - margin - 24;
                        DrawItemsTableHeader(columns, headerH);
                    }

                    if (zebra) gfx.DrawRectangle(brushZebra, tableLeft, y, tableWidth, rowH);
                    zebra = !zebra;

                    gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, rowH);

                    x = tableLeft;
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
                }

                void DrawItemsTableHeader(Col[] cols, double h)
                {
                    var hx = tableLeft;
                    gfx.DrawRectangle(brushHeaderBack, tableLeft, y, tableWidth, h);
                    gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                    foreach (var c in cols)
                    {
                        gfx.DrawLine(penBorder, hx, y, hx, y + h);
                        gfx.DrawString(c.Title, fontHeader, XBrushes.Black, new XRect(hx + padX, y + 3, c.Width - padX * 2, h - 6), XStringFormats.TopLeft);
                        hx += c.Width;
                    }
                    gfx.DrawLine(penBorder, tableLeft + tableWidth, y, tableLeft + tableWidth, y + h);
                    y += h;
                }
            }

            var ordered = safe
                .OrderBy(x => x.Company ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DocumentNumber ?? string.Empty, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.SetId)
                .ToList();

            foreach (var inv in ordered)
            {
                DrawInvoiceHeader(inv);

                IReadOnlyList<InvoiceItemRow> items = null;
                if (itemsBySetId != null)
                    itemsBySetId.TryGetValue(inv.SetId, out items);

                if (items == null || items.Count == 0)
                {
                    // spacer
                    if (y + 10 > bottomLimit) NewPage("(cont.)");
                    y += 6;
                    gfx.DrawString("No items found.", fontMeta, XBrushes.Gray,
                        new XRect(tableLeft + padX, y, tableWidth, 14), XStringFormats.TopLeft);
                    y += 18;
                }
                else
                {
                    DrawItemsTable(items);

                    // footer summary for the invoice items table
                    var totalDue = inv?.TotalAmountDue ?? 0m;
                    const double h = 18;
                    if (y + h > bottomLimit) NewPage("(cont.)");
                    gfx.DrawRectangle(brushSubtotal, tableLeft, y, tableWidth, h);
                    gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                    gfx.DrawString($"Items: {items.Count}", fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + padY, tableWidth * 0.5, h - padY * 2), XStringFormats.TopLeft);
                    gfx.DrawString($"Total Due: {totalDue:N2}", fontSection, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                    y += h + 10;
                }
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

            return lines;
        }
    }
}
