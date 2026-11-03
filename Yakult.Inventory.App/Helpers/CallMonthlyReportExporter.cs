using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ClosedXML.Excel;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using Yakult.Inventory.App.Models.CallMonitoring;

namespace Yakult.Inventory.App.Helpers
{
    public static class CallMonthlyReportExporter
    {
        private sealed class Table
        {
            public string Name;
            public string[] Headers;
            public List<object[]> Rows = new List<object[]>();
            public Table(string name, params string[] headers) { Name = name; Headers = headers; }
            public void Add(params object[] cells) { Rows.Add(cells); }
        }

        private static bool KnownOpen(string status) => !string.IsNullOrWhiteSpace(status) && status != "Unknown"
            && status != "Not yet created" && !CallMonthlyReport.IsClosed(status);
        private static object Date(DateTime? utc) => utc.HasValue ? (object)CallMonthlyReport.Local(utc.Value) : null;
        private static string Text(object value) => value is DateTime date ? date.ToString("yyyy-MM-dd HH:mm:ss")
            : value is double number ? number.ToString("0.00", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture) ?? "";

        private static List<Table> Build(CallMonthlyReport report)
        {
            var tickets = report.Tickets.ToDictionary(t => t.TicketId);
            var history = report.History.ToLookup(h => h.TicketId);
            var notes = report.Notes.ToLookup(n => n.TicketId);
            var monthHistory = report.History.Where(h => h.ChangedAt >= report.FromUtc).ToList();
            var summary = new Table("Summary", "Measure", "Value");
            summary.Add("Report", "Yakult IT Call Monitoring • Monthly Detailed Report");
            summary.Add("Month", report.MonthLocal.ToString("MMMM yyyy"));
            summary.Add("Reporting timezone", "Philippine time (UTC+08:00)");
            summary.Add("Generated", Date(report.GeneratedUtc));
            summary.Add("Cutoff (exclusive)", Date(report.CutoffUtc));
            summary.Add("Scope", "All new, carried-over, and active tickets in the month; independent of grid filters and selection. Current month is month-to-date.");
            summary.Add("Historical data", "Opening/cutoff status uses recorded history. Unknown values are retained separately. Other ticket reference details (including priority, assignment and issue text) are current values, not historical snapshots.");
            summary.Add("Notes and activity", "Full available history and notes before cutoff are included; each event is marked as inside or before the month. Missing/deleted records cannot be reconstructed.");
            summary.Add("Field work", "Visits edited after cutoff have mutable details withheld. Attachment counts are based on upload timestamps; a historical signature cannot be reconstructed after later edits.");
            summary.Add("Temporary resolutions", "Counted separately and retained in backlog until Solved or Closed.");
            summary.Add("Ticket count", tickets.Count);
            summary.Add("Opening backlog (confirmed)", tickets.Values.Count(t => t.CreatedAt < report.FromUtc && KnownOpen(t.OpeningStatus)));
            summary.Add("New tickets", tickets.Values.Count(t => t.CreatedAt >= report.FromUtc));
            summary.Add("Tickets with permanent completion events", monthHistory.Where(h => h.FieldName == "Status" && CallMonthlyReport.IsClosed(h.NewValue) && !CallMonthlyReport.IsClosed(h.OldValue)).Select(h => h.TicketId).Distinct().Count());
            summary.Add("Reopened tickets", monthHistory.Where(h => h.FieldName == "Status" && CallMonthlyReport.IsClosed(h.OldValue) && !CallMonthlyReport.IsClosed(h.NewValue)).Select(h => h.TicketId).Distinct().Count());
            summary.Add("Temporary resolutions at cutoff", tickets.Values.Count(t => t.ClosingStatus == "Resolved (Temporary)"));
            summary.Add("Closing backlog (confirmed)", tickets.Values.Count(t => KnownOpen(t.ClosingStatus)));
            summary.Add("Unknown opening statuses", tickets.Values.Count(t => t.OpeningStatus == "Unknown"));
            summary.Add("Unknown cutoff statuses", tickets.Values.Count(t => t.ClosingStatus == "Unknown"));
            var completionHours = monthHistory.Where(h => h.FieldName == "Status" && h.ChangedAt.HasValue
                && CallMonthlyReport.IsClosed(h.NewValue) && !CallMonthlyReport.IsClosed(h.OldValue))
                .GroupBy(h => h.TicketId).Select(g => Math.Max(0, (g.Max(h => h.ChangedAt.Value) - tickets[g.Key].CreatedAt).TotalHours))
                .OrderBy(h => h).ToList();
            summary.Add("Average elapsed resolution hours (monthly permanent completions)", completionHours.Count == 0 ? (object)"No completions" : completionHours.Average());
            summary.Add("Median elapsed resolution hours (monthly permanent completions)", completionHours.Count == 0 ? (object)"No completions"
                : (completionHours[(completionHours.Count - 1) / 2] + completionHours[completionHours.Count / 2]) / 2);
            summary.Add("Resolution timing basis", "Calendar hours from ticket creation to its latest permanent completion event within the month, one sample per ticket. Includes waiting time; not technician labor hours or business-hours SLA.");
            foreach (var warning in report.Warnings) summary.Add("Data availability", warning);

            var register = new Table("All Tickets", "Ticket", "Received", "Caller (current)", "Company (current)", "Department (current)", "Branch (current)",
                "Issue type (current)", "Priority (current)", "Responsible (current)", "Opening status", "Status at cutoff", "Problem (current)",
                "Solution recorded before cutoff", "Latest permanent completion", "Elapsed hours to completion", "Age at cutoff (days)", "Latest recorded activity", "Idle days at cutoff");
            var outstanding = new Table("Outstanding Tickets", "Ticket", "Status at cutoff", "Age days", "Last recorded activity", "Idle days", "Responsible (current)", "Latest recorded note");
            foreach (var t in tickets.Values.OrderBy(t => t.CreatedAt).ThenBy(t => t.TicketId))
            {
                var hs = history[t.TicketId].OrderBy(h => h.ChangedAt).ThenBy(h => h.HistoryId).ToList();
                var ns = notes[t.TicketId].OrderBy(n => n.CreatedAt).ThenBy(n => n.NoteId).ToList();
                var solved = hs.LastOrDefault(h => h.FieldName == "Status" && CallMonthlyReport.IsClosed(h.NewValue) && !CallMonthlyReport.IsClosed(h.OldValue));
                var solution = ns.LastOrDefault(n => n.NoteType == "Solution")?.NoteText
                    ?? (t.UpdatedAt < report.CutoffUtc ? t.ProvidedSolution : "Historical solution unavailable");
                var lastActivity = hs.Select(h => h.ChangedAt).Concat(ns.Select(n => n.CreatedAt))
                    .Concat(new DateTime?[] { t.UpdatedAt < report.CutoffUtc ? t.UpdatedAt : t.CreatedAt })
                    .Concat(report.Visits.Where(v => v.TicketId == t.TicketId).Select(v => (DateTime?)(v.UpdatedAt < report.CutoffUtc ? v.UpdatedAt : v.CreatedAt)))
                    .Where(d => d.HasValue).Select(d => d.Value).DefaultIfEmpty(t.CreatedAt).Max();
                var age = Math.Max(0, (report.CutoffUtc - t.CreatedAt).TotalDays);
                var idle = Math.Max(0, (report.CutoffUtc - lastActivity).TotalDays);
                object hours = solved?.ChangedAt != null ? (object)Math.Max(0, (solved.ChangedAt.Value - t.CreatedAt).TotalHours) : null;
                register.Add(t.TicketCode, Date(t.CreatedAt), t.CallerName, t.Company, t.Department, t.Branch, t.IssueType, t.Priority,
                    t.ResponsiblePerson, t.OpeningStatus, t.ClosingStatus, t.Issue, solution, Date(solved?.ChangedAt), hours, age, Date(lastActivity), idle);
                if (!CallMonthlyReport.IsClosed(t.ClosingStatus)) outstanding.Add(t.TicketCode, t.ClosingStatus, age, Date(lastActivity), idle, t.ResponsiblePerson, ns.LastOrDefault()?.NoteText);
            }
            var activity = new Table("Activity History", "Ticket", "When", "In reporting month", "Changed by", "Field", "Old value", "New value", "Remarks");
            foreach (var h in report.History) activity.Add(tickets[h.TicketId].TicketCode, Date(h.ChangedAt), h.ChangedAt >= report.FromUtc ? "Yes" : "Before month", h.ChangedByName, h.FieldName, h.OldValue, h.NewValue, h.Note);
            var noteTable = new Table("Notes and Solutions", "Ticket", "When", "In reporting month", "Recorded by", "Type", "Full note");
            foreach (var n in report.Notes) noteTable.Add(tickets[n.TicketId].TicketCode, Date(n.CreatedAt), n.CreatedAt >= report.FromUtc ? "Yes" : "Before month", n.CreatedByName, n.NoteType, n.NoteText);
            var replacements = new Table("Replacements", "Ticket", "When", "In reporting month", "Recorded by", "Field", "Recorded value", "Equipment reference (current)", "Remarks or batch reference");
            var items = report.Items.ToDictionary(i => i.ItemId);
            foreach (var h in report.History.Where(h => (h.FieldName ?? "").StartsWith("Replacement", StringComparison.OrdinalIgnoreCase) || h.FieldName == "ResolutionType"))
            {
                var ids = h.FieldName == "ReplacementBatchItem" ? (h.NewValue ?? "").Split(',').Take(2) : new[] { h.NewValue };
                var equipment = new List<string>();
                if (h.FieldName == "ReplacementBatchItem" || h.FieldName == "ReplacementOldItemId" || h.FieldName == "ReplacementNewItemId")
                    foreach (var id in ids) if (int.TryParse(id, out var itemId) && items.TryGetValue(itemId, out var item)) equipment.Add($"ID {item.ItemId}: {item.Name}; Model {item.ModelNumber}; Serial {item.SerialNumber}");
                replacements.Add(tickets[h.TicketId].TicketCode, Date(h.ChangedAt), h.ChangedAt >= report.FromUtc ? "Yes" : "Before month", h.ChangedByName, h.FieldName, h.NewValue, string.Join(" | ", equipment), h.Note);
            }
            summary.Add("Replacement interpretation", "Replacement rows are recorded events, not quantities to sum. BatchItem values are old item ID, new item ID, quantity; batch references distinguish repeated/revised resolutions.");
            var visits = new Table("Field Work", "Ticket", "Visit ID", "Created", "Scheduled", "Completed", "Status", "Technician", "Notes", "Signature available", "Attachments before cutoff", "Historical availability");
            foreach (var v in report.Visits)
            {
                var historical = v.UpdatedAt >= report.CutoffUtc;
                visits.Add(tickets[v.TicketId].TicketCode, v.FieldVisitId, Date(v.CreatedAt), historical ? null : Date(v.ScheduledAt), historical ? null : Date(v.CompletedAt),
                    historical ? "Unknown" : v.Status, historical ? null : v.TechnicianName, historical ? null : v.Notes,
                    historical ? "Unknown" : v.HasSignature ? "Yes" : "No", v.AttachmentCount, historical ? "Edited after cutoff; see recorded ticket history" : "Available");
            }
            var performance = new Table("Performance", "Dimension (current references)", "Group", "Tickets in monthly scope", "New", "Permanently complete at cutoff", "Temporary at cutoff", "Open at cutoff (confirmed)", "Unknown at cutoff");
            var dimensions = new Dictionary<string, Func<CallMonthlyTicket, string>> { { "Company", t => t.Company }, { "Branch", t => t.Branch }, { "Department", t => t.Department }, { "Issue type", t => t.IssueType }, { "Priority", t => t.Priority }, { "Responsible", t => t.ResponsiblePerson } };
            foreach (var dimension in dimensions)
                foreach (var group in tickets.Values.GroupBy(t => string.IsNullOrWhiteSpace(dimension.Value(t)) ? "Unspecified / Unassigned" : dimension.Value(t)).OrderBy(g => g.Key))
                    performance.Add(dimension.Key, group.Key, group.Count(), group.Count(t => t.CreatedAt >= report.FromUtc), group.Count(t => CallMonthlyReport.IsClosed(t.ClosingStatus)), group.Count(t => t.ClosingStatus == "Resolved (Temporary)"), group.Count(t => KnownOpen(t.ClosingStatus)), group.Count(t => t.ClosingStatus == "Unknown"));
            return new List<Table> { summary, register, activity, noteTable, replacements, visits, outstanding, performance };
        }

