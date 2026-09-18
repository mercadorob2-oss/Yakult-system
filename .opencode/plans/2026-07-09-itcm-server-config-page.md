# ITCM Server Configuration Page Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a web-based configuration page to the ITCM Server dashboard where the user can enter SQL connection settings, scheduler options, and save changes without manually editing JSON files.

**Architecture:** New Razor Page (`/Config`) with form fields for DB connection + scheduler settings. Writes to `appsettings.local.json` for persistence. Immediate in-memory update via `UpdateConnectionString` on the repository so no restart is required for connection changes. Scheduler settings use `IOptionsSnapshot` for hot-reload.

**Tech Stack:** ASP.NET Core 8 Razor Pages, Microsoft.Data.SqlClient, System.Text.Json

---

### Task 1: Add appsettings.local.json support to Program.cs

**Files:**
- Modify: `Yakult.ITCM.Server/Program.cs:24` (after builder.Build -> before app.UseHttpsRedirection)

- [ ] **Add local config file loading before builder.Build()**

Add `AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true)` to the configuration builder so that the local override file is loaded with highest priority (after appsettings.json and appsettings.Development.json):

```csharp
// In the var builder = WebApplication.CreateBuilder(args); section, before var app = builder.Build();
// Add this line after builder.Services.AddRazorPages():

builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: true);
```

- [ ] **Verify build succeeds**

Run: `dotnet build Yakult.ITCM.Server\Yakult.ITCM.Server.csproj`
Expected: Build succeeded, 0 errors

---

### Task 2: Add TestConnection and UpdateConnectionString to the repository

**Files:**
- Modify: `Yakult.ITCM.Server/Data/IItcmRepository.cs` -- add `TestConnectionAsync(string connectionString)` and `UpdateConnectionString(string)`
- Modify: `Yakult.ITCM.Server/Data/ItcmRepository.cs` -- implement the new methods

- [ ] **Add two new methods to the interface**

```csharp
// Add to IItcmRepository.cs after the existing TestConnectionAsync line (line 43):
Task<bool> TestConnectionAsync(string connectionString);
void UpdateConnectionString(string connectionString);
```

- [ ] **Implement in ItcmRepository.cs**

Change `_connectionString` from `readonly` to mutable:
```csharp
// Line 9: change from:
private readonly string _connectionString;
// to:
private string _connectionString;
```

Add two new methods after the existing `TestConnectionAsync()` (around line 790):

```csharp
public async Task<bool> TestConnectionAsync(string connectionString)
{
    try
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        return true;
    }
    catch
    {
        return false;
    }
}

public void UpdateConnectionString(string connectionString)
{
    _connectionString = connectionString;
}
```

- [ ] **Verify build succeeds**

Run: `dotnet build Yakult.ITCM.Server\Yakult.ITCM.Server.csproj`
Expected: Build succeeded, 0 errors

---

### Task 3: Create the Config Razor Page

**Files:**
- Create: `Yakult.ITCM.Server/Pages/Config.cshtml.cs`
- Create: `Yakult.ITCM.Server/Pages/Config.cshtml`

- [ ] **Create Config.cshtml.cs PageModel**

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Options;

namespace Yakult.ITCM.Server.Pages;

public class ConfigModel : PageModel
{
    private readonly IItcmRepository _repo;
    private readonly IConfiguration _configuration;
    private readonly IOptions<ItcmSchedulerOptions> _schedulerOptions;

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

    public string CurrentConnectionString { get; private set; } = string.Empty;
    public string? CurrentServer { get; private set; }
    public string? CurrentDatabase { get; private set; }
    public string CurrentAuthType { get; private set; } = "Windows";

    public ConfigModel(
        IItcmRepository repo,
        IConfiguration configuration,
        IOptions<ItcmSchedulerOptions> schedulerOptions)
    {
        _repo = repo;
        _configuration = configuration;
        _schedulerOptions = schedulerOptions;
    }

    public void OnGet()
    {
        CurrentConnectionString = _repo.ConnectionString;
        ParseConnectionString(CurrentConnectionString);
        SchedulerEnabled = _schedulerOptions.Value.Enabled;
        IntervalMinutes = _schedulerOptions.Value.IntervalMinutes;
        MaxTicketsPerRun = _schedulerOptions.Value.MaxTicketsPerRun;
        Server = CurrentServer ?? "localhost";
        Database = CurrentDatabase ?? "YIMS";
    }

    public async Task<IActionResult> OnPostTestConnectionAsync()
    {
        var cs = BuildConnectionString();
        var ok = await _repo.TestConnectionAsync(cs);
        return new JsonResult(new { success = ok, message = ok ? "Connected successfully" : "Failed to connect" });
    }

