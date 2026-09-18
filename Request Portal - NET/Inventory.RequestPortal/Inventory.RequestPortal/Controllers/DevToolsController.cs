using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Inventory.RequestPortal.Services;
using Inventory.RequestPortal.Models;
using Inventory.RequestPortal.Extensions;
using Inventory.RequestPortal.Filters;
using Microsoft.Data.SqlClient;

namespace Inventory.RequestPortal.Controllers
{
    /// <summary>
    /// Internal tool controller for database switching and diagnostics.
    /// Requires a logged-in session with IsDeveloper set (mirrors AppSession.IsDeveloper
    /// in the WinForms app's DatabaseSetupForm) — see IsDeveloperUser().
    ///
    /// Available endpoints:
    /// - POST /DevTools/ResetDatabase    - Truncates Request and Inventory tables (dev only)
    /// - GET  /DevTools/GetAvailableDatabases - Returns list of available databases
    /// - GET  /DevTools/GetCurrentDatabase    - Returns currently selected database
    /// - POST /DevTools/SetDatabase           - Sets the active database for the session
    /// - GET  /DevTools/GetLocalDatabases     - Lists databases on local SQL Server
    /// - POST /DevTools/SetLocalDatabase      - Switches to a local Windows Auth database
    /// </summary>
    [ApiController]
    [Route("[controller]")]
    [RequireLogin]
    public class DevToolsController : ControllerBase, IActionFilter
    {
        private readonly IDevToolsService _devToolsService;
        private readonly ILogger<DevToolsController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IConfiguration _configuration;

        public DevToolsController(
            IDevToolsService devToolsService,
            ILogger<DevToolsController> logger,
            IWebHostEnvironment environment,
            IConfiguration configuration)
        {
            _devToolsService = devToolsService;
            _logger = logger;
            _environment = environment;
            _configuration = configuration;
        }

        /// <summary>
        /// Blocks every action in this controller outside the Development environment,
        /// before IsDeveloperUser() even runs. This entire controller only exists as a
        /// local dev convenience (see class summary) — a "developer" User row is a normal,
        /// persistent DB flag, not an environment control, so IsDeveloperUser() alone was
        /// not enough to keep this off production. Individual actions no longer need their
        /// own environment check (ResetDatabase's is now redundant but harmless).
        /// </summary>
        void IActionFilter.OnActionExecuting(ActionExecutingContext context)
        {
            if (!_environment.IsDevelopment())
            {
                _logger.LogError(
                    "SECURITY: DevTools action {Action} blocked outside Development environment (from {IpAddress})",
                    context.ActionDescriptor.DisplayName, HttpContext.Connection.RemoteIpAddress);
                context.Result = StatusCode(403, new
                {
                    success = false,
                    message = "DevTools is only available in the Development environment."
                });
            }
        }

        void IActionFilter.OnActionExecuted(ActionExecutedContext context) { }

        /// <summary>
        /// True when the logged-in session belongs to a developer account.
        /// Mirrors AppSession.IsDeveloper in the WinForms app.
        /// </summary>
        private bool IsDeveloperUser()
        {
            var user = HttpContext.Session.GetObject<UserSessionModel>(AccountController.SessionKeyUser);
            return user?.IsDeveloper == true;
        }