        public static void ExportExcel(CallMonthlyReport report, string path)
        {
            using (var workbook = new XLWorkbook())
            {
                foreach (var table in Build(report))
                {
                    if (table.Rows.Count > 1048575) throw new InvalidOperationException("This report exceeds Excel's worksheet row capacity. Export a smaller reporting scope.");
                    var sheet = workbook.Worksheets.Add(table.Name);
                    for (var c = 0; c < table.Headers.Length; c++) sheet.Cell(1, c + 1).Value = table.Headers[c];
                    for (var r = 0; r < table.Rows.Count; r++)
                        for (var c = 0; c < table.Rows[r].Length; c++)
                        {
                            var cell = sheet.Cell(r + 2, c + 1);
                            var value = table.Rows[r][c];
                            if (value is DateTime date) { cell.Value = date; cell.Style.DateFormat.Format = "yyyy-mm-dd hh:mm:ss"; }
                            else if (value is int || value is double) { cell.Value = value; cell.Style.NumberFormat.Format = "0.##"; }
                            else { cell.DataType = XLDataType.Text; cell.Value = Text(value); }
                        }
                    sheet.Row(1).Style.Font.Bold = true;
                    sheet.Row(1).Style.Fill.BackgroundColor = XLColor.FromHtml("#A91D32");
                    sheet.Row(1).Style.Font.FontColor = XLColor.White;
                    sheet.RangeUsed().Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                    sheet.RangeUsed().Style.Alignment.WrapText = true;
                    sheet.Columns(1, table.Headers.Length).Width = table.Name == "Summary" ? 55 : 24;
                    sheet.Column(1).Width = table.Name == "Summary" ? 38 : 22;
                    if (table.Name == "Summary") sheet.Column(2).Width = 100;
                    for (var c = 0; c < table.Headers.Length; c++)
                        if (table.Headers[c].Contains("note") || table.Headers[c].Contains("Problem") || table.Headers[c].Contains("Solution")) sheet.Column(c + 1).Width = 65;
                    // Excel has a maximum row height; full cell text remains available for very long notes.
                    for (var r = 0; r < table.Rows.Count; r++)
                    {
                        var lines = table.Rows[r].Select((v, c) => Text(v).Split('\n').Sum(line => Math.Max(1, (int)Math.Ceiling(line.Length / Math.Max(12, sheet.Column(c + 1).Width - 3))))).DefaultIfEmpty(1).Max();
                        sheet.Row(r + 2).Height = Math.Min(409, Math.Max(30, lines * 15 + 8));
                    }
                    sheet.Row(1).Height = 45;
                    sheet.SheetView.FreezeRows(1);
                    sheet.SheetView.FreezeColumns(1);
                    sheet.Range(1, 1, Math.Max(1, table.Rows.Count + 1), table.Headers.Length).SetAutoFilter();
                    sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape;
                    sheet.PageSetup.SetRowsToRepeatAtTop(1, 1);
                }
                workbook.SaveAs(path);
            }
        }

