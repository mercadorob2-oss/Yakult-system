namespace Yakult.ITCM.Server.Services;

public sealed class ItcmSchedulerState
{
    private readonly object _sync = new();

    public bool IsPaused { get; private set; }
    public bool IsRunning { get; private set; }
    public DateTimeOffset? LastStartedAt { get; private set; }
    public DateTimeOffset? LastCompletedAt { get; private set; }
    public DateTimeOffset? NextRunAt { get; private set; }
    public bool? LastRunSucceeded { get; private set; }
    public string? LastError { get; private set; }
    public int TotalRuns { get; private set; }
    public int SuccessfulRuns { get; private set; }
    public int FailedRuns { get; private set; }

    public int? ReminderCandidates { get; private set; }
    public int? RemindersSent { get; private set; }
    public int? EscalationCandidates { get; private set; }
    public int? EscalationsApplied { get; private set; }
    public bool? LockAcquired { get; private set; }

    public void Pause()
    {
        lock (_sync) { IsPaused = true; }
    }

    public void Resume()
    {
        lock (_sync) { IsPaused = false; }
    }

    public void MarkStarting(DateTimeOffset startedAt)
    {
        lock (_sync)
        {
            IsRunning = true;
            LastStartedAt = startedAt;
            LastError = null;
        }
    }

    public void MarkCompleted(DateTimeOffset completedAt, int? reminderCandidates = null, int? remindersSent = null,
        int? escalationCandidates = null, int? escalationsApplied = null, bool? lockAcquired = null)
    {
        lock (_sync)
        {
            IsRunning = false;
            LastCompletedAt = completedAt;
            LastRunSucceeded = true;
            TotalRuns++;
            SuccessfulRuns++;
            ReminderCandidates = reminderCandidates;
            RemindersSent = remindersSent;
            EscalationCandidates = escalationCandidates;
            EscalationsApplied = escalationsApplied;
            LockAcquired = lockAcquired;
        }
    }

    public void MarkFailed(DateTimeOffset completedAt, Exception exception)
    {
        lock (_sync)
        {
            IsRunning = false;
            LastCompletedAt = completedAt;
            LastRunSucceeded = false;
            LastError = exception.Message;
            TotalRuns++;
            FailedRuns++;
        }
    }

    // A run that did no work: lock contention, cancellation, or an explicit
    // skip. Deliberately neither success nor failure (LastRunSucceeded=null)
    // so contention/cancel is visible instead of masquerading as success.
    public void MarkSkipped(DateTimeOffset completedAt, string reason, bool? lockAcquired = null)
    {
        lock (_sync)
        {
            IsRunning = false;
            LastCompletedAt = completedAt;
            LastRunSucceeded = null;
            LastError = reason;
            TotalRuns++;
            LockAcquired = lockAcquired;
        }
    }

    public void SetNextRun(DateTimeOffset? nextRunAt)
    {
        lock (_sync) { NextRunAt = nextRunAt; }
    }

    public ItcmSchedulerStatusSnapshot GetSnapshot()
    {
        lock (_sync)
        {
            return new ItcmSchedulerStatusSnapshot(
                IsPaused,
                IsRunning,
                LastStartedAt,
                LastCompletedAt,
                NextRunAt,
                LastRunSucceeded,
                LastError,
                TotalRuns,
                SuccessfulRuns,
                FailedRuns,
                new LastRunDetails(
                    ReminderCandidates,
                    RemindersSent,
                    EscalationCandidates,
                    EscalationsApplied,
                    LockAcquired));
        }
    }
}

public sealed record LastRunDetails(
    int? ReminderCandidates,
    int? RemindersSent,
    int? EscalationCandidates,
    int? EscalationsApplied,
    bool? LockAcquired);

public sealed record ItcmSchedulerStatusSnapshot(
    bool IsPaused,
    bool IsRunning,
    DateTimeOffset? LastStartedAt,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? NextRunAt,
    bool? LastRunSucceeded,
    string? LastError,
    int TotalRuns,
    int SuccessfulRuns,
    int FailedRuns,
    LastRunDetails? LastRunDetails);