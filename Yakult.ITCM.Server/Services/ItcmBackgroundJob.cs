using Microsoft.Data.SqlClient;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Models;

namespace Yakult.ITCM.Server.Services;

public interface IItcmBackgroundJob
{
    Task<BackgroundJobResult> RunAsync(int maxTickets, CancellationToken cancellationToken);
}

public sealed class ItcmBackgroundJob : IItcmBackgroundJob
{
    private const string GlobalLockName = "Yakult.Inventory.App|CallMonitoring|BackgroundJobs";

    private readonly IItcmRepository _repo;
    private readonly EmailService _emailService;
    private readonly ILogger<ItcmBackgroundJob> _logger;
    private readonly string _machineName;
    private readonly string _version;

    public ItcmBackgroundJob(
        IItcmRepository repo,
        EmailService emailService,
        ILogger<ItcmBackgroundJob> logger)
    {
        _repo = repo;
        _emailService = emailService;
        _logger = logger;
        _machineName = Environment.MachineName;
        _version = "1.0.0";
    }

    public async Task<BackgroundJobResult> RunAsync(int maxTickets, CancellationToken cancellationToken)
    {
        var startUtc = DateTime.UtcNow;
        var result = new BackgroundJobResult();
        var heartbeat = new SchedulerHeartbeat
        {
            StartedAtUtc = startUtc,
            MachineName = _machineName,
            Version = _version,
            LockAcquired = false
        };

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // ── 1. Open lock connection ────────────────────────────────────────────
            using var lockConnection = new SqlConnection(_repo.ConnectionString);
            await lockConnection.OpenAsync(cancellationToken);

            result.LockAcquired = await _repo.AcquireGlobalLockAsync(lockConnection, timeoutMs: 0);
            heartbeat.LockAcquired = result.LockAcquired;

            if (!result.LockAcquired)
            {
                _logger.LogInformation("ITCM background job skipped: another instance holds the global lock.");
                heartbeat.FinishedAtUtc = DateTime.UtcNow;
                heartbeat.Succeeded = null;
                heartbeat.ErrorMessage = "skipped-lock-held";
                await _repo.WriteHeartbeatAsync(heartbeat);
                result.LastError = "skipped-lock-held";
                result.Elapsed = DateTime.UtcNow - startUtc;
                return result;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // ── 2. Process reminders ──────────────────────────────────────────
                _logger.LogInformation("ITCM background job: processing reminders (maxTickets={MaxTickets})...", maxTickets);
                (result.ReminderCandidates, result.RemindersSent) = await ProcessRemindersAsync(maxTickets, cancellationToken);
                result.ReminderCandidates = Math.Max(0, result.ReminderCandidates);
                result.RemindersSent = Math.Max(0, result.RemindersSent);

                cancellationToken.ThrowIfCancellationRequested();

                // ── 3. Process auto-escalations ───────────────────────────────────
                _logger.LogInformation("ITCM background job: processing auto-escalations (maxTickets={MaxTickets})...", maxTickets);
                (result.EscalationCandidates, result.EscalationsApplied) = await ProcessAutoEscalationsAsync(maxTickets, cancellationToken);
                result.EscalationCandidates = Math.Max(0, result.EscalationCandidates);
                result.EscalationsApplied = Math.Max(0, result.EscalationsApplied);

                heartbeat.Succeeded = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Let cancellation propagate so state/heartbeat report
                // Cancelled instead of Failed.
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ITCM background job processing failed.");
                result.LastError = ex.Message;
                heartbeat.ErrorMessage = ex.Message;
                heartbeat.Succeeded = false;
            }
            finally
            {
                await _repo.ReleaseGlobalLockAsync(lockConnection);
                heartbeat.FinishedAtUtc = DateTime.UtcNow;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ITCM background job was cancelled.");
            result.LastError = "Cancelled";
            heartbeat.ErrorMessage = "Cancelled";
            heartbeat.Succeeded = null;
            heartbeat.FinishedAtUtc = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ITCM background job lock acquisition failed.");
            result.LastError = ex.Message;
            heartbeat.ErrorMessage = ex.Message;
            heartbeat.Succeeded = false;
            heartbeat.FinishedAtUtc = DateTime.UtcNow;
        }

        // ── 4. Write heartbeat ──────────────────────────────────────────────────
        try
        {
            heartbeat.ReminderCandidates = result.ReminderCandidates;
            heartbeat.RemindersSent = result.RemindersSent;
            heartbeat.EscalationCandidates = result.EscalationCandidates;
            heartbeat.EscalationsApplied = result.EscalationsApplied;
            await _repo.WriteHeartbeatAsync(heartbeat);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write scheduler heartbeat.");
        }

        result.Elapsed = DateTime.UtcNow - startUtc;
        _logger.LogInformation(
            "ITCM background job completed in {Elapsed:F1}s. " +
            "Reminders: {Sent}/{Candidates} sent. " +
            "Escalations: {Applied}/{EscCandidates} applied. " +
            "Lock: {LockAcquired}",
            result.Elapsed.TotalSeconds,
            result.RemindersSent, result.ReminderCandidates,
            result.EscalationsApplied, result.EscalationCandidates,
            result.LockAcquired);

        return result;
    }

