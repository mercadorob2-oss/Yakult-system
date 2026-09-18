namespace Yakult.ITCM.Server.Options;

public sealed class ItcmSchedulerOptions
{
    public const string SectionName = "ItcmScheduler";

    public bool Enabled { get; set; } = true;

    // Durable pause: written by the pause/resume endpoints into
    // appsettings.local.json and seeded into runtime state at startup,
    // so a deliberate pause survives restarts. Run Now still overrides.
    public bool Paused { get; set; } = false;

    public int IntervalMinutes { get; set; } = 15;

    // Budget PER PHASE (reminders, then escalations): one run may process up
    // to 2x this many tickets in total. Oldest-first ordering within each
    // phase keeps sustained overloads from starving the oldest tickets.
    public int MaxTicketsPerRun { get; set; } = 20;
}
