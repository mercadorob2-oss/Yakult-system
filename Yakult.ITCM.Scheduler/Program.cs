using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Repositories;
using Yakult.Inventory.App.Services;

namespace Yakult.ITCM.Scheduler
{
    internal static class Program
    {
        private const string EventSourceName = "YakultITCM";
        private const int MaxTicketsPerRun = 20;

        /// <summary>
        /// Entry point for the IT Call Monitoring background scheduler.
        /// Returns exit code 0 on success, 1 on failure.
        /// Designed to be invoked by Windows Task Scheduler every 15 minutes.
        /// </summary>
        static int Main(string[] args)
        {
            return Task.Run(() => RunAsync()).GetAwaiter().GetResult();
        }

        private static async Task<int> RunAsync()
        {
            var runId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            var startUtc = DateTime.UtcNow;

            try
            {
                // ── 1. Resolve and validate connection string ──────────────────────
                var cs = ResolveConnectionString();

                if (string.IsNullOrWhiteSpace(cs))
                {
                    throw new InvalidOperationException(
                        "Connection string 'Yakult_Inventory_System' is missing. " +
                        "Set YAKULT_SCHEDULER_CONNECTION_STRING, create App.local.config, or update App.config.");
                }

                // Override DatabaseConfig so all repositories use this connection string
                // instead of looking for WinForms user settings.
                DatabaseConfig.Bootstrap(cs);

                Log(runId, EventLogEntryType.Information,
                    "ITCM background job started.");

                // ── 2. Execute reminder + escalation processing ────────────────────
                var repo = new CallMonitoringRepository();
                var svc = new CallEmailNotificationService(repo);

                await svc.ProcessRemindersAndAutoEscalationsWithGlobalLockAsync(
                    maxTickets: MaxTicketsPerRun,
                    triggeredByUserId: null);

                // ── 3. Report result ───────────────────────────────────────────────
                var snapshot = CallEmailNotificationService.GetBackgroundJobStatusSnapshot();
                var elapsed = (DateTime.UtcNow - startUtc).TotalSeconds;

                var summary = $"Completed in {elapsed:F1}s. " +
                              $"Reminders: {snapshot.RemindersSent}/{snapshot.ReminderCandidates} sent. " +
                              $"Escalations: {snapshot.EscalationsApplied}/{snapshot.EscalationCandidates} applied." +
                              (snapshot.LastLockAcquired == false
                                   ? " (Lock not acquired — another instance was running; skipped.)"
                                   : string.Empty);

                Log(runId, EventLogEntryType.Information, summary);
                return 0;
            }
            catch (Exception ex)
            {
                var elapsed = (DateTime.UtcNow - startUtc).TotalSeconds;
                Log(runId, EventLogEntryType.Error,
                    $"FAILED after {elapsed:F1}s: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
                return 1;
            }
        }

        // ── Logging helpers ────────────────────────────────────────────────────────

        private static void Log(string runId, EventLogEntryType type, string message)
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var level = type == EventLogEntryType.Error ? "ERROR" : "INFO";
            var line = $"[{ts}] [{runId}] {level}: {message}";

            if (type == EventLogEntryType.Error)
                Console.Error.WriteLine(line);
            else
                Console.WriteLine(line);

            WriteToEventLog(type, message);
        }

        private static void WriteToEventLog(EventLogEntryType type, string message)
        {
            try
            {
                if (!EventLog.SourceExists(EventSourceName))
                    EventLog.CreateEventSource(EventSourceName, "Application");

                EventLog.WriteEntry(EventSourceName, message, type);
            }
            catch
            {
                // Not running as Administrator, or event source already exists
                // under a different log.  Console output is still available.
            }
        }

        private static string ResolveConnectionString()
        {
            var environmentConnectionString = Environment.GetEnvironmentVariable("YAKULT_SCHEDULER_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(environmentConnectionString))
                return environmentConnectionString;

            try
            {
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App.local.config");
                if (File.Exists(configPath))
                {
                    var map = new ExeConfigurationFileMap
                    {
                        ExeConfigFilename = configPath
                    };

                    var localConfig = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);
                    var localConnectionString = localConfig.ConnectionStrings.ConnectionStrings["Yakult_Inventory_System"]?.ConnectionString;
                    if (!string.IsNullOrWhiteSpace(localConnectionString))
                        return localConnectionString;
                }
            }
            catch
            {
            }

            return ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"]?.ConnectionString;
        }
    }
}
