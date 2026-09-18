using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using PdfSharp.Drawing;
using PdfSharp.Pdf;

namespace Yakult.Inventory.App.Helpers
{
    public sealed class TicketSignOffPhoto
    {
        public string FileName { get; set; }
        public int SizeBytes { get; set; }
        public string UploadedBy { get; set; }
        public DateTime UploadedAt { get; set; }
        public byte[] ThumbBytes { get; set; }
    }

    public sealed class TicketSignOffVisit
    {
        public int VisitId { get; set; }
        public string Status { get; set; }
        public string TechnicianName { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Location { get; set; }
        public string Notes { get; set; }
        public bool HasSignature { get; set; }
        public List<TicketSignOffPhoto> Photos { get; set; } = new List<TicketSignOffPhoto>();
    }

    public sealed class TicketSignOffHistoryRow
    {
        public DateTime? ChangedAt { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }
    }

    public sealed class TicketSignOffReportModel
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public string Status { get; set; }
        public string Priority { get; set; }
        public string Caller { get; set; }
        public string Department { get; set; }
        public string Branch { get; set; }
        public string Assignee { get; set; }
        public string Issue { get; set; }
        public string Solution { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? SolvedAt { get; set; }
        public int AgeDays { get; set; }
        public List<string> HistoryLines { get; set; } = new List<string>();
        public List<TicketSignOffHistoryRow> HistoryRows { get; set; } = new List<TicketSignOffHistoryRow>();
        public int HistoryTotalCount { get; set; }
        public string ResolutionTypeLabel { get; set; }
        public List<string> NotesLines { get; set; } = new List<string>();
        public List<TicketSignOffVisit> Visits { get; set; } = new List<TicketSignOffVisit>();
        public List<string> ResolutionLines { get; set; } = new List<string>();
        public List<string> EmailLines { get; set; } = new List<string>();
        public byte[] CustomerSignatureBytes { get; set; }
        public string CustomerName { get; set; }
        public string TechnicianName { get; set; }
        public DateTime? TechnicianSignedAt { get; set; }
        public string ReportHash { get; set; }
        public string GeneratedBy { get; set; }
        public DateTime GeneratedAt { get; set; }
    }

    public static class FieldVisitCompletionPdfGenerator
    {
        // Yakult brand + supporting palette.
        private static readonly XColor YakultRed = XColor.FromArgb(227, 6, 19);
        private static readonly XColor Ink = XColor.FromArgb(30, 41, 59);
        private static readonly XColor Muted = XColor.FromArgb(100, 116, 139);
        private static readonly XColor Border = XColor.FromArgb(226, 232, 240);
        private static readonly XColor HeaderFill = XColor.FromArgb(241, 245, 249);
        private static readonly XColor IssueBg = XColor.FromArgb(254, 242, 242);
        private static readonly XColor ResolutionBg = XColor.FromArgb(240, 253, 244);
        private static readonly XColor Green = XColor.FromArgb(22, 163, 74);
        private static readonly XColor Amber = XColor.FromArgb(217, 119, 6);

        private const int TimelineMaxRows = 15;

        public static string Generate(TicketSignOffReportModel model, string outputPath)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentNullException(nameof(outputPath));

            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var code = model.TicketCode ?? ("#" + model.TicketId);
            var doc = new PdfDocument
            {
                Info = { Title = "Field Work Completion Report " + code, Subject = "Yakult IT Call Completion Sign-Off", Author = "Yakult.Inventory.App" }
            };

            var page = doc.AddPage();
            page.Size = PdfSharp.PageSize.A4;

            var gfx = XGraphics.FromPdfPage(page);
            var fontTitle = new XFont("Segoe UI", 19, XFontStyle.Bold);
            var fontPill = new XFont("Segoe UI", 10, XFontStyle.Bold);
            var fontMeta = new XFont("Segoe UI", 9, XFontStyle.Regular);
            var fontH2 = new XFont("Segoe UI", 11, XFontStyle.Bold);
            var fontLabel = new XFont("Segoe UI", 8.5, XFontStyle.Regular);
            var fontValue = new XFont("Segoe UI", 9.5, XFontStyle.Regular);
            var fontBody = new XFont("Segoe UI", 10, XFontStyle.Regular);
            var fontSmall = new XFont("Segoe UI", 8.5, XFontStyle.Regular);
            var fontCell = new XFont("Segoe UI", 9, XFontStyle.Regular);
            var fontCellBold = new XFont("Segoe UI", 9, XFontStyle.Bold);
            var fontFooter = new XFont("Segoe UI", 8, XFontStyle.Regular);

