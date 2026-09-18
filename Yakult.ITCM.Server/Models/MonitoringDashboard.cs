namespace Yakult.ITCM.Server.Models;

public sealed class MonitoringDashboard
{
    public int UnassignedPortalTicketCount { get; set; }
    public int RecentAutoEscalationCount { get; set; }
    public int ReminderEmailsSentToday { get; set; }
    public List<PortalTriageTicket> UnassignedPortalTickets { get; set; } = [];
    public List<TicketMovement> RecentTicketMovements { get; set; } = [];
    public List<SchedulerActivity> RecentSchedulerActivity { get; set; } = [];
    public List<NotificationActivity> RecentNotifications { get; set; } = [];
}

public sealed class PortalTriageTicket
{
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public string CallerName { get; set; } = string.Empty;
    public string Issue { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}

public sealed class TicketMovement
{
    public long HistoryId { get; set; }
    public int TicketId { get; set; }
    public string TicketCode { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string FieldName { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
    public int? ChangedByUserId { get; set; }
    public bool IsAutoEscalation { get; set; }
}

public sealed class SchedulerActivity
{
    public long HeartbeatId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool? Succeeded { get; set; }
    public int RemindersSent { get; set; }
    public int EscalationsApplied { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class NotificationActivity
{
    public long EmailLogId { get; set; }
    public int? TicketId { get; set; }
    public string? TicketCode { get; set; }
    public DateTime DateSent { get; set; }
    public string EmailType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Recipient { get; set; }
    public string? ErrorMessage { get; set; }
}
