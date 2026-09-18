using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Options;
using Yakult.ITCM.Server.Services;

namespace Yakult.ITCM.Server.Pages;

// Read-only for viewers: any signed-in account may GET this page, but
// Save / Test-connection POSTs stay administrator-only (checked explicitly
// in each handler below).
[Authorize]
[AutoValidateAntiforgeryToken]
public class ConfigModel : PageModel
{
    private readonly IItcmRepository _repo;
    private readonly IOptionsMonitor<ItcmSchedulerOptions> _schedulerOptions;
    private readonly IConfiguration _configuration;
    private readonly ItcmAuditService _audit;

    [BindProperty]
    public string Server { get; set; } = string.Empty;

    [BindProperty]
    public string Database { get; set; } = "YIMS";

    [BindProperty]
    public string AuthType { get; set; } = "Windows";

    [BindProperty]
    public string? Username { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public bool SchedulerEnabled { get; set; } = true;

    [BindProperty]
    public int IntervalMinutes { get; set; } = 15;

    [BindProperty]
    public int MaxTicketsPerRun { get; set; } = 20;

    [TempData]
    public string? StatusMessage { get; set; }

    [TempData]
    public bool StatusSuccess { get; set; }

    public string? CurrentServer { get; private set; }
    public string? CurrentDatabase { get; private set; }
    public string CurrentAuthType { get; private set; } = "Windows";
    public string? CurrentUsername { get; private set; }

    public bool IsAdministrator { get; private set; }

    public ConfigModel(
        IItcmRepository repo,
        IOptionsMonitor<ItcmSchedulerOptions> schedulerOptions,
        IConfiguration configuration,
        ItcmAuditService audit)
    {
        _repo = repo;
        _schedulerOptions = schedulerOptions;
        _configuration = configuration;
        _audit = audit;
    }

    public void OnGet()
    {
        IsAdministrator = User.HasClaim("ItcmAdministrator", "true");
        ParseConnectionString(_repo.ConnectionString);
        SchedulerEnabled = _schedulerOptions.CurrentValue.Enabled;
        IntervalMinutes = _schedulerOptions.CurrentValue.IntervalMinutes;
        MaxTicketsPerRun = _schedulerOptions.CurrentValue.MaxTicketsPerRun;
        Server = CurrentServer ?? "localhost";
        Database = CurrentDatabase ?? "YIMS";
        AuthType = CurrentAuthType;
        Username = CurrentUsername;
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        if (!User.HasClaim("ItcmAdministrator", "true"))
        {
            _audit.Record(HttpContext, "ConfigTestConnection", false, "non-administrator-blocked");
            return new JsonResult(new { success = false, message = "Administrator access required. Sign in as an administrator to test connections." })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        try
        {
            var cs = BuildConnectionString();
            var ok = await _repo.TestConnectionAsync(cs);
            _audit.Record(HttpContext, "ConfigTestConnection", ok, ok ? null : "connection-failed");
            return new JsonResult(new
            {
                success = ok,
                message = ok ? "Connected successfully" : "Failed to connect"
            });
        }
        catch
        {
            _audit.Record(HttpContext, "ConfigTestConnection", false, "validation-or-connection-error");
            return new JsonResult(new { success = false, message = "Connection test failed." })
            {
                StatusCode = StatusCodes.Status400BadRequest
            };
        }
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        if (!User.HasClaim("ItcmAdministrator", "true"))
        {
            StatusMessage = "Administrator access required. Sign in as an administrator to save configuration.";
            StatusSuccess = false;
            _audit.Record(HttpContext, "ConfigSave", false, "non-administrator-blocked");
            return RedirectToPage();
        }

        if (!ValidateSettings())
        {
            StatusSuccess = false;
            return RedirectToPage();
        }

        try
        {
            var cs = BuildConnectionString();
            var csWithTimeout = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs)
            {
                ConnectTimeout = 8
            }.ConnectionString;

            var ok = await _repo.TestConnectionAsync(csWithTimeout);
            if (!ok)
            {
                StatusMessage = "Connection failed — settings were not saved. Verify the server, credentials, and firewall.";
                StatusSuccess = false;
                _audit.Record(HttpContext, "ConfigSave", false, "connection-test-failed");
                return RedirectToPage();
            }

            var localConfig = new Dictionary<string, object?>
            {
                ["ItcmScheduler"] = new Dictionary<string, object>
                {
                    ["Enabled"] = SchedulerEnabled,
                    ["IntervalMinutes"] = IntervalMinutes,
                    ["MaxTicketsPerRun"] = MaxTicketsPerRun,
                    // Preserve the durable pause flag so saving settings does
                    // not silently resume a deliberately paused scheduler.
                    ["Paused"] = _schedulerOptions.CurrentValue.Paused
                },
                ["ConnectionStrings"] = new Dictionary<string, string>
                {
                    ["Yakult_Inventory_System"] = cs
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(localConfig, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            var path = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
            // Atomic write so a crash cannot leave a torn config file behind.
            var tempPath = path + ".tmp";
            await System.IO.File.WriteAllTextAsync(tempPath, json);
            System.IO.File.Move(tempPath, path, overwrite: true);

            if (_configuration is IConfigurationRoot configurationRoot)
                configurationRoot.Reload();

            _repo.UpdateConnectionString(cs);
            StatusMessage = "Configuration saved and applied successfully.";
            StatusSuccess = true;
            _audit.Record(HttpContext, "ConfigSave", true,
                $"schedulerEnabled={SchedulerEnabled};intervalMinutes={IntervalMinutes};maxTicketsPerRun={MaxTicketsPerRun}");
            return RedirectToPage();
        }
        catch
        {
            StatusMessage = "Configuration could not be saved. Check the server logs for details.";
            StatusSuccess = false;
            _audit.Record(HttpContext, "ConfigSave", false, "unexpected-error");
            return RedirectToPage();
        }
    }

    private bool ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(Server) || string.IsNullOrWhiteSpace(Database))
        {
            StatusMessage = "Server and database are required.";
            return false;
        }

        if (IntervalMinutes is < 1 or > 1440)
        {
            StatusMessage = "Run interval must be between 1 and 1440 minutes.";
            return false;
        }

        if (MaxTicketsPerRun is < 1 or > 500)
        {
            StatusMessage = "Maximum tickets per run must be between 1 and 500.";
            return false;
        }

        if (!string.Equals(AuthType, "Windows", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(AuthType, "SQL", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "Unsupported database authentication type.";
            return false;
        }

        if (string.Equals(AuthType, "SQL", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(Username))
        {
            StatusMessage = "A SQL username is required.";
            return false;
        }

        return true;
    }

    private string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = (Server ?? string.Empty).Trim(),
            InitialCatalog = (Database ?? string.Empty).Trim(),
            TrustServerCertificate = true
        };

        if (string.Equals(AuthType, "Windows", StringComparison.OrdinalIgnoreCase))
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = (Username ?? string.Empty).Trim();
            // The password is never rendered back into the form. When only
            // scheduler settings change, retain the existing SQL password.
            if (string.IsNullOrWhiteSpace(Password))
            {
                try
                {
                    var current = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_repo.ConnectionString);
                    builder.Password = current.Password;
                }
                catch
                {
                    builder.Password = string.Empty;
                }
            }
            else
            {
                builder.Password = Password;
            }
        }

        return builder.ConnectionString;
    }

    private void ParseConnectionString(string cs)
    {
        try
        {
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs);
            CurrentServer = builder.DataSource;
            CurrentDatabase = builder.InitialCatalog;
            CurrentAuthType = builder.IntegratedSecurity ? "Windows" : "SQL";
            CurrentUsername = builder.IntegratedSecurity ? null : builder.UserID;
        }
        catch
        {
            CurrentServer = "localhost";
            CurrentDatabase = "YIMS";
            CurrentAuthType = "Windows";
            CurrentUsername = null;
        }
    }
}