            const double margin = 44;
            var y = margin;
            var contentW = page.Width - margin * 2;
            var brushInk = new XSolidBrush(Ink);
            var brushMuted = new XSolidBrush(Muted);
            var brushRed = new XSolidBrush(YakultRed);
            var brushGreen = new XSolidBrush(Green);
            var brushWhite = XBrushes.White;
            var penBorder = new XPen(Border, 0.8);

            Func<double, bool> ensure = null;
            Action newPage = null;
            newPage = () =>
            {
                try { if (gfx != null) gfx.Dispose(); } catch { }
                page = doc.AddPage();
                page.Size = PdfSharp.PageSize.A4;
                gfx = XGraphics.FromPdfPage(page);
                contentW = page.Width - margin * 2;
                y = margin;
            };
            ensure = (h) =>
            {
                if (y + h > page.Height - margin - 34) { newPage(); return true; }
                return false;
            };

            // ── Banner (full-bleed Yakult red) ────────────────────────────────
            const double bannerH = 118;
            gfx.DrawRectangle(new XSolidBrush(YakultRed), 0, 0, page.Width, bannerH);
            gfx.DrawString("FIELD WORK COMPLETION REPORT", fontTitle, brushWhite,
                new XRect(margin, 20, contentW, 26), XStringFormats.TopLeft);
            var pillText = (model.Status ?? "-").Trim().ToUpperInvariant();
            var pillTW = gfx.MeasureString(pillText, fontPill).Width;
            var pillW = pillTW + 30;
            const double pillH = 24;
            var pillX = margin + contentW - pillW;
            gfx.DrawRoundedRectangle(new XPen(XColors.White, 1), new XSolidBrush(XColors.White),
                new XRect(pillX, 20, pillW, pillH), new XSize(12, 12));
            var pillBrush = pillText.IndexOf("TEMPORARY", StringComparison.OrdinalIgnoreCase) >= 0
                ? new XSolidBrush(Amber) : brushRed;
            gfx.DrawString(pillText, fontPill, pillBrush,
                new XRect(pillX, 20, pillW, pillH), XStringFormats.Center);
            var whitish = new XSolidBrush(XColor.FromArgb(255, 255, 255));
            gfx.DrawLine(new XPen(XColor.FromArgb(120, 255, 255, 255), 0.8), margin, 66, margin + contentW, 66);
            gfx.DrawString("Report No.: " + code, fontMeta, whitish,
                new XRect(margin, 76, contentW / 2, 14), XStringFormats.TopLeft);
            var genText = "Generated: " + model.GeneratedAt.ToString("MMM d, yyyy h:mm tt");
            var genW = gfx.MeasureString(genText, fontMeta).Width;
            gfx.DrawString(genText, fontMeta, whitish,
                new XRect(margin + contentW - genW, 76, genW, 14), XStringFormats.TopLeft);
            y = bannerH + 18;
            contentW = page.Width - margin * 2;

            var firstVisit = model.Visits.FirstOrDefault();

            // ── Info grid (two columns) ───────────────────────────────────────
            SectionHead(ref gfx, ref y, contentW, margin, brushRed, fontH2, "TICKET INFORMATION", "PERSONNEL & ASSIGNMENT");
            InfoRows(ref gfx, ref y, contentW, margin, penBorder, fontLabel, fontValue, brushMuted,
                new[] { "Department", "Branch", "Priority", "Created", "Solved", "Age" },
                new[]
                {
                    OrDash(model.Department),
                    OrDash(model.Branch),
                    OrDash(model.Priority),
                    AsLocal(model.CreatedAt).ToString("MMM d, yyyy h:mm tt"),
                    model.SolvedAt.HasValue ? AsLocal(model.SolvedAt.Value).ToString("MMM d, yyyy h:mm tt") : "—",
                    model.AgeDays + (model.AgeDays == 1 ? " day" : " days")
                },
                new[] { "Caller", "Technician", "Field Tech", "Visit #", "Schedule" },
                new[]
                {
                    OrDash(model.Caller),
                    OrDash(model.Assignee),
                    OrDash(firstVisit != null ? firstVisit.TechnicianName : null),
                    firstVisit != null ? "Field Visit #" + firstVisit.VisitId : "—",
                    firstVisit != null && firstVisit.ScheduledAt.HasValue
                        ? AsLocal(firstVisit.ScheduledAt.Value).ToString("MMM d, yyyy h:mm tt") : "—"
                });
            y += 14;

