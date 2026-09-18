using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Models;
using Microsoft.Data.SqlClient;

namespace Inventory.RequestPortal.Services
{
    /// <summary>
    /// Provides connection strings with support for runtime database switching (DEV-ONLY).
    /// In Development mode, checks session for database preference and overrides the database name.
    /// In Production mode, always uses the default connection string.
    /// </summary>
    public interface IConnectionStringProvider
    {
        string GetConnectionString();
        string GetCurrentDatabaseName();
        bool IsUsingLocalDatabase();
    }

    public class ConnectionStringProvider : IConnectionStringProvider
    {
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ConnectionStringProvider> _logger;

        public ConnectionStringProvider(
            IConfiguration configuration,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment environment,
            ILogger<ConnectionStringProvider> logger)
        {
            _configuration = configuration;
            _httpContextAccessor = httpContextAccessor;
            _environment = environment;
            _logger = logger;
        }

        /// <summary>
        /// Gets the connection string, with database override from session if in Development mode.
        /// </summary>
        public string GetConnectionString()
        {
            var baseConnectionString = _configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not configured.");

            // Session-based database override is a Development-only convenience (DevToolsController
            // is itself restricted to Development, but a preference saved before that restriction
            // existed — or before an environment ever flips from Development to Production — must
            // not silently keep redirecting production traffic). This is the actual enforcement
            // point of the "In Production mode, always uses the default connection string" contract
            // documented on IConnectionStringProvider.GetConnectionString().
            if (!_environment.IsDevelopment())
                return baseConnectionString;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext?.Session != null)
            {
                var preference = httpContext.Session.GetObject<DatabasePreference>("DatabasePreference");
                if (preference != null)
                {
                    // Fully custom connection string (user-entered fields)
                    if (preference.UseCustomConnection && !string.IsNullOrWhiteSpace(preference.CustomConnectionString))
                    {
                        _logger.LogInformation("Using custom connection string (SessionId: {SessionId})", httpContext.Session.Id);
                        return preference.CustomConnectionString;
                    }

                    // Local Windows Auth connection
                    if (preference.UseLocalDatabase)
                    {
                        var localBase = _configuration.GetConnectionString("LocalConnection")
                            ?? "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;";

                        var localBuilder = new SqlConnectionStringBuilder(localBase);
                        if (!string.IsNullOrWhiteSpace(preference.LocalDatabaseName))
                            localBuilder.InitialCatalog = preference.LocalDatabaseName;

                        _logger.LogInformation("🏠 Using local database: {Database} (Windows Auth, SessionId: {SessionId})",
                            localBuilder.InitialCatalog, httpContext.Session.Id);
                        return localBuilder.ConnectionString;
                    }

                    // Remote database override
                    if (!string.IsNullOrWhiteSpace(preference.DatabaseName))
                    {
                        var builder = new SqlConnectionStringBuilder(baseConnectionString)
                        {
                            InitialCatalog = preference.DatabaseName
                        };

                        _logger.LogInformation("🔄 Using remote database override: {Database} (SessionId: {SessionId})",
                            preference.DatabaseName, httpContext.Session.Id);
                        return builder.ConnectionString;
                    }
                }
                else
                {
                    var defaultDb = new SqlConnectionStringBuilder(baseConnectionString).InitialCatalog;
                    _logger.LogInformation("📂 No session preference — using appsettings database: {Database} (SessionId: {SessionId})",
                        defaultDb, httpContext.Session.Id);
                }
            }
            else
            {
                var defaultDb = new SqlConnectionStringBuilder(baseConnectionString).InitialCatalog;
                _logger.LogWarning("⚠️ No HttpContext or Session available, using default database: {Database}", defaultDb);
            }

            // No override, return base connection string
            return baseConnectionString;
        }

        /// <summary>
        /// Gets the current database name (either from session override or default).
        /// </summary>
        public string GetCurrentDatabaseName()
        {
            var connectionString = GetConnectionString();
            var builder = new SqlConnectionStringBuilder(connectionString);
            return builder.InitialCatalog;
        }

        public bool IsUsingLocalDatabase()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var preference = httpContext?.Session?.GetObject<DatabasePreference>("DatabasePreference");
            return preference?.UseLocalDatabase == true;
        }
    }
}