        private IActionResult ForbidNotDeveloper()
        {
            _logger.LogWarning("DevTools: non-developer session attempted access from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);
            return StatusCode(403, new { success = false, message = "This tool is restricted to developer accounts." });
        }

        /// <summary>
        /// Resets the database by truncating Request and Inventory tables.
        /// Restricted to Development environment — too destructive for production.
        /// </summary>
        [HttpPost("ResetDatabase")]
        public async Task<IActionResult> ResetDatabase()
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            if (!_environment.IsDevelopment())
            {
                _logger.LogError("SECURITY: Attempted database reset in non-Development environment");
                return StatusCode(403, new
                {
                    success = false,
                    message = "Database reset is only allowed in Development environment."
                });
            }

            _logger.LogWarning("DevTools: Database reset endpoint called from {IpAddress}",
                HttpContext.Connection.RemoteIpAddress);

            try
            {
                var result = await _devToolsService.ResetDatabaseAsync();

                if (result.Success)
                {
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        requestsDeleted = result.RequestsDeleted,
                        inventoryRowsDeleted = result.InventoryRowsDeleted,
                        durationMs = result.Duration.TotalMilliseconds
                    });
                }
                else
                {
                    return StatusCode(500, new
                    {
                        success = false,
                        message = result.Message,
                        durationMs = result.Duration.TotalMilliseconds
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled exception in ResetDatabase endpoint");
                return StatusCode(500, new { success = false, message = $"Unhandled error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Health check endpoint to verify DevTools controller is available.
        /// </summary>
        [HttpGet("Ping")]
        public IActionResult Ping()
        {
            return Ok(new
            {
                message = "DevTools controller is active",
                environment = _environment.EnvironmentName,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Returns list of available databases for switching.
        /// </summary>
        [HttpGet("GetAvailableDatabases")]
        public IActionResult GetAvailableDatabases()
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            return Ok(new
            {
                databases = DatabaseOptions.AvailableDatabases,
                current = GetCurrentDatabaseFromSession()
            });
        }

        /// <summary>
        /// Returns the currently selected database from session.
        /// </summary>
        [HttpGet("GetCurrentDatabase")]
        public IActionResult GetCurrentDatabase()
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            var currentDb = GetCurrentDatabaseFromSession();
            var preference = HttpContext.Session.GetObject<DatabasePreference>("DatabasePreference");

            return Ok(new
            {
                database = currentDb,
                sessionPreference = preference,
                sessionId = HttpContext.Session.Id,
                timestamp = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Returns available databases on the local SQL Server instance (Windows Auth).
        /// </summary>
        [HttpGet("GetLocalDatabases")]
        public async Task<IActionResult> GetLocalDatabases()
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            try
            {
                var localConnStr = _configuration.GetConnectionString("LocalConnection")
                    ?? "Server=localhost;Integrated Security=True;TrustServerCertificate=True;Encrypt=False;";

                var databases = new List<string>();

                using var con = new SqlConnection(localConnStr);
                await con.OpenAsync();

                using var cmd = new SqlCommand(
                    "SELECT name FROM sys.databases WHERE name NOT IN ('master','tempdb','model','msdb') ORDER BY name",
                    con);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    databases.Add(reader.GetString(0));

                var preference = HttpContext.Session.GetObject<DatabasePreference>("DatabasePreference");
                var currentLocal = preference?.UseLocalDatabase == true ? preference.LocalDatabaseName : null;

                return Ok(new { success = true, databases, current = currentLocal });
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message, databases = Array.Empty<string>() });
            }
        }

        /// <summary>
        /// Switches to a local database using Windows Authentication.
        /// </summary>
        [HttpPost("SetLocalDatabase")]
        public IActionResult SetLocalDatabase([FromBody] SetLocalDatabaseRequest request)
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            if (string.IsNullOrWhiteSpace(request.DatabaseName))
                return BadRequest(new { success = false, message = "Database name is required" });

            var preference = new DatabasePreference
            {
                UseLocalDatabase  = true,
                LocalDatabaseName = request.DatabaseName,
                DatabaseName      = string.Empty,
                LastChanged       = DateTime.UtcNow
            };

            HttpContext.Session.SetObject("DatabasePreference", preference);

            _logger.LogWarning("DevTools: Switched to LOCAL database {Database} (Windows Auth) from {IpAddress}",
                request.DatabaseName, HttpContext.Connection.RemoteIpAddress);

            return Ok(new { success = true, message = $"Now using local database: {request.DatabaseName} (Windows Auth)" });
        }

        /// <summary>
        /// Switches to a remote database by name.
        /// </summary>
        [HttpPost("SetDatabase")]
        public IActionResult SetDatabase([FromBody] SetDatabaseRequest request)
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            if (string.IsNullOrWhiteSpace(request.DatabaseName))
                return BadRequest(new { success = false, message = "Database name is required" });

            if (!DatabaseOptions.AvailableDatabases.Contains(request.DatabaseName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Invalid database name. Allowed: {string.Join(", ", DatabaseOptions.AvailableDatabases)}"
                });
            }

            var preference = new DatabasePreference
            {
                DatabaseName     = request.DatabaseName,
                UseLocalDatabase = false,
                LastChanged      = DateTime.UtcNow
            };

            HttpContext.Session.SetObject("DatabasePreference", preference);

            _logger.LogWarning("DevTools: Switched to remote database {Database} from {IpAddress}",
                request.DatabaseName, HttpContext.Connection.RemoteIpAddress);

            return Ok(new { success = true, message = $"Database switched to {request.DatabaseName}", database = request.DatabaseName });
        }

        /// <summary>
        /// Tests a custom connection and saves it to the session if successful.
        /// Mirrors the WinForms DatabaseSetupForm Test + Save flow.
        /// </summary>
        [HttpPost("SetCustomConnection")]
        public async Task<IActionResult> SetCustomConnection([FromBody] SetCustomConnectionRequest request)
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            if (string.IsNullOrWhiteSpace(request.Server))
                return BadRequest(new { success = false, message = "Server address is required." });
            if (string.IsNullOrWhiteSpace(request.Database))
                return BadRequest(new { success = false, message = "Database name is required." });
            if (!request.UseWindowsAuth && string.IsNullOrWhiteSpace(request.Username))
                return BadRequest(new { success = false, message = "Username is required for SQL Server auth." });

            var builder = new SqlConnectionStringBuilder
            {
                DataSource            = request.Server.Trim(),
                InitialCatalog        = request.Database.Trim(),
                TrustServerCertificate = true,
                Encrypt               = false,
                ConnectTimeout        = 10
            };

            if (request.UseWindowsAuth)
            {
                builder.IntegratedSecurity = true;
            }
            else
            {
                builder.UserID   = request.Username!.Trim();
                builder.Password = request.Password ?? string.Empty;
            }

            var connStr = builder.ConnectionString;

            try
            {
                using var con = new SqlConnection(connStr);
                await con.OpenAsync();
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = $"Connection failed: {ex.Message}" });
            }

            var preference = new DatabasePreference
            {
                UseCustomConnection    = true,
                CustomConnectionString = connStr,
                DatabaseName           = request.Database.Trim(),
                UseLocalDatabase       = false,
                LastChanged            = DateTime.UtcNow
            };

            HttpContext.Session.SetObject("DatabasePreference", preference);

            _logger.LogWarning("DevTools: Custom connection set to {Server}/{Database} from {IpAddress}",
                request.Server, request.Database, HttpContext.Connection.RemoteIpAddress);

            return Ok(new { success = true, message = $"Connected: {request.Server} / {request.Database}" });
        }