            // ── Issue + resolution cards ──────────────────────────────────────
            AccentCard(ref gfx, ref y, contentW, margin, penBorder, fontH2, fontBody, fontSmall,
                YakultRed, IssueBg, brushRed, "REPORTED ISSUE", OrDash(model.Issue));
            var resType = string.IsNullOrWhiteSpace(model.ResolutionTypeLabel) ? "SERVICE ONLY" : model.ResolutionTypeLabel.Trim();
            // Resolution meta (department + responsible person) always shows
            // alongside the solution text - never suppressed by it.
            var resMetaRaw = model.ResolutionLines
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();
            // Stable card order: responsible person, department, then type.
            var resMeta = HumanizeResolutionLines(resMetaRaw
                    .Where(s => s.TrimStart().StartsWith("ResolutionResponsiblePerson", StringComparison.OrdinalIgnoreCase))
                    .ToList());
            resMeta.AddRange(HumanizeResolutionLines(resMetaRaw
                    .Where(s => s.TrimStart().StartsWith("ResolutionDepartment", StringComparison.OrdinalIgnoreCase))
                    .ToList()));
            resMeta.AddRange(HumanizeResolutionLines(resMetaRaw
                    .Where(s => s.TrimStart().StartsWith("ResolutionType", StringComparison.OrdinalIgnoreCase))
                    .ToList()));
            var resParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(model.Solution))
                resParts.Add(model.Solution.Trim());
            resParts.AddRange(resMeta.Where(s => !string.IsNullOrWhiteSpace(s)));
            var resBody = resParts.Count > 0 ? string.Join("  •  ", resParts) : "—";
            AccentCard(ref gfx, ref y, contentW, margin, penBorder, fontH2, fontBody, fontSmall,
                Green, ResolutionBg, brushGreen, "RESOLUTION SUMMARY (" + resType + ")", string.IsNullOrWhiteSpace(resBody) ? "—" : resBody);
            y += 6;

            // ── Timeline table ────────────────────────────────────────────────
            SectionHead(ref gfx, ref y, contentW, margin, brushRed, fontH2, "STATUS TIMELINE", null);
            DrawTimeline(ref gfx, ref y, contentW, margin, penBorder, fontSmall, fontCell, fontCellBold, brushMuted, ensure, model);
            y += 6;

            // ── Evidence (only when photos exist) ─────────────────────────────
            var allPhotos = model.Visits
                .SelectMany(v => v.Photos ?? Enumerable.Empty<TicketSignOffPhoto>())
                .Where(p => p != null)
                .ToList();
            if (allPhotos.Count > 0)
            {
                SectionHead(ref gfx, ref y, contentW, margin, brushRed, fontH2,
                    "EVIDENCE (" + allPhotos.Count + (allPhotos.Count == 1 ? " PHOTO)" : " PHOTOS)"), null);
                DrawPhotoGrid(ref gfx, ref y, contentW, margin, penBorder, fontSmall, brushMuted, ensure, allPhotos);
                y += 6;
            }

            // ── Notes (only when present) ─────────────────────────────────────
            var noteLines = model.NotesLines.Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
            if (noteLines.Count > 0)
            {
                SectionHead(ref gfx, ref y, contentW, margin, brushRed, fontH2, "NOTES", null);
                DrawBullets(ref gfx, ref y, contentW, margin, fontSmall, ensure, noteLines);
                y += 6;
            }

