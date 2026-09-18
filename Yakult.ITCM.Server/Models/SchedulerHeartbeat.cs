namespace Yakult.ITCM.Server.Models;

public sealed class SchedulerHeartbeat
{
    public long HeartbeatId { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime? FinishedAtUtc { get; set; }
    public bool? Succeeded { get; set; }
    public bool LockAcquired { get; set; }
    public int ReminderCandidates { get; set; }
    public int RemindersSent { get; set; }
    public int EscalationCandidates { get; set; }
    public int EscalationsApplied { get; set; }
    public string? ErrorMessage { get; set; }
    public string MachineName { get; set; } = string.Empty;
    public string Version { get; set; } = "1.0.0";
}

public sealed class SchedulerHeartbeatSummary
{
    public DateTime? LastStartedUtc { get; set; }
    public DateTime? LastFinishedUtc { get; set; }
    public DateTime? LastSuccessUtc { get; set; }
    public int TotalRuns { get; set; }
    public int SuccessfulRuns { get; set; }
    public int FailedRuns { get; set; }
    public string? LastError { get; set; }
    public string? MachineName { get; set; }
    public string? Version { get; set; }
}

public sealed class SchedulerHeartbeatPage
{
    public List<SchedulerHeartbeat> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalCount / PageSize) : 0;
}