        public static void ExportPdf(CallMonthlyReport report, string path)
        {
            var tables = Build(report);
            using (var document = new PdfDocument())
            {
                var font = new XFont("Segoe UI", 9, XFontStyle.Regular);
                var heading = new XFont("Segoe UI", 12, XFontStyle.Bold);
                document.Info.Title = "IT Call Monitoring - " + report.MonthLocal.ToString("MMMM yyyy");
                document.Info.Author = "Yakult Inventory System";
                PdfPage page = null;
                XGraphics graphics = null;
                double y = 0;
                var context = "Monthly overview";
                Action newPage = () =>
                {
                    graphics?.Dispose();
                    page = document.AddPage(); page.Size = PdfSharp.PageSize.A4;
                    graphics = XGraphics.FromPdfPage(page);
                    graphics.DrawString(document.Info.Title, heading, XBrushes.DarkRed, new XRect(38, 24, page.Width.Point - 76, 24), XStringFormats.TopLeft);
                    graphics.DrawString("Philippine time | Generated " + CallMonthlyReport.Local(report.GeneratedUtc).ToString("yyyy-MM-dd HH:mm"), font, XBrushes.Gray, new XRect(38, 49, page.Width.Point - 76, 18), XStringFormats.TopLeft);
                    graphics.DrawString("Page " + document.PageCount, font, XBrushes.Gray, new XRect(38, page.Height.Point - 30, page.Width.Point - 76, 16), XStringFormats.TopLeft);
                    graphics.DrawString(context, font, XBrushes.Gray, new XRect(38, 65, page.Width.Point - 76, 16), XStringFormats.TopLeft);
                    y = 88;
                };
                Action<string, bool> write = (value, bold) =>
                {
                    var f = bold ? heading : font;
                    foreach (var paragraph in (value ?? "").Replace("\r", "").Split('\n'))
                    {
                        var rest = paragraph.Length == 0 ? " " : paragraph;
                        while (rest.Length > 0)
                        {
                            if (page == null || y > page.Height.Point - 54) newPage();
                            var low = 1; var high = rest.Length;
                            while (low < high) { var mid = (low + high + 1) / 2; if (graphics.MeasureString(rest.Substring(0, mid), f).Width <= page.Width.Point - 76) low = mid; else high = mid - 1; }
                            var count = low;
                            if (count < rest.Length) { var space = rest.LastIndexOf(' ', count - 1, count); if (space > 0) count = space + 1; }
                            graphics.DrawString(rest.Substring(0, count), f, XBrushes.Black, new XRect(38, y, page.Width.Point - 76, 20), XStringFormats.TopLeft);
                            rest = rest.Substring(count); y += bold ? 20 : 14;
                        }
                    }
                    y += 4;
                };
                try
                {
                    write("Monthly overview", true);
                    foreach (var row in tables[0].Rows) write(Text(row[0]) + ": " + Text(row[1]), false);
                    write("Performance by current reference details", true);
                    foreach (var row in tables.Last().Rows) write(string.Join(" | ", row.Select((v, i) => tables.Last().Headers[i] + ": " + Text(v))), false);
                    var details = tables.Skip(2).Take(5).Select(t => new { Table = t, Rows = t.Rows.ToLookup(r => Text(r[0])) }).ToList();
                    foreach (var row in tables[1].Rows)
                    {
                        context = "Ticket " + Text(row[0]) + " - continued on subsequent pages";
                        newPage();
                        write("Ticket " + Text(row[0]), true);
                        for (var c = 1; c < row.Length; c++) write(tables[1].Headers[c] + ": " + Text(row[c]), false);
                        foreach (var detail in details)
                        {
                            var rows = detail.Rows[Text(row[0])].ToList();
                            if (rows.Count == 0) continue;
                            write(detail.Table.Name, true);
                            foreach (var entry in rows)
                            {
                                for (var c = 1; c < entry.Length; c++) write(detail.Table.Headers[c] + ": " + Text(entry[c]), false);
                                write(" ", false);
                            }
                        }
                    }
                }
                finally { graphics?.Dispose(); }
                document.Save(path);
            }
        }
    }
}