            // ── Sign-off page: fresh page only when the cards would not fit ────
            // (an unconditional break strands half-empty pages like the old layout did).
            const double signBlockH = 300;
            if (y + signBlockH > page.Height - margin - 34)
                newPage();
            SectionHead(ref gfx, ref y, contentW, margin, brushRed, fontH2, "CUSTOMER SIGN-OFF & ATTESTATION", null);
            y += 4;
            var cardW = (contentW - 14) / 2;
            const double cardH = 210;
            ensure(cardH + 10);
            var cardY = y;
            // Customer card
            DrawSignCard(ref gfx, margin, cardY, cardW, cardH, penBorder, fontH2, fontSmall, brushMuted,
                "CUSTOMER SIGNATURE",
                model.CustomerSignatureBytes, "No signature captured",
                model.CustomerName, "CUSTOMER",
                model.GeneratedAt.ToString("MMM d, yyyy"));
            // Technician card (fixed attestation template, no image by design)
            DrawAttestCard(ref gfx, margin + cardW + 14, cardY, cardW, cardH, penBorder, fontH2, fontSmall, brushMuted,
                "TECHNICIAN ATTESTATION",
                "“Attested no image required. Verified and signed on file.”",
                model.TechnicianName, "TECHNICIAN-ATTESTED",
                model.TechnicianSignedAt.HasValue ? AsLocal(model.TechnicianSignedAt.Value).ToString("MMM d, yyyy") : string.Empty);
            y = cardY + cardH + 10;

            // ── Two-pass footers (totals known only now) ──────────────────────
            // Release the working surface first: a page accepts only one live
            // XGraphics at a time.
            gfx.Dispose();
            gfx = null;
            for (var i = 0; i < doc.Pages.Count; i++)
            {
                var pg = doc.Pages[i];
                var g = XGraphics.FromPdfPage(pg);
                try
                {
                    g.DrawString("Report No: " + code + "  |  Confidential", fontFooter, brushMuted,
                        new XRect(margin, pg.Height - 28, contentW, 14), XStringFormats.TopLeft);
                    var pr = "Page " + (i + 1) + " of " + doc.Pages.Count;
                    var pw = g.MeasureString(pr, fontFooter).Width;
                    g.DrawString(pr, fontFooter, brushMuted,
                        new XRect(margin + contentW - pw, pg.Height - 28, pw, 14), XStringFormats.TopLeft);
                }
                finally
                {
                    g.Dispose();
                }
            }

            doc.Save(outputPath);
            return outputPath;
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

        private static void SectionHead(ref XGraphics gfx, ref double y, double contentW, double margin,
            XSolidBrush accent, XFont font, string left, string right)
        {
            gfx.DrawString(left ?? string.Empty, font, accent, new XRect(margin, y, contentW, 16), XStringFormats.TopLeft);
            if (!string.IsNullOrWhiteSpace(right))
            {
                var w = gfx.MeasureString(right, font).Width;
                gfx.DrawString(right, font, accent, new XRect(margin + contentW - w, y, w, 16), XStringFormats.TopLeft);
            }
            y += 18;
            gfx.DrawLine(new XPen(Border, 0.8), margin, y, margin + contentW, y);
            y += 10;
        }

        private static void InfoRows(ref XGraphics gfx, ref double y, double contentW, double margin,
            XPen penBorder, XFont fontLabel, XFont fontValue, XSolidBrush brushMuted,
            string[] leftLabels, string[] leftValues, string[] rightLabels, string[] rightValues)
        {
            var rows = Math.Max(leftLabels.Length, rightLabels.Length);
            const double rowH = 27;
            var colW = contentW / 2;
            for (var r = 0; r < rows; r++)
            {
                DrawInfoCell(ref gfx, ref y, margin, colW - 14, rowH, penBorder, fontLabel, fontValue, brushMuted,
                    r < leftLabels.Length ? leftLabels[r] : null, r < leftValues.Length ? leftValues[r] : null);
                DrawInfoCell(ref gfx, ref y, margin + colW + 14, colW - 14, rowH, penBorder, fontLabel, fontValue, brushMuted,
                    r < rightLabels.Length ? rightLabels[r] : null, r < rightValues.Length ? rightValues[r] : null);
                y += rowH;
            }
        }

        private static void DrawInfoCell(ref XGraphics gfx, ref double y, double x, double w, double rowH,
            XPen penBorder, XFont fontLabel, XFont fontValue, XSolidBrush brushMuted,
            string label, string value)
        {
            if (string.IsNullOrWhiteSpace(label) && string.IsNullOrWhiteSpace(value)) return;
            var lx = x + 100;
            gfx.DrawString(label ?? string.Empty, fontLabel, brushMuted, new XRect(x, y + 6, 100, 14), XStringFormats.TopLeft);
            gfx.DrawString(TrimToFit(gfx, value ?? "—", fontValue, w - 104), fontValue, XBrushes.Black,
                new XRect(lx, y + 5, w - 104, 15), XStringFormats.TopLeft);
            gfx.DrawLine(penBorder, x, y + rowH - 1, x + w, y + rowH - 1);
        }