    public async Task<IActionResult> OnPostSaveAsync()
    {
        var cs = BuildConnectionString();

        var ok = await _repo.TestConnectionAsync(cs);
        if (!ok)
        {
            StatusMessage = "Connection failed -- settings not saved. Verify your credentials and try again.";
            StatusSuccess = false;
            return RedirectToPage();
        }

        var localConfig = new Dictionary<string, object?>
        {
            ["ItcmScheduler"] = new Dictionary<string, object>
            {
                ["Enabled"] = SchedulerEnabled,
                ["IntervalMinutes"] = IntervalMinutes,
                ["MaxTicketsPerRun"] = MaxTicketsPerRun
            },
            ["ConnectionStrings"] = new Dictionary<string, string>
            {
                ["Yakult_Inventory_System"] = cs
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(localConfig, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var path = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "appsettings.local.json");
        await System.IO.File.WriteAllTextAsync(path, json);

        _repo.UpdateConnectionString(cs);

        StatusMessage = "Configuration saved and applied successfully!";
        StatusSuccess = true;
        return RedirectToPage();
    }

    private string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            TrustServerCertificate = true
        };

        if (AuthType == "Windows")
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = Username ?? "";
            builder.Password = Password ?? "";
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
        }
        catch
        {
            CurrentServer = "localhost";
            CurrentDatabase = "YIMS";
            CurrentAuthType = "Windows";
        }
    }
}
```

- [ ] **Create Config.cshtml Razor Page**

```html
@page
@namespace Yakult.ITCM.Server.Pages
@model ConfigModel
@{
    ViewData["Title"] = "Server Configuration";
}

<header>
  <div>
    <h1>Configuration</h1>
    <div class="sub">ITCM Server connection and scheduler settings</div>
  </div>
  <div class="toolbar">
    <a href="/" style="text-decoration:none"><button>Back to Dashboard</button></a>
  </div>
</header>

<main>
  @if (TempData["StatusMessage"] is string msg)
  {
    <div class="panel" style="margin-bottom:18px;border-color:@((bool)(TempData["StatusSuccess"] ?? false) ? "var(--green)" : "var(--red)");">
      <strong>@((bool)(TempData["StatusSuccess"] ?? false) ? "OK" : "Error"):</strong> @msg
    </div>
  }

  <form method="post" id="configForm">
    <section class="grid">
      <div class="panel span-4">
        <h2>Database Connection</h2>
        <table>
          <tr>
            <th>Server</th>
            <td><input type="text" asp-for="Server" style="width:100%;padding:8px;font-size:14px" /></td>
          </tr>
          <tr>
            <th>Database</th>
            <td><input type="text" asp-for="Database" style="width:100%;padding:8px;font-size:14px" /></td>
          </tr>
          <tr>
            <th>Authentication</th>
            <td>
              <select asp-for="AuthType" style="padding:8px;font-size:14px" onchange="toggleAuth()">
                <option value="Windows">Windows Authentication</option>
                <option value="SQL">SQL Server Authentication</option>
              </select>
            </td>
          </tr>
          <tr id="sqlAuthRow" style="display:none">
            <th>Username</th>
            <td><input type="text" asp-for="Username" style="width:100%;padding:8px;font-size:14px" /></td>
          </tr>
          <tr id="sqlPassRow" style="display:none">
            <th>Password</th>
            <td><input type="password" asp-for="Password" style="width:100%;padding:8px;font-size:14px" /></td>
          </tr>
        </table>
        <div style="margin-top:12px">
          <button type="button" class="primary" onclick="testConnection()">Test Connection</button>
          <span id="testResult" style="margin-left:12px"></span>
        </div>
      </div>
    </section>

    <section class="grid">
      <div class="panel span-4">
        <h2>Scheduler Settings</h2>
        <table>
          <tr>
            <th>Enabled</th>
            <td>
              <select asp-for="SchedulerEnabled" style="padding:8px;font-size:14px">
                <option value="true">Enabled</option>
                <option value="false">Disabled</option>
              </select>
            </td>
          </tr>
          <tr>
            <th>Interval (minutes)</th>
            <td><input type="number" asp-for="IntervalMinutes" min="1" max="1440" style="width:100px;padding:8px;font-size:14px" /></td>
          </tr>
          <tr>
            <th>Max tickets per run</th>
            <td><input type="number" asp-for="MaxTicketsPerRun" min="1" max="500" style="width:100px;padding:8px;font-size:14px" /></td>
          </tr>
        </table>
      </div>
    </section>

    <div style="margin-top:18px">
      <button type="submit" asp-page-handler="Save" class="good" style="font-size:15px;padding:12px 24px">Save Configuration</button>
      <span style="color:var(--muted);margin-left:12px">Settings are tested before persisting</span>
    </div>
  </form>
</main>

<script>
  function toggleAuth() {
    var val = document.getElementById('Config_AuthType').value;
    document.getElementById('sqlAuthRow').style.display = val === 'SQL' ? '' : 'none';
    document.getElementById('sqlPassRow').style.display = val === 'SQL' ? '' : 'none';
  }
  toggleAuth();

  async function testConnection() {
    var btn = event.target;
    btn.disabled = true;
    btn.textContent = 'Testing...';
    document.getElementById('testResult').textContent = '';

    var server = document.getElementById('Config_Server').value;
    var db = document.getElementById('Config_Database').value;
    var auth = document.getElementById('Config_AuthType').value;
    var user = document.getElementById('Config_Username').value;
    var pass = document.getElementById('Config_Password').value;

    const res = await fetch('/api/itcm/config/test-connection', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ server, database: db, authType: auth, username: user, password: pass })
    });
    const result = await res.json();

    btn.disabled = false;
    btn.textContent = 'Test Connection';

    var el = document.getElementById('testResult');
    if (result.success) {
      el.innerHTML = '<span class="pill ok">Connected</span>';
    } else {
      el.innerHTML = '<span class="pill fail">Failed: ' + result.message + '</span>';
    }
  }
</script>
```

- [ ] **Verify build succeeds**

Run: `dotnet build Yakult.ITCM.Server\Yakult.ITCM.Server.csproj`
Expected: Build succeeded, 0 errors

---

### Task 4: Add Config API endpoint for test-connection

**Files:**
- Modify: `Yakult.ITCM.Server/Program.cs` -- add test-connection POST endpoint and add button to dashboard

- [ ] **Add the test-connection endpoint**

Add after the heartbeat endpoint (after line 75):

```csharp
// -- Config: Test Connection ---------------------------------------------------------
app.MapPost("/api/itcm/config/test-connection", async (IItcmRepository repo, TestConnectionRequest req) =>
{
    var cs = req.BuildConnectionString();
    var ok = await repo.TestConnectionAsync(cs);
    return Results.Ok(new { success = ok, message = ok ? "Connected successfully" : "Connection failed -- check server, credentials, and firewall" });
});

record TestConnectionRequest(string Server, string Database, string AuthType, string? Username, string? Password)
{
    public string BuildConnectionString()
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Server,
            InitialCatalog = Database,
            TrustServerCertificate = true
        };
        if (AuthType == "Windows")
            builder.IntegratedSecurity = true;
        else
        {
            builder.IntegratedSecurity = false;
            builder.UserID = Username ?? "";
            builder.Password = Password ?? "";
        }
        return builder.ConnectionString;
    }
}
```

Add the required `using` directive at the top if not already present:
```csharp
using Microsoft.Data.SqlClient;
```

- [ ] **Add Config button to dashboard**

In `Pages/Index.cshtml`, add a Config button after the Refresh button in the toolbar:
```html
<a href="/Config" style="text-decoration:none"><button type="button">Config</button></a>
```

- [ ] **Verify build succeeds**

Run: `dotnet build Yakult.ITCM.Server\Yakult.ITCM.Server.csproj`
Expected: Build succeeded, 0 errors

---

### Task 5: Update CSS for config page form elements

**Files:**
- Modify: `Yakult.ITCM.Server/wwwroot/css/dashboard.css` -- add styles for inputs, selects, and form layout

- [ ] **Add form element styles**

Append at the end of `dashboard.css`:

```css
input, select, textarea {
  font-family: Segoe UI, Arial, sans-serif;
  border: 1px solid var(--line);
  border-radius: 6px;
  background: var(--panel);
  color: var(--text);
}
input:focus, select:focus {
  outline: none;
  border-color: var(--blue);
  box-shadow: 0 0 0 2px rgba(36,119,191,0.15);
}
```

---

### Task 6: Restart server and test end-to-end

- [ ] **Restart the server and verify the config page works**

```bash
# Kill old process
Get-Process -Name "dotnet" | Where-Object { $_.CommandLine -like "*Yakult.ITCM.Server*" } | Stop-Process -Force

# Restart
Start-Process -NoNewWindow -FilePath "dotnet" -ArgumentList "run --project Yakult.ITCM.Server\Yakult.ITCM.Server.csproj --urls http://localhost:50330"

# Wait for startup
Start-Sleep -Seconds 8

# Test config page loads
curl.exe -s http://localhost:50330/Config | Select-String -Pattern "Configuration"

# Test the API test-connection endpoint
curl.exe -s -X POST http://localhost:50330/api/itcm/config/test-connection -H "Content-Type: application/json" -d '{\"server\":\"localhost\",\"database\":\"YIMS\",\"authType\":\"Windows\"}'
```

---

## Task Dependency Map

```
Task 1 (appsettings.local.json)
    |
    v
Task 2 (repository methods) ----+
    |                           |
    v                           v
Task 3 (Config Razor Page)   Task 4 (API endpoint + dashboard button)
    |                           |
    +----------->---------------+
                |
                v
          Task 5 (CSS styles)
                |
                v
          Task 6 (test)
```

Tasks 1-2 should be done first (dependencies for Task 3). Tasks 3 and 4 depend on Task 2. Task 5 is independent. Task 6 is last.