        /// <summary>
        /// Returns the parsed fields of the default appsettings connection for pre-filling the custom form.
        /// Always reads from DefaultConnection (appsettings) so the form starts with known-good credentials.
        /// Password is never returned.
        /// </summary>
        [HttpGet("GetConnectionDetails")]
        public IActionResult GetConnectionDetails()
        {
            if (!IsDeveloperUser()) return ForbidNotDeveloper();

            var connStr = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

            try
            {
                var b = new SqlConnectionStringBuilder(connStr);
                return Ok(new
                {
                    server          = b.DataSource,
                    database        = b.InitialCatalog,
                    useWindowsAuth  = b.IntegratedSecurity,
                    username        = b.IntegratedSecurity ? string.Empty : b.UserID
                });
            }
            catch
            {
                return Ok(new { server = string.Empty, database = string.Empty, useWindowsAuth = false, username = string.Empty });
            }
        }

        private string GetCurrentDatabaseFromSession()
        {
            var preference = HttpContext.Session.GetObject<DatabasePreference>("DatabasePreference");
            if (preference?.UseCustomConnection == true && !string.IsNullOrWhiteSpace(preference.CustomConnectionString))
            {
                try
                {
                    var b = new SqlConnectionStringBuilder(preference.CustomConnectionString);
                    return $"{b.DataSource} / {b.InitialCatalog}";
                }
                catch { return "[Custom]"; }
            }
            if (preference?.UseLocalDatabase == true)
                return $"[LOCAL] {preference.LocalDatabaseName}";
            return preference?.DatabaseName ?? "YIMS_PROD";
        }
    }

    public class SetLocalDatabaseRequest
    {
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class SetDatabaseRequest
    {
        public string DatabaseName { get; set; } = string.Empty;
    }

    public class SetCustomConnectionRequest
    {
        public string Server         { get; set; } = string.Empty;
        public string Database       { get; set; } = string.Empty;
        public bool   UseWindowsAuth { get; set; } = false;
        public string? Username      { get; set; }
        public string? Password      { get; set; }
    }
}