        private static void AccentCard(ref XGraphics gfx, ref double y, double contentW, double margin,
            XPen penBorder, XFont fontH2, XFont fontBody, XFont fontSmall,
            XColor accent, XColor fill, XSolidBrush titleBrush, string title, string body)
        {
            var lines = WrapToLines(gfx, body ?? "—", fontBody, contentW - 36, 12);
            if (lines.Count == 0) lines.Add("—");
            var lh = gfx.MeasureString("Ag", fontBody).Height;
            var h = 34 + lines.Count * lh + 14;
            // Cards never split: move the whole card when it does not fit.
            if (y + h > gfx.PageSize.Height - margin - 34)
                y = gfx.PageSize.Height - margin - 34 - h;
            DrawAccentCardAt(gfx, y, contentW, margin, penBorder, fontH2, fontBody, accent, fill, titleBrush, title, lines, lh);
            y += h + 12;
        }

        private static void DrawAccentCardAt(XGraphics gfx, double y, double contentW, double margin,
            XPen penBorder, XFont fontH2, XFont fontBody,
            XColor accent, XColor fill, XSolidBrush titleBrush, string title, List<string> lines, double lh)
        {
            var h = 34 + lines.Count * lh + 14;
            gfx.DrawRoundedRectangle(penBorder, new XSolidBrush(fill), new XRect(margin, y, contentW, h), new XSize(8, 8));
            gfx.DrawRectangle(new XSolidBrush(accent), margin + 1, y + 8, 4, h - 16);
            gfx.DrawString(title ?? string.Empty, fontH2, titleBrush, new XRect(margin + 18, y + 10, contentW - 36, 16), XStringFormats.TopLeft);
            var ty = y + 30;
            foreach (var ln in lines)
            {
                gfx.DrawString(ln, fontBody, XBrushes.Black, new XRect(margin + 18, ty, contentW - 36, lh + 2), XStringFormats.TopLeft);
                ty += lh;
            }
        }

        private static void DrawTimeline(ref XGraphics gfx, ref double y, double contentW, double margin,
            XPen penBorder, XFont fontSmall, XFont fontCell, XFont fontCellBold, XSolidBrush brushMuted,
            Func<double, bool> ensure, TicketSignOffReportModel model)
        {
            const double tsW = 150;
            const double stW = 132;
            var dtW = contentW - tsW - stW;
            const double headH = 24;

            ensure(headH + 30);
            y = DrawTimelineHead(gfx, y, contentW, margin, fontSmall, brushMuted, tsW, stW, dtW, headH);

            var rows = (model.HistoryRows ?? new List<TicketSignOffHistoryRow>()).Take(TimelineMaxRows).ToList();
            if (rows.Count == 0)
            {
                ensure(30);
                gfx.DrawString("No history recorded.", fontCell, brushMuted, new XRect(margin + 8, y + 6, contentW, 14), XStringFormats.TopLeft);
                y += 26;
            }
            var firstRow = true;
            foreach (var row in rows)
            {
                var ts = row.ChangedAt.HasValue ? AsLocal(row.ChangedAt.Value).ToString("MMM d, yyyy h:mm tt") : "—";
                var st = string.IsNullOrWhiteSpace(row.Status) ? "—" : row.Status.Trim();
                var dt = string.IsNullOrWhiteSpace(row.Details) ? "—" : row.Details.Trim();
                var dtLines = WrapToLines(gfx, dt, fontCell, dtW - 16, 6);
                if (dtLines.Count == 0) dtLines.Add("—");
                var lh = gfx.MeasureString("Ag", fontCell).Height;
                var rh = Math.Max(26, dtLines.Count * lh + 12);
                // Repeat the header when the table spans pages.
                var needHead = !firstRow && y <= margin + 0.5;
                ensure(rh + 4 + (needHead ? headH : 0));
                if (needHead)
                    y = DrawTimelineHead(gfx, y, contentW, margin, fontSmall, brushMuted, tsW, stW, dtW, headH);
                firstRow = false;
                gfx.DrawString(ts, fontCell, XBrushes.Black, new XRect(margin + 8, y + 6, tsW - 8, 14), XStringFormats.TopLeft);
                gfx.DrawString(TrimToFit(gfx, st, fontCellBold, stW - 8), fontCellBold, StatusBrush(st),
                    new XRect(margin + tsW, y + 6, stW - 8, 14), XStringFormats.TopLeft);
                var ty = y + 6;
                foreach (var ln in dtLines)
                {
                    gfx.DrawString(ln, fontCell, XBrushes.Black, new XRect(margin + tsW + stW, ty, dtW - 16, lh + 2), XStringFormats.TopLeft);
                    ty += lh;
                }
                y += rh;
                gfx.DrawLine(penBorder, margin, y, margin + contentW, y);
            }

            var total = model.HistoryTotalCount > 0 ? model.HistoryTotalCount : rows.Count;
            if (total > rows.Count)
            {
                ensure(22);
                gfx.DrawString("+" + (total - rows.Count) + " earlier events on file.", fontSmall, brushMuted,
                    new XRect(margin + 8, y + 4, contentW, 14), XStringFormats.TopLeft);
                y += 20;
            }
        }