    private async Task<(int candidates, int sent)> ProcessRemindersAsync(int maxTickets, CancellationToken ct)
    {
        var rules = await _repo.GetNotificationRulesAsync();
        if (rules is null || !rules.NotifyOnReminder)
            return (0, 0);

        var reminderDays = Math.Clamp(rules.ReminderDays, 1, 60);
        var ticketIds = await _repo.GetTicketIdsNeedingReminderAsync(reminderDays, maxTickets);

        if (ticketIds.Count == 0)
            return (0, 0);

        var sentCount = 0;
        foreach (var ticketId in ticketIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var result = await _emailService.NotifyReminderAsync(ticketId, null);
                if (result.SentSuccessfully)
                {
                    await _repo.MarkReminderSentAsync(ticketId);
                    sentCount++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Reminder processing failed for TicketId={TicketId}", ticketId);
            }
        }

        return (ticketIds.Count, sentCount);
    }

    private async Task<(int candidates, int applied)> ProcessAutoEscalationsAsync(int maxTickets, CancellationToken ct)
    {
        maxTickets = Math.Clamp(maxTickets, 1, 500);

        var ticketIds = await _repo.GetTicketIdsNeedingAutoEscalationAsync(maxTickets);
        if (ticketIds.Count == 0)
            return (0, 0);

        var autoAssigneeEmpId = await TryGetAutoEscalationAssigneeAsync();

        var appliedCount = 0;
        foreach (var ticketId in ticketIds)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ticket = await _repo.GetTicketNotificationDataAsync(ticketId);
                if (ticket is null) continue;

                var oldStatus = ticket.Status ?? string.Empty;
                if (await ShouldSkipTicketAsync(ticketId, oldStatus)) continue;

                // Keep Portal submissions in the desktop's Pending/unassigned
                // triage queue until an IT employee explicitly owns them.
                if (string.Equals(ticket.TicketSource, "Portal", StringComparison.OrdinalIgnoreCase)
                    && !ticket.AssignedToEmpId.HasValue)
                {
                    _logger.LogInformation(
                        "Skipped auto-escalation for unassigned Portal ticket {TicketId}.", ticketId);
                    continue;
                }

                var note = BuildAutoEscalationNote(ticket);
                await _repo.SetTicketStatusAsync(ticketId, "Escalated", null, note);

                var employeeRecipients = new List<int>();
                if (ticket.AssignedToEmpId.HasValue && ticket.AssignedToEmpId.Value > 0)
                    employeeRecipients.Add(ticket.AssignedToEmpId.Value);

                if (autoAssigneeEmpId.HasValue && autoAssigneeEmpId.Value > 0)
                {
                    try { await _repo.AssignTicketEmployeeAsync(ticketId, autoAssigneeEmpId.Value, null); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Auto-assignment failed for TicketId={TicketId}", ticketId); }
                    employeeRecipients.Add(autoAssigneeEmpId.Value);
                }

                appliedCount++;
                try
                {
                    await _emailService.NotifyStatusChangeAsync(ticketId, oldStatus, "Escalated", note, null, employeeRecipients);
                }
                catch (Exception ex)
                {
                    // The escalation itself is applied and counted; a mail
                    // failure is traced in CallEmailLog, not rethrown.
                    _logger.LogWarning(ex, "Escalation email failed for TicketId={TicketId}", ticketId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Auto-escalation failed for TicketId={TicketId}", ticketId);
            }
        }

        return (ticketIds.Count, appliedCount);
    }

    private async Task<int?> TryGetAutoEscalationAssigneeAsync()
    {
        try
        {
            if (await _repo.EmployeeAssignmentEnabledAsync())
                return await _repo.GetAutoEscalationAssigneeEmpIdAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-escalation assignee lookup failed.");
        }
        return null;
    }

    private async Task<bool> ShouldSkipTicketAsync(int ticketId, string status)
    {
        status = (status ?? string.Empty).Trim();
        if (status.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Solved", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Closed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase))
            return false;

        // Replacement-backed temporaries stay parked (awaiting return);
        // Service-Only temporaries remain watchable. Fail parked when the
        // allocation cannot be determined.
        try
        {
            return await _repo.HasReplacementAllocationAsync(ticketId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Replacement allocation check failed for TicketId={TicketId}; parking.", ticketId);
            return true;
        }
    }

    private static bool ShouldSkipAutoEscalation(string status)
    {
        status = (status ?? string.Empty).Trim();
        return status.Equals("Escalated", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Solved", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Closed", StringComparison.OrdinalIgnoreCase)
            || status.Equals("Forwarded to Repair", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildAutoEscalationNote(CallTicketNotificationData ticket)
    {
        var lastContactUtc = ticket.LastContactAt.HasValue
            ? DateTime.SpecifyKind(ticket.LastContactAt.Value, DateTimeKind.Utc)
            : DateTime.SpecifyKind(ticket.UpdatedAt, DateTimeKind.Utc);

        var idleDays = Math.Max(0, (int)Math.Floor((DateTime.UtcNow - lastContactUtc).TotalDays));
        return idleDays > 0
            ? $"Auto-escalated due to no updates for {idleDays} day(s)."
            : "Auto-escalated due to inactivity.";
    }
}