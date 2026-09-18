using System;

namespace Yakult.Inventory.App.Core
{
    /// <summary>
    /// Centralized runtime connection provider for database access.
    ///
    /// DESIGN DECISION: Static provider chosen over constructor injection because:
    /// 1. WinForms pages instantiate repositories directly in constructors
    /// 2. No DI container is used in this project
    /// 3. Connection string is set once after login and remains constant
    /// 4. Minimizes changes across 26+ repositories and 100+ pages
    ///
    /// USAGE:
    /// - Connection string is loaded from user settings at app startup
    /// - Call EnsureConfigured() before database operations to get user-friendly errors
    /// - Never throws in property getters to prevent constructor crashes
    /// </summary>
    public static class DatabaseConfig
    {
        // For non-WinForms hosts (e.g. Yakult.ITCM.Scheduler console) — highest priority.
        private static string _bootstrapConnectionString;

        // Loaded from appsettings.json / appsettings.Development.json at startup.
        // Acts as the default for the WinForms app; overridden by user-saved settings.
        private static string _jsonDefaultConnectionString;

        // The SQL Server moved from port 50300 to 50301. A machine that previously used the
        // in-app DB switcher (Ctrl+Shift+D) has the old host baked into its per-user
        // user.config, which takes priority over the updated appsettings default — so correct
        // it in place the first time it's read (see ConnectionString getter).
        private const string OldSqlHost = "192.168.100.186,50300";
        private const string NewSqlHost = "192.168.100.186,50301";

        /// <summary>
        /// Overrides the connection string for non-WinForms hosts such as the
        /// ITCM background-job console application. Call this once at startup
        /// before any repository is instantiated.
        /// </summary>
        public static void Bootstrap(string connectionString)
        {
            _bootstrapConnectionString = connectionString;
        }

        /// <summary>
        /// Sets the default connection string loaded from appsettings JSON files.
        /// Used by the WinForms app at startup. User-saved settings (via Ctrl+Shift+D)
        /// will override this value.
        /// </summary>
        public static void SetJsonDefault(string connectionString)
        {
            _jsonDefaultConnectionString = connectionString;
        }

        /// <summary>
        /// Gets the current connection string. Priority order:
        ///   1. Bootstrap (non-WinForms console hosts only)
        ///   2. User-saved settings (set via the DB switcher, Ctrl+Shift+D)
        ///   3. JSON config default (appsettings.json / appsettings.Development.json)
        /// Returns empty string if nothing is configured (never throws).
        /// </summary>
        public static string ConnectionString
        {
            get
            {
                // Bootstrap overrides everything — for the scheduler console, not WinForms
                if (!string.IsNullOrWhiteSpace(_bootstrapConnectionString))
                    return _bootstrapConnectionString;

                // User-saved override wins over JSON default (developer explicitly switched)
                try
                {
                    var saved = Properties.Settings.Default.DbConnectionString;
                    if (!string.IsNullOrWhiteSpace(saved))
                    {
                        // Self-heal a stale saved override that still points at the old SQL port.
                        if (saved.IndexOf(OldSqlHost, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            saved = saved.Replace(OldSqlHost, NewSqlHost);
                            try
                            {
                                Properties.Settings.Default.DbConnectionString = saved;
                                Properties.Settings.Default.Save();
                            }
                            catch
                            {
                                // Persisting can fail on a locked/corrupt config — the corrected
                                // value is still returned for this session either way.
                            }
                        }
                        return saved;
                    }
                }
                catch
                {
                    // Settings access can fail on first run or corrupted config
                }

                // Fall back to JSON config default
                return _jsonDefaultConnectionString ?? string.Empty;
            }
        }

        /// <summary>
        /// Returns true if the connection string is configured and non-empty.
        /// Use this to check before instantiating repositories or executing queries.
        /// </summary>
        public static bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);

        /// <summary>
        /// Validates that the connection is configured. Call this at the start of
        /// repository methods that require database access.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when connection string is not configured.</exception>
        public static void EnsureConfigured()
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException(
                    "Database connection is not configured. Please restart the application and complete the database setup.");
            }
        }

        /// <summary>
        /// Gets the connection string if configured, otherwise returns null.
        /// Useful for repositories that want to defer validation.
        /// </summary>
        public static string GetConnectionStringOrNull()
        {
            return IsConfigured ? ConnectionString : null;
        }
    }
}

