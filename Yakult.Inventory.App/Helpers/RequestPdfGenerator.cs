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
    public static class RequestPdfGenerator
    {
        public static void GenerateSelectedRequestsPdf(IReadOnlyList<RequestDto> requests, string outputPath, string title = "Selected Requests")
        {
            if (requests == null) throw new ArgumentNullException(nameof(requests));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var safe = requests.Where(r => r != null).ToList();

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var doc = new PdfDocument
            {
                Info =
                {
                    Title = title,
                    Subject = "Yakult Inventory Requests Export",
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

            var meta = $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}    Requests: {safe.Count}";
            gfx.DrawString(meta, fontMeta, XBrushes.Gray, new XRect(margin, y, page.Width - margin * 2, 16), XStringFormats.TopLeft);
            y += 18;

            var tableLeft = margin;
            var tableWidth = page.Width - margin * 2;

            var columns = new[]
            {
                new Col("Req", 50, rightAlign: true),
                new Col("Item", 200, wrap: true, maxLines: 2),
                new Col("Model", 90),
                new Col("Serial", 95),
                new Col("Category", 95),
                new Col("Qty", 45, rightAlign: true),
                new Col("Entry", 85),
                new Col("Fixed", 55),
                new Col("Remarks", 210, wrap: true, maxLines: 2),
            };

            var total = columns.Sum(c => c.Width);
            if (total > tableWidth)
            {
                var excess = total - tableWidth;
                foreach (var titleToReduce in new[] { "Remarks", "Item" })
                {
                    var col = columns.FirstOrDefault(c => c.Title == titleToReduce);
                    if (col == null) continue;

                    var min = titleToReduce == "Remarks" ? 120 : 140;
                    var reducible = Math.Max(0, col.Width - min);
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
            }

            void DrawSection(string left, string right)
            {
                const double h = 18;
                gfx.DrawRectangle(brushSection, tableLeft, y, tableWidth, h);
                gfx.DrawRectangle(penBorder, tableLeft, y, tableWidth, h);
                gfx.DrawString(left, fontSection, XBrushes.Black, new XRect(tableLeft + padX, y + padY, tableWidth * 0.7, h - padY * 2), XStringFormats.TopLeft);
                gfx.DrawString(right, fontSection, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                y += h;
            }

            void DrawSubtotal(int count, int qtySum)
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

                var labelLeft = tableLeft + columns[0].Width;
                gfx.DrawString($"Subtotal: {count} item line(s)", fontHeader, XBrushes.Black, new XRect(labelLeft + padX, y + padY, tableWidth * 0.7, h - padY * 2), XStringFormats.TopLeft);
                gfx.DrawString($"Qty sum: {qtySum}", fontHeader, XBrushes.Black, new XRect(tableLeft, y + padY, tableWidth - padX, h - padY * 2), XStringFormats.TopRight);
                y += h;
            }

            var bottomLimit = page.Height - margin - 24;
            var zebra = false;

            string GetGroupKey(RequestDto r)
            {
                if (r == null) return "null";
                return r.SubmissionSessionId.HasValue
                    ? "SESSION:" + r.SubmissionSessionId.Value.ToString("N")
                    : "REQ:" + r.ReqId.ToString();
            }

            var groups = safe
                .GroupBy(GetGroupKey)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var lineHeight = gfx.MeasureString("Ag", fontCell).Height;

            foreach (var g in groups)
            {
                var groupRows = g
                    .OrderBy(r => r.ReqId)
                    .ToList();

                var head = groupRows.FirstOrDefault();
                var employee = string.IsNullOrWhiteSpace(head?.EmployeeName) ? "(Unknown)" : head.EmployeeName.Trim();
                var dateText = head?.DateRequested == null ? "" : head.DateRequested.Value.ToString("yyyy-MM-dd");
                var status = string.IsNullOrWhiteSpace(head?.Status) ? "(None)" : head.Status.Trim();
                var sessionShort = head?.SubmissionSessionId.HasValue == true
                    ? head.SubmissionSessionId.Value.ToString("N").Substring(0, 8)
                    : null;

                var headerLeft = $"Request: {employee}" + (string.IsNullOrWhiteSpace(dateText) ? "" : $" • {dateText}");
                var headerRight = $"Status: {status} • Lines: {groupRows.Count}" + (sessionShort == null ? "" : $" • Session: {sessionShort}");

                void DrawGroupHeader(string left, string right)
                {
                    if (y + 18 + headerH + minRowH > bottomLimit)
                    {
                        NewPage();
                        bottomLimit = page.Height - margin - 24;
                    }

                    DrawSection(left, right);
                    DrawHeaderRow();
                }

                DrawGroupHeader(headerLeft, headerRight);

                foreach (var r in groupRows)
                {
                    var values = new[]
                    {
                        r.ReqId.ToString(),
                        r.ItemName,
                        r.ModelNumber,
                        r.SerialNumber,
                        r.Category,
                        r.Quantity.ToString(),
                        r.EntryType,
                        r.IsTrackedAsset ? "Yes" : "No",
                        r.Remarks
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
                        DrawGroupHeader(headerLeft + " (cont.)", headerRight);
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

                if (y + 18 > bottomLimit)
                {
                    NewPage();
                    bottomLimit = page.Height - margin - 24;
                }

                DrawSubtotal(groupRows.Count, groupRows.Sum(x => x?.Quantity ?? 0));
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

            return lines;
        }
    }
}
