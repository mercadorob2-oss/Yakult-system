using System;
using System.IO;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Simple file-based logger for application errors and events
    /// </summary>
    public static class Logger
    {
        private static readonly string LogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "YakultInventoryApp",
            "Logs"
        );

        private static readonly object _lockObject = new object();

        static Logger()
        {
            // Ensure log directory exists
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        /// <summary>
        /// Logs an error with exception details
        /// </summary>
        public static void LogError(string message, Exception ex = null)
        {
            Log("ERROR", message, ex);
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            Log("INFO", message, null);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            Log("WARNING", message, null);
        }

        private static void Log(string level, string message, Exception ex)
        {
            try
            {
                lock (_lockObject)
                {
                    string logFile = Path.Combine(LogDirectory, $"log_{DateTime.UtcNow:yyyy-MM-dd}.txt");
                    string logEntry = FormatLogEntry(level, message, ex);

                    File.AppendAllText(logFile, logEntry + Environment.NewLine);
                }
            }
            catch
            {
                // Silently fail if logging fails (don't break the app)
            }
        }

        private static string FormatLogEntry(string level, string message, Exception ex)
        {
            string timestampUtc = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
            string entry = $"[{timestampUtc}] [{level}] {message}";

            if (ex != null)
            {
                entry += $"\nException: {ex.GetType().Name}: {ex.Message}";
                entry += $"\nStackTrace: {ex.StackTrace}";

                var inner = ex.InnerException;
                int depth = 1;
                while (inner != null)
                {
                    entry += $"\nInner Exception [{depth}]: {inner.GetType().Name}: {inner.Message}";
                    inner = inner.InnerException;
                    depth++;
                }
            }

            return entry;
        }

        /// <summary>
        /// Gets the path to today's log file
        /// </summary>
        public static string GetTodayLogPath()
        {
            return Path.Combine(LogDirectory, $"log_{DateTime.UtcNow:yyyy-MM-dd}.txt");
        }
    }
}
