namespace Yakult.ITCM.Server.Models;

public sealed class BackgroundJobResult
{
    public bool LockAcquired { get; set; }
    public int ReminderCandidates { get; set; }
    public int RemindersSent { get; set; }
    public int EscalationCandidates { get; set; }
    public int EscalationsApplied { get; set; }
    public string? LastError { get; set; }
    public TimeSpan Elapsed { get; set; }
}
