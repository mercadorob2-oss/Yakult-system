using System;
using System.Collections.Generic;

namespace Yakult.Inventory.App.Models.CallMonitoring
{
    public sealed class CallMonthlyReport
    {
        public DateTime MonthLocal { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime CutoffUtc { get; set; }
        public DateTime GeneratedUtc { get; set; }
        public List<CallMonthlyTicket> Tickets { get; set; } = new List<CallMonthlyTicket>();
        public List<CallTicketHistoryItem> History { get; set; } = new List<CallTicketHistoryItem>();
        public List<CallTicketNoteItem> Notes { get; set; } = new List<CallTicketNoteItem>();
        public List<CallMonthlyVisit> Visits { get; set; } = new List<CallMonthlyVisit>();
        public List<CallMonthlyItem> Items { get; set; } = new List<CallMonthlyItem>();
        public List<string> Warnings { get; set; } = new List<string>();
        public static bool IsClosed(string status) => string.Equals(status, "Solved", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
        public static readonly TimeZoneInfo ReportZone = TimeZoneInfo.FindSystemTimeZoneById("Singapore Standard Time");
        public static DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), ReportZone);
    }

    public sealed class CallMonthlyTicket
    {
        public int TicketId { get; set; }
        public string TicketCode { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Company { get; set; }
        public string Department { get; set; }
        public string Branch { get; set; }
        public string CallerName { get; set; }
        public string IssueType { get; set; }
        public string Priority { get; set; }
        public string ResponsiblePerson { get; set; }
        public string Issue { get; set; }
        public string ProvidedSolution { get; set; }
        public string OpeningStatus { get; set; }
        public string ClosingStatus { get; set; }
    }

    public sealed class CallMonthlyVisit
    {
        public int FieldVisitId { get; set; }
        public int TicketId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? ScheduledAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Status { get; set; }
        public string TechnicianName { get; set; }
        public string Notes { get; set; }
        public bool HasSignature { get; set; }
        public int AttachmentCount { get; set; }
    }

    public sealed class CallMonthlyItem
    {
        public int ItemId { get; set; }
        public string Name { get; set; }
        public string ModelNumber { get; set; }
        public string SerialNumber { get; set; }
    }
}