        private static double DrawTimelineHead(XGraphics gfx, double y, double contentW, double margin,
            XFont fontSmall, XSolidBrush brushMuted, double tsW, double stW, double dtW, double headH)
        {
            gfx.DrawRectangle(new XSolidBrush(HeaderFill), margin, y, contentW, headH);
            gfx.DrawString("TIMESTAMP", fontSmall, brushMuted, new XRect(margin + 8, y + 6, tsW, 12), XStringFormats.TopLeft);
            gfx.DrawString("STATUS", fontSmall, brushMuted, new XRect(margin + tsW, y + 6, stW, 12), XStringFormats.TopLeft);
            gfx.DrawString("DETAILS", fontSmall, brushMuted, new XRect(margin + tsW + stW, y + 6, dtW, 12), XStringFormats.TopLeft);
            return y + headH;
        }

        private static XBrush StatusBrush(string status)
        {
            var s = (status ?? string.Empty).Trim();
            if (s.Equals("Solved", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Closed", StringComparison.OrdinalIgnoreCase))
                return new XSolidBrush(Green);
            if (s.IndexOf("Temporary", StringComparison.OrdinalIgnoreCase) >= 0)
                return new XSolidBrush(Amber);
            return XBrushes.Black;
        }

        private static void DrawBullets(ref XGraphics gfx, ref double y, double contentW, double margin,
            XFont font, Func<double, bool> ensure, List<string> lines)
        {
            var lh = gfx.MeasureString("Ag", font).Height;
            foreach (var raw in lines)
            {
                var wrapped = WrapToLines(gfx, raw ?? string.Empty, font, contentW - 24, 12);
                if (wrapped.Count == 0) wrapped.Add("-");
                var h = wrapped.Count * lh + 8;
                ensure(h);
                var ty = y + 4;
                gfx.DrawString("•", font, XBrushes.Black, new XRect(margin + 8, ty, 12, lh), XStringFormats.TopLeft);
                foreach (var ln in wrapped)
                {
                    gfx.DrawString(ln, font, XBrushes.Black, new XRect(margin + 22, ty, contentW - 30, lh), XStringFormats.TopLeft);
                    ty += lh;
                }
                y += h;
            }
            y += 4;
        }

        private static void DrawPhotoGrid(ref XGraphics gfx, ref double y, double contentW, double margin,
            XPen penBorder, XFont fontSmall, XSolidBrush brushMuted, Func<double, bool> ensure,
            List<TicketSignOffPhoto> allPhotos)
        {
            const int perRow = 3;
            const double cellW = 150;
            const double cellH = 110;
            const double cellPad = 6;
            var shown = allPhotos.Take(6).ToList();
            for (var i = 0; i < shown.Count; i += perRow)
            {
                var rowCount = Math.Min(perRow, shown.Count - i);
                ensure(cellH + cellPad);
                for (var k = 0; k < rowCount; k++)
                {
                    var p = shown[i + k];
                    var cx = margin + k * (cellW + cellPad);
                    DrawImageFit(gfx, p.ThumbBytes, new XRect(cx, y, cellW, cellH), fontSmall);
                }
                y += cellH + cellPad;
            }
            y += 4;
            if (allPhotos.Count > 6)
            {
                ensure(20);
                gfx.DrawString("+" + (allPhotos.Count - 6) + " more photos on file.",
                    fontSmall, brushMuted, new XRect(margin, y, contentW, 14), XStringFormats.TopLeft);
                y += 18;
            }
        }

        private static void DrawSignCard(ref XGraphics gfx, double x, double cardY, double cardW, double cardH,
            XPen penBorder, XFont fontH2, XFont fontSmall, XSolidBrush brushMuted,
            string title, byte[] pngBytes, string emptyText, string name, string role, string dateText)
        {
            gfx.DrawRoundedRectangle(penBorder, XBrushes.White, new XRect(x, cardY, cardW, cardH), new XSize(10, 10));
            gfx.DrawString(title ?? string.Empty, fontSmall, brushMuted,
                new XRect(x, cardY + 14, cardW, 12), XStringFormats.TopCenter);
            var imgRect = new XRect(x + 14, cardY + 34, cardW - 28, 84);
            if (pngBytes != null && pngBytes.Length > 0)
                DrawImageFit(gfx, pngBytes, imgRect, fontSmall);
            else
                gfx.DrawString(emptyText ?? "No signature captured", fontSmall, XBrushes.Gray, imgRect, XStringFormats.Center);
            gfx.DrawLine(penBorder, x + 14, cardY + 124, x + cardW - 14, cardY + 124);
            var who = TitleName(name);
            var whoW = gfx.MeasureString(who, fontH2).Width;
            gfx.DrawString(who, fontH2, XBrushes.Black,
                new XRect(x + (cardW - whoW) / 2, cardY + 130, whoW, 18), XStringFormats.TopLeft);
            var sub = ((role ?? string.Empty) + (string.IsNullOrWhiteSpace(dateText) ? string.Empty : " | " + dateText)).Trim();
            gfx.DrawString(sub, fontSmall, brushMuted, new XRect(x, cardY + 152, cardW, 12), XStringFormats.TopCenter);
        }

        private static void DrawAttestCard(ref XGraphics gfx, double x, double cardY, double cardW, double cardH,
            XPen penBorder, XFont fontH2, XFont fontSmall, XSolidBrush brushMuted,
            string title, string quote, string name, string role, string dateText)
        {
            gfx.DrawRoundedRectangle(penBorder, XBrushes.White, new XRect(x, cardY, cardW, cardH), new XSize(10, 10));
            gfx.DrawString(title ?? string.Empty, fontSmall, brushMuted,
                new XRect(x, cardY + 14, cardW, 12), XStringFormats.TopCenter);
            var fontItalic = new XFont("Segoe UI", 9, XFontStyle.Italic);
            var lines = WrapToLines(gfx, quote ?? string.Empty, fontItalic, cardW - 40, 6);
            var ty = cardY + 40;
            foreach (var ln in lines)
            {
                gfx.DrawString(ln, fontItalic, XBrushes.Black, new XRect(x, ty, cardW, 14), XStringFormats.TopCenter);
                ty += 15;
            }
            gfx.DrawLine(penBorder, x + 14, cardY + 124, x + cardW - 14, cardY + 124);
            var who = TitleName(name);
            var whoW = gfx.MeasureString(who, fontH2).Width;
            gfx.DrawString(who, fontH2, XBrushes.Black,
                new XRect(x + (cardW - whoW) / 2, cardY + 130, whoW, 18), XStringFormats.TopLeft);
            var sub = ((role ?? string.Empty) + (string.IsNullOrWhiteSpace(dateText) ? string.Empty : " | " + dateText)).Trim();
            gfx.DrawString(sub, fontSmall, brushMuted, new XRect(x, cardY + 152, cardW, 12), XStringFormats.TopCenter);
        }

        private static string TitleName(string name)
        {
            var whoRaw = string.IsNullOrWhiteSpace(name) ? "—" : name.Trim();
            if (whoRaw == "—") return whoRaw;
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(whoRaw.ToLowerInvariant());
        }

        private static DateTime AsLocal(DateTime d)
        {
            var utc = d.Kind == DateTimeKind.Utc ? d : DateTime.SpecifyKind(d, DateTimeKind.Utc);
            return utc.ToLocalTime();
        }

        private static string OrDash(string s) { return string.IsNullOrWhiteSpace(s) ? "—" : s.Trim(); }

        private static string TrimToFit(XGraphics gfx, string text, XFont font, double maxWidth)
        {
            if (string.IsNullOrEmpty(text) || maxWidth <= 0) return string.Empty;
            if (gfx.MeasureString(text, font).Width <= maxWidth) return text;
            const string ellipsis = "…";
            var eW = gfx.MeasureString(ellipsis, font).Width;
            if (eW > maxWidth) return string.Empty;
            var s = text;
            while (s.Length > 1)
            {
                s = s.Substring(0, s.Length - 1);
                if (gfx.MeasureString(s, font).Width + eW <= maxWidth) return s + ellipsis;
            }
            return ellipsis;
        }

        private static List<string> WrapToLines(XGraphics gfx, string text, XFont font, double maxWidth, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrWhiteSpace(text) || maxWidth <= 0) return lines;
            var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var current = string.Empty;
            foreach (var w in words)
            {
                var candidate = string.IsNullOrEmpty(current) ? w : current + " " + w;
                if (gfx.MeasureString(candidate, font).Width <= maxWidth) { current = candidate; continue; }
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
            if (lines.Count < maxLines && !string.IsNullOrEmpty(current)) lines.Add(current);
            return lines;
        }

        private static void DrawImageFit(XGraphics gfx, byte[] raw, XRect bounds, XFont fontSmall)
        {
            if (raw == null || raw.Length == 0)
            {
                gfx.DrawString("No image", fontSmall, XBrushes.Gray, bounds, XStringFormats.Center);
                return;
            }
            var ext = raw.Length > 4 && raw[0] == 0x89 && raw[1] == 0x50 && raw[2] == 0x4E && raw[3] == 0x47 ? ".png" : ".jpg";
            var tmp = Path.Combine(Path.GetTempPath(), "YakultSignOff_" + Guid.NewGuid().ToString("N") + ext);
            try
            {
                File.WriteAllBytes(tmp, raw);
                using (var img = XImage.FromFile(tmp))
                {
                    var scale = Math.Min(bounds.Width / img.PixelWidth, bounds.Height / img.PixelHeight);
                    if (scale <= 0) scale = 1;
                    var w = img.PixelWidth * scale;
                    var h = img.PixelHeight * scale;
                    gfx.DrawImage(img, new XRect(bounds.X + (bounds.Width - w) / 2, bounds.Y + (bounds.Height - h) / 2, w, h));
                }
            }
            catch
            {
                gfx.DrawString("Invalid image", fontSmall, XBrushes.Gray, bounds, XStringFormats.Center);
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }

        private static List<string> HumanizeResolutionLines(IReadOnlyList<string> src)
        {
            var dst = new List<string>();
            if (src == null) return dst;
            var ti = CultureInfo.InvariantCulture.TextInfo;
            foreach (var raw in src)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var s = raw.Trim().Replace("[Resolution]", "Resolution").Replace("[Replacement]", "Replacement");
                if (s.StartsWith("Resolution", StringComparison.OrdinalIgnoreCase))
                {
                    s = s.Substring("Resolution".Length).TrimStart();
                    if (s.StartsWith(":"))
                        s = s.Substring(1).TrimStart();
                    if (s.Length == 0) continue;
                }
                var colon = s.IndexOf(':');
                if (colon > 0)
                {
                    var keyRaw = s.Substring(0, colon).Trim();
                    var val = s.Substring(colon + 1).Trim();
                    var key = SplitCamel(keyRaw);
                    if (key.IndexOf("person", StringComparison.OrdinalIgnoreCase) >= 0
                        || key.IndexOf("responsible", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        if (!string.IsNullOrWhiteSpace(val))
                            val = ti.ToTitleCase(val.ToLowerInvariant());
                    }
                    if (string.IsNullOrWhiteSpace(key) && string.IsNullOrWhiteSpace(val)) continue;
                    s = string.IsNullOrWhiteSpace(val) ? key : key + ": " + val;
                }
                else if (s.Length == 0) continue;
                dst.Add(s);
            }
            return dst;
        }

        private static string SplitCamel(string key)
        {
            if (string.IsNullOrEmpty(key)) return key ?? string.Empty;
            var sb = new StringBuilder(key.Length + 4);
            sb.Append(key[0]);
            for (var i = 1; i < key.Length; i++)
            {
                if (char.IsUpper(key[i]) && char.IsLower(key[i - 1]))
                    sb.Append(' ');
                sb.Append(key[i]);
            }
            return sb.ToString().Trim();
        }

        private static List<string> WrapBlock(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<string> { "—" };
            return text.Replace("\r", string.Empty).Split('\n').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
        }
    }
}
