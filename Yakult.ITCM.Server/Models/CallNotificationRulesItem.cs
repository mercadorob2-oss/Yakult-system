namespace Yakult.ITCM.Server.Models;

public sealed class CallNotificationRulesItem
{
    public int RulesId { get; set; }
    public bool NotifyOnNewTicket { get; set; }
    public bool NotifyOnStatusChange { get; set; }
    public bool NotifyOnEscalation { get; set; }
    public bool NotifyOnReminder { get; set; }
    public string? GroupEmail { get; set; }
    public string? EscalationEmail { get; set; }
    public int ReminderDays { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
