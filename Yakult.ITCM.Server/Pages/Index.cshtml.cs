using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Models;
using Yakult.ITCM.Server.Options;
using Yakult.ITCM.Server.Services;

namespace Yakult.ITCM.Server.Pages;

public class IndexModel : PageModel
{
    private readonly IItcmRepository _repo;
    private readonly ItcmSchedulerState _state;
    private readonly IOptionsMonitor<ItcmSchedulerOptions> _schedulerOptions;

    public string Title { get; private set; } = "Yakult ITCM Server";
    public string Subtitle { get; private set; } = "Central scheduler and diagnostics for IT Call Monitoring";

    public bool SchedulerEnabled { get; private set; }
    public int IntervalMinutes { get; private set; }
    public int MaxTicketsPerRun { get; private set; }
    public string HeartbeatHistoryUrl { get; } = "/api/itcm/heartbeat/history";

    public IndexModel(IItcmRepository repo, ItcmSchedulerState state, IOptionsMonitor<ItcmSchedulerOptions> schedulerOptions)
    {
        _repo = repo;
        _state = state;
        _schedulerOptions = schedulerOptions;
    }

    public void OnGet()
    {
        SchedulerEnabled = _schedulerOptions.CurrentValue.Enabled;
        IntervalMinutes = _schedulerOptions.CurrentValue.IntervalMinutes;
        MaxTicketsPerRun = _schedulerOptions.CurrentValue.MaxTicketsPerRun;
    }
}