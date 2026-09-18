using Microsoft.Extensions.Options;
using Yakult.ITCM.Server.Models;
using Yakult.ITCM.Server.Options;

namespace Yakult.ITCM.Server.Services;

public sealed class ItcmSchedulerHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ItcmSchedulerState _state;
    private readonly IOptionsMonitor<ItcmSchedulerOptions> _options;
    private readonly ILogger<ItcmSchedulerHostedService> _logger;
    private readonly SemaphoreSlim _runLock = new(1, 1);

    public ItcmSchedulerHostedService(
        IServiceScopeFactory scopeFactory,
        ItcmSchedulerState state,
        IOptionsMonitor<ItcmSchedulerOptions> options,
        ILogger<ItcmSchedulerHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _state = state;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> RunNowAsync(CancellationToken cancellationToken)
    {
        // Intentional override: Run Now executes even when the scheduler is
        // paused or disabled. The dashboard snapshot distinguishes the outcome.
        bool entered;
        try
        {
            entered = await _runLock.WaitAsync(0, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
        if (!entered)
            return false;

        try
        {
            await RunOnceAsync(cancellationToken);
            return true;
        }
        finally
        {
            _runLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seededPaused = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            var options = GetSafeOptions();
            if (!seededPaused)
            {
                seededPaused = true;
                // Pause survives restarts: seed the runtime flag from config.
                if (options.Paused)
                    _state.Pause();
            }
            var interval = TimeSpan.FromMinutes(options.IntervalMinutes);
            var nextRunAt = DateTimeOffset.Now.Add(interval);
            var runnable = options.Enabled && !_state.GetSnapshot().IsPaused;
            _state.SetNextRun(runnable ? nextRunAt : null);

            // Check the reloadable options once per second while waiting. This lets a
            // newly saved interval replace an existing long delay promptly.
            while (!stoppingToken.IsCancellationRequested && DateTimeOffset.Now < nextRunAt)
            {
                try
                {
                    var remaining = nextRunAt - DateTimeOffset.Now;
                    await Task.Delay(remaining < TimeSpan.FromSeconds(1) ? remaining : TimeSpan.FromSeconds(1), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                var reloadedOptions = GetSafeOptions();
                if (reloadedOptions.IntervalMinutes != options.IntervalMinutes
                    || reloadedOptions.Enabled != options.Enabled
                    || reloadedOptions.Paused != options.Paused)
                {
                    options = reloadedOptions;
                    interval = TimeSpan.FromMinutes(options.IntervalMinutes);
                    nextRunAt = DateTimeOffset.Now.Add(interval);
                    runnable = options.Enabled && !_state.GetSnapshot().IsPaused;
                    _state.SetNextRun(runnable ? nextRunAt : null);
                    _logger.LogInformation(
                        "ITCM scheduler settings reloaded: enabled={Enabled}, interval={IntervalMinutes} minute(s).",
                        options.Enabled,
                        options.IntervalMinutes);
                }
                else
                {
                    options = reloadedOptions;
                }
            }

            if (stoppingToken.IsCancellationRequested)
                break;

            if (!options.Enabled || _state.GetSnapshot().IsPaused)
                continue;

            if (!await _runLock.WaitAsync(0, stoppingToken))
            {
                _logger.LogWarning("Skipped ITCM scheduled run because another run is already active.");
                continue;
            }

            try
            {
                await RunOnceAsync(stoppingToken);
            }
            finally
            {
                _runLock.Release();
            }
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        var options = GetSafeOptions();
        _state.MarkStarting(DateTimeOffset.Now);

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var job = scope.ServiceProvider.GetRequiredService<IItcmBackgroundJob>();
            var result = await job.RunAsync(options.MaxTicketsPerRun, cancellationToken);

            if (!result.LockAcquired)
            {
                _logger.LogWarning("ITCM run skipped: another instance holds the global lock.");
                _state.MarkSkipped(DateTimeOffset.Now, "skipped-lock-held", lockAcquired: false);
                return;
            }

            _state.MarkCompleted(
                DateTimeOffset.Now,
                reminderCandidates: result.ReminderCandidates,
                remindersSent: result.RemindersSent,
                escalationCandidates: result.EscalationCandidates,
                escalationsApplied: result.EscalationsApplied,
                lockAcquired: result.LockAcquired);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("ITCM scheduled run was cancelled.");
            _state.MarkSkipped(DateTimeOffset.Now, "cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ITCM scheduled run failed.");
            _state.MarkFailed(DateTimeOffset.Now, ex);
        }
    }

    private ItcmSchedulerOptions GetSafeOptions()
    {
        var configured = _options.CurrentValue;
        return new ItcmSchedulerOptions
        {
            Enabled = configured.Enabled,
            Paused = configured.Paused,
            IntervalMinutes = Math.Clamp(configured.IntervalMinutes <= 0 ? 15 : configured.IntervalMinutes, 1, 1440),
            MaxTicketsPerRun = Math.Clamp(configured.MaxTicketsPerRun <= 0 ? 20 : configured.MaxTicketsPerRun, 1, 500)
        };
    }
}