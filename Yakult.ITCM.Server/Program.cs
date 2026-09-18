using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Yakult.ITCM.Server.Data;
using Yakult.ITCM.Server.Models;
using Yakult.ITCM.Server.Options;
using Yakult.ITCM.Server.Security;
using Yakult.ITCM.Server.Services;

// Set web root explicitly so static files are found when running the compiled exe
// directly (e.g. from VS or bin\Debug), where the working directory may not be
// the project folder and ASPNETCORE_ENVIRONMENT may not be set to Development.
var wwwrootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = Directory.Exists(wwwrootPath) ? wwwrootPath : null
});

// The Configuration page writes this file beside the running executable, so read
// the exact same physical file and watch it for changes.
var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
builder.Configuration.AddJsonFile(localConfigPath, optional: true, reloadOnChange: true);

builder.Services.Configure<ItcmSchedulerOptions>(
    builder.Configuration.GetSection(ItcmSchedulerOptions.SectionName));

// ── Persistent cookie encryption keys ─────────────────────────────────────────────
// Keep keys outside the publish folder so deployment does not invalidate all
// administrator sessions. Grant Modify access only to the IIS app-pool identity.
var dataProtectionKeyPath = builder.Configuration["ItcmAuth:DataProtectionKeysPath"];
if (string.IsNullOrWhiteSpace(dataProtectionKeyPath))
{
    dataProtectionKeyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Yakult",
        "ITCM",
        "DataProtection-Keys");
}
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("Yakult.ITCM.Server");

// ── Data and services ──────────────────────────────────────────────────────────────
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});
builder.Services.AddSingleton<IItcmRepository, ItcmRepository>();
builder.Services.AddSingleton<IItcmAuthenticationService, ItcmAuthenticationService>();
builder.Services.AddSingleton<ItcmAuditService>();
builder.Services.AddSingleton<ItcmSchedulerState>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddScoped<IItcmBackgroundJob, ItcmBackgroundJob>();
builder.Services.AddSingleton<ItcmSchedulerHostedService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<ItcmSchedulerHostedService>());

// ── Authentication and authorization ───────────────────────────────────────────────
var requireHttps = builder.Configuration.GetValue("ItcmAuth:RequireHttps", false);
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = ItcmAuthDefaults.Scheme;
    options.DefaultChallengeScheme = ItcmAuthDefaults.Scheme;
    options.DefaultSignInScheme = ItcmAuthDefaults.Scheme;
})
.AddCookie(ItcmAuthDefaults.Scheme, options =>
{
    options.Cookie.Name = ItcmAuthDefaults.CookieName;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = requireHttps
        ? CookieSecurePolicy.Always
        : CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = false;
    options.Events = new CookieAuthenticationEvents
    {
        OnRedirectToLogin = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return Task.CompletedTask;
            }

            var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
            var loginUrl = $"/Account/Login?ReturnUrl={Uri.EscapeDataString(returnUrl)}";
            context.Response.Redirect(loginUrl);
            return Task.CompletedTask;
        },
        OnRedirectToAccessDenied = context =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                return Task.CompletedTask;
            }

            context.Response.Redirect("/Account/AccessDenied");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(ItcmAuthDefaults.AdministratorPolicy, policy =>
    {
        policy.AddAuthenticationSchemes(ItcmAuthDefaults.Scheme);
        policy.RequireAuthenticatedUser();
        policy.RequireClaim("ItcmAdministrator", "true");
    });
});

builder.Services.AddRazorPages(options =>
{
    // Login is mandatory everywhere: any signed-in account may view the
    // dashboard and the Configuration page, but Configuration writes
    // (Save / Test) stay admin-gated inside ConfigModel.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Account/Login");
    options.Conventions.AllowAnonymousToPage("/Account/Logout");
    options.Conventions.AllowAnonymousToPage("/Account/AccessDenied");
});

var app = builder.Build();

if (!requireHttps)
    app.Logger.LogWarning("ITCM authentication is running with HTTP cookies. Enable ItcmAuth:RequireHttps before external or production exposure.");

app.UseStaticFiles();

// Fallback: serve from wwwroot next to the exe (needed when running compiled exe
// outside of dotnet run, where static web assets manifest may not resolve correctly).
var exeWwwRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
if (Directory.Exists(exeWwwRoot))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(exeWwwRoot),
        RequestPath = ""
    });
}

app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

// ── Health ─────────────────────────────────────────────────────────────────────────
// Authenticated read endpoints below (any signed-in viewer). Writes and
// configuration stay behind the administrator policy.
app.MapGet("/api/itcm/health", async (IItcmRepository repo) =>
{
    var dbOk = await repo.TestConnectionAsync();
    return Results.Ok(new
    {
        Status = dbOk ? "Healthy" : "Degraded",
        Database = dbOk ? "Connected" : "Unreachable",
        Timestamp = DateTimeOffset.Now
    });
}).RequireAuthorization();

// ── Ping (anonymous) ─────────────────────────────────────────────────────────────
// Lightweight unauthenticated probe for desktop Diagnostics ("Web Server"
// status row). Deliberately excludes error text and run counts; anything
// sensitive stays behind the administrator policy on the endpoints above.
app.MapGet("/api/itcm/ping", (ItcmSchedulerState state, IOptionsMonitor<ItcmSchedulerOptions> opts) =>
{
    var snap = state.GetSnapshot();
    return Results.Ok(new
    {
        Ok = true,
        Version = "1.0.0",
        TimeUtc = DateTimeOffset.UtcNow,
        Scheduler = new
        {
            Enabled = opts.CurrentValue.Enabled,
            Paused = snap.IsPaused,
            Running = snap.IsRunning,
            NextRunAt = snap.NextRunAt,
            LastStartedAt = snap.LastStartedAt,
            LastCompletedAt = snap.LastCompletedAt,
            LastRunSucceeded = snap.LastRunSucceeded,
            TotalRuns = snap.TotalRuns
        }
    });
});

// ── Scheduler Status ───────────────────────────────────────────────────────────────
app.MapGet("/api/itcm/scheduler/status", (ItcmSchedulerState state) =>
    Results.Ok(state.GetSnapshot())).RequireAuthorization();

// ── Client Presence ─────────────────────────────────────────────────────────────
// POST is anonymous by design (desktops hold no server credentials; the beat
// carries self-reported machine/user like ping). No CSRF check applies —
// there is no cookie session to forge. Writes are a single guarded MERGE.
// Reads stay behind the administrator policy.
app.MapPost("/api/itcm/presence", async (IItcmRepository repo, PresenceBeatRequest req) =>
{
    var ok = await repo.UpsertClientPresenceAsync(
        req.MachineName ?? string.Empty,
        req.UserName ?? string.Empty,
        req.Module,
        req.ClientVersion);
    return ok ? Results.Ok(new { Recorded = true }) : Results.Ok(new { Recorded = false });
});

app.MapGet("/api/itcm/presence", async (IItcmRepository repo, int onlineMinutes = 15) =>
{
    var report = await repo.GetClientPresenceAsync(Math.Clamp(onlineMinutes, 1, 1440));
    return Results.Ok(report);
}).RequireAuthorization();

// ── IT Employees (assignee picker source) ──────────────────────────────────────
app.MapGet("/api/itcm/it-employees", async (IItcmRepository repo) =>
{
    var items = await repo.GetItEmployeesAsync();
    return Results.Ok(items);
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Ticket Assign (dashboard triage parity with desktop AssignTicketAndNotifyAsync) ──
app.MapPost("/api/itcm/tickets/{ticketId}/assign", async (
    string ticketId,
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService,
    TicketAssignRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "TicketAssign"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var id = await ResolveTicketIdAsync(repo, ticketId);
    if (!id.HasValue)
    {
        audit.Record(context, "TicketAssign", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    var ticket = await repo.GetTicketNotificationDataAsync(id.Value);
    if (ticket is null)
    {
        audit.Record(context, "TicketAssign", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    if (IsFinalTicketStatus(ticket.Status))
    {
        audit.Record(context, "TicketAssign", false, "final-ticket");
        return Results.Conflict(new { Message = "Assignment cannot be changed on a final ticket. Reopen it first." });
    }
    if (req.AssignedToEmpId.HasValue)
    {
        if (req.AssignedToEmpId.Value <= 0)
        {
            audit.Record(context, "TicketAssign", false, "validation-error");
            return Results.BadRequest(new { Message = "AssignedToEmpId must be a positive employee id, or omitted to unassign." });
        }
        if (!await repo.IsItEmployeeAsync(req.AssignedToEmpId.Value))
        {
            audit.Record(context, "TicketAssign", false, "validation-error");
            return Results.BadRequest(new { Message = "AssignedToEmpId must reference an active IT employee." });
        }
    }
    if (ticket.AssignedToEmpId == req.AssignedToEmpId)
    {
        audit.Record(context, "TicketAssign", true, "already-assigned");
        return Results.Ok(new { success = true, message = "Already assigned." });
    }

    var previousAssignee = ticket.AssignedToEmpId;
    var previousAssigneeName = ticket.AssignedTo;
    var userId = GetUserId(context.User);
    await repo.AssignTicketEmployeeAsync(id.Value, req.AssignedToEmpId, userId);
    if (req.AssignedToEmpId.HasValue)
        await emailService.NotifyAssignmentAsync(id.Value, req.AssignedToEmpId.Value, userId, previousAssignee, previousAssigneeName);
    audit.Record(context, "TicketAssign", true, $"assignee={req.AssignedToEmpId?.ToString() ?? "unassigned"}");
    return Results.Ok(new
    {
        success = true,
        message = req.AssignedToEmpId.HasValue ? "Ticket assigned." : "Ticket unassigned."
    });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Ticket Escalate (manual escalation from the dashboard) ─────────────────────
app.MapPost("/api/itcm/tickets/{ticketId}/escalate", async (
    string ticketId,
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService,
    TicketEscalateRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "TicketEscalate"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var id = await ResolveTicketIdAsync(repo, ticketId);
    if (!id.HasValue)
    {
        audit.Record(context, "TicketEscalate", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    var ticket = await repo.GetTicketNotificationDataAsync(id.Value);
    if (ticket is null)
    {
        audit.Record(context, "TicketEscalate", false, "not-found");
        return Results.NotFound(new { Message = "Ticket not found." });
    }
    if (string.Equals(ticket.Status, "Escalated", StringComparison.OrdinalIgnoreCase))
    {
        audit.Record(context, "TicketEscalate", false, "already-escalated");
        return Results.Conflict(new { Message = "Ticket is already escalated." });
    }
    if (IsFinalTicketStatus(ticket.Status))
    {
        audit.Record(context, "TicketEscalate", false, "final-ticket");
        return Results.Conflict(new { Message = "A final ticket cannot be escalated. Reopen it first." });
    }
    if (string.Equals(ticket.TicketSource, "Portal", StringComparison.OrdinalIgnoreCase)
        && !ticket.AssignedToEmpId.HasValue)
    {
        audit.Record(context, "TicketEscalate", false, "portal-unassigned");
        return Results.Conflict(new { Message = "Portal tickets must be assigned to an IT employee before escalation." });
    }
    var note = (req.Note ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(note))
    {
        audit.Record(context, "TicketEscalate", false, "validation-error");
        return Results.BadRequest(new { Message = "An escalation note is required." });
    }

    var userId = GetUserId(context.User);
    await repo.SetTicketStatusAsync(id.Value, "Escalated", userId, note);
    await emailService.NotifyStatusChangeAsync(id.Value, ticket.Status ?? string.Empty, "Escalated", note, userId);
    audit.Record(context, "TicketEscalate", true, null);
    return Results.Ok(new { success = true, message = "Ticket escalated." });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Run Now ────────────────────────────────────────────────────────────────────────
app.MapPost("/api/itcm/scheduler/run-now", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    ItcmSchedulerHostedService scheduler,
    CancellationToken cancellationToken) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "SchedulerRunNow"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    var started = await scheduler.RunNowAsync(cancellationToken);
    audit.Record(context, "SchedulerRunNow", started, started ? null : "already-active");
    return started
        ? Results.Accepted("/api/itcm/scheduler/status")
        : Results.Conflict(new { Message = "An ITCM scheduler run is already active." });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Pause / Resume (durable: persisted to appsettings.local.json) ───────────────
app.MapPost("/api/itcm/scheduler/pause", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    ItcmSchedulerState state,
    IConfiguration configuration) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "SchedulerPause"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    state.Pause();
    PersistSchedulerPaused(configuration, audit, context, paused: true, action: "SchedulerPause");
    return Results.Ok(state.GetSnapshot());
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapPost("/api/itcm/scheduler/resume", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    ItcmSchedulerState state,
    IConfiguration configuration) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "SchedulerResume"))
        return Results.BadRequest(new { Message = "Invalid request token." });

    state.Resume();
    PersistSchedulerPaused(configuration, audit, context, paused: false, action: "SchedulerResume");
    return Results.Ok(state.GetSnapshot());
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

static void PersistSchedulerPaused(
    IConfiguration configuration,
    ItcmAuditService audit,
    HttpContext context,
    bool paused,
    string action)
{
    try
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.local.json");
        System.Text.Json.Nodes.JsonObject root;
        if (File.Exists(path))
        {
            var parsed = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(path));
            root = parsed as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject();
        }
        else
        {
            root = new System.Text.Json.Nodes.JsonObject();
        }

        var section = root[ItcmSchedulerOptions.SectionName] as System.Text.Json.Nodes.JsonObject;
        if (section is null)
        {
            section = new System.Text.Json.Nodes.JsonObject();
            root[ItcmSchedulerOptions.SectionName] = section;
        }
        section["Paused"] = paused;

        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, root.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        File.Move(tempPath, path, overwrite: true);

        if (configuration is IConfigurationRoot configurationRoot)
            configurationRoot.Reload();
        audit.Record(context, action, true, $"persistedPaused={paused}");
    }
    catch (Exception ex)
    {
        // Runtime state already flipped; persistence is best-effort so a
        // restart may resume an intentionally paused scheduler.
        audit.Record(context, action, true, $"persist-failed: {ex.Message}");
    }
}

// ── Heartbeat ──────────────────────────────────────────────────────────────────────
app.MapGet("/api/itcm/heartbeat", async (IItcmRepository repo) =>
{
    var summary = await repo.GetHeartbeatSummaryAsync();
    return summary is not null ? Results.Ok(summary) : Results.Ok(new { });
}).RequireAuthorization();

// ── Heartbeat History (paged) ──────────────────────────────────────────────────────
app.MapGet("/api/itcm/heartbeat/history", async (IItcmRepository repo, int page = 1, int pageSize = 20) =>
{
    var paged = await repo.GetHeartbeatPageAsync(Math.Max(1, page), Math.Clamp(pageSize, 1, 100));
    return Results.Ok(paged);
}).RequireAuthorization();

// ── Monitoring Dashboard ───────────────────────────────────────────────────────────
app.MapGet("/api/itcm/monitoring", async (IItcmRepository repo, int maxRows = 12) =>
{
    var dashboard = await repo.GetMonitoringDashboardAsync(Math.Clamp(maxRows, 1, 100));
    return Results.Ok(dashboard);
}).RequireAuthorization();

// ── Scheduler Settings ─────────────────────────────────────────────────────────────
app.MapGet("/api/itcm/scheduler/settings", (IOptionsMonitor<ItcmSchedulerOptions> opts) =>
    Results.Ok(opts.CurrentValue)).RequireAuthorization();

// ── Config: Test Connection ─────────────────────────────────────────────────────────
app.MapPost("/api/itcm/config/test-connection", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    TestConnectionRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "ConfigTestConnection"))
        return Results.BadRequest(new { success = false, message = "Invalid request token." });

    try
    {
        var cs = req.BuildConnectionString();
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(cs) { ConnectTimeout = 8 };
        var ok = await repo.TestConnectionAsync(builder.ConnectionString);
        audit.Record(context, "ConfigTestConnection", ok, ok ? null : "connection-failed");
        return Results.Ok(new
        {
            success = ok,
            message = ok ? "Connected successfully" : "Connection failed — check server address, credentials, and firewall"
        });
    }
    catch
    {
        audit.Record(context, "ConfigTestConnection", false, "validation-or-connection-error");
        return Results.BadRequest(new { success = false, message = "Connection test failed. Check the supplied settings." });
    }
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

// ── Email Notifications (admin only, no DB change) ──────────────────────────────
// The scheduler and all desktops share dbo.CallEmailSettings /
// dbo.CallNotificationRules (newest row wins), so saves here propagate to
// every connected device on its next read. Password blobs are never sent to
// the browser; a blank password retains the saved value.
app.MapGet("/api/itcm/email/settings", async (IItcmRepository repo) =>
{
    var s = await repo.GetGlobalEmailSettingsAsync();
    return Results.Ok(new
    {
        smtpServer = s?.SmtpServer ?? string.Empty,
        smtpPort = s?.SmtpPort ?? 25,
        useSsl = s?.UseSsl ?? false,
        smtpUsername = s?.SmtpUsername,
        fromName = s?.FromName,
        fromEmail = s?.FromEmail,
        hasSavedPassword = s?.SmtpPasswordEnc is { Length: > 0 }
    });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapGet("/api/itcm/email/rules", async (IItcmRepository repo) =>
{
    var r = await repo.GetNotificationRulesAsync();
    return Results.Ok(new
    {
        notifyOnNewTicket = r?.NotifyOnNewTicket ?? false,
        notifyOnStatusChange = r?.NotifyOnStatusChange ?? false,
        notifyOnEscalation = r?.NotifyOnEscalation ?? false,
        notifyOnReminder = r?.NotifyOnReminder ?? false,
        groupEmail = r?.GroupEmail,
        escalationEmail = r?.EscalationEmail,
        reminderDays = r?.ReminderDays ?? 1
    });
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapPost("/api/itcm/email/settings/save", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailSettingsSaveRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "EmailSmtpSave"))
        return Results.BadRequest(new { success = false, message = "Invalid request token." });

    var server = (req.SmtpServer ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(server))
    {
        audit.Record(context, "EmailSmtpSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "SMTP server is required." });
    }
    if (req.SmtpPort is < 1 or > 65535)
    {
        audit.Record(context, "EmailSmtpSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "SMTP port must be between 1 and 65535." });
    }
    if (!IsValidEmailList(req.SmtpUsername, single: true, allowEmpty: true)
        || !IsValidEmailList(req.FromEmail, single: true, allowEmpty: true))
    {
        audit.Record(context, "EmailSmtpSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Username and From Email must be valid email addresses." });
    }

    try
    {
        var existing = await repo.GetGlobalEmailSettingsAsync();
        byte[]? passwordEnc;
        if (string.IsNullOrWhiteSpace(req.Password))
        {
            // Blank retains the saved password (never rendered to the page).
            passwordEnc = existing?.SmtpPasswordEnc;
        }
        else
        {
            passwordEnc = SecretProtector.ProtectString(req.Password);
        }

        await repo.SaveEmailSettingsAsync(new CallEmailSettingsItem
        {
            SmtpServer = server,
            SmtpPort = req.SmtpPort,
            UseSsl = req.UseSsl,
            SmtpUsername = string.IsNullOrWhiteSpace(req.SmtpUsername) ? null : req.SmtpUsername.Trim(),
            SmtpPasswordEnc = passwordEnc,
            FromName = string.IsNullOrWhiteSpace(req.FromName) ? null : req.FromName.Trim(),
            FromEmail = string.IsNullOrWhiteSpace(req.FromEmail) ? null : req.FromEmail.Trim(),
            UpdatedByUserId = GetUserId(context.User)
        });
        audit.Record(context, "EmailSmtpSave", true, $"server={server};port={req.SmtpPort}");
        return Results.Ok(new { success = true, message = "SMTP settings saved. All connected devices use these on their next read." });
    }
    catch (Exception ex)
    {
        audit.Record(context, "EmailSmtpSave", false, "save-failed");
        return Results.Problem($"Could not save SMTP settings: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapPost("/api/itcm/email/rules/save", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    NotificationRulesSaveRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "EmailRulesSave"))
        return Results.BadRequest(new { success = false, message = "Invalid request token." });

    if (req.ReminderDays is < 1 or > 60)
    {
        audit.Record(context, "EmailRulesSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Reminder days must be between 1 and 60." });
    }
    if (!IsValidEmailList(req.GroupEmail, single: false, allowEmpty: true)
        || !IsValidEmailList(req.EscalationEmail, single: false, allowEmpty: true))
    {
        audit.Record(context, "EmailRulesSave", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Group and escalation emails must be valid email addresses." });
    }

    try
    {
        await repo.SaveNotificationRulesAsync(new CallNotificationRulesItem
        {
            NotifyOnNewTicket = req.NotifyOnNewTicket,
            NotifyOnStatusChange = req.NotifyOnStatusChange,
            NotifyOnEscalation = req.NotifyOnEscalation,
            NotifyOnReminder = req.NotifyOnReminder,
            GroupEmail = string.IsNullOrWhiteSpace(req.GroupEmail) ? null : req.GroupEmail.Trim(),
            EscalationEmail = string.IsNullOrWhiteSpace(req.EscalationEmail) ? null : req.EscalationEmail.Trim(),
            ReminderDays = req.ReminderDays,
            UpdatedByUserId = GetUserId(context.User)
        });
        audit.Record(context, "EmailRulesSave", true, $"reminderDays={req.ReminderDays}");
        return Results.Ok(new { success = true, message = "Notification rules saved. All connected devices use these on their next read." });
    }
    catch (Exception ex)
    {
        audit.Record(context, "EmailRulesSave", false, "save-failed");
        return Results.Problem($"Could not save notification rules: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

app.MapPost("/api/itcm/email/test-smtp", async (
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    IItcmRepository repo,
    EmailService emailService,
    SmtpTestRequest req) =>
{
    if (!await ValidateCsrfAsync(context, antiforgery, audit, "EmailSmtpTest"))
        return Results.BadRequest(new { success = false, message = "Invalid request token." });

    var server = (req.SmtpServer ?? string.Empty).Trim();
    var testRecipient = (req.TestRecipient ?? string.Empty).Trim();
    if (string.IsNullOrWhiteSpace(server) || string.IsNullOrWhiteSpace(testRecipient))
    {
        audit.Record(context, "EmailSmtpTest", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "SMTP server and a test recipient are required." });
    }
    if (req.SmtpPort is < 1 or > 65535 || !IsValidEmailList(testRecipient, single: true, allowEmpty: false))
    {
        audit.Record(context, "EmailSmtpTest", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Check the port and test recipient address." });
    }

    try
    {
        byte[]? passwordEnc = string.IsNullOrWhiteSpace(req.Password)
            ? (await repo.GetGlobalEmailSettingsAsync())?.SmtpPasswordEnc
            : SecretProtector.ProtectString(req.Password);

        var sender = new SmtpSenderConfig
        {
            SmtpServer = server,
            SmtpPort = req.SmtpPort,
            UseSsl = req.UseSsl,
            SmtpUsername = string.IsNullOrWhiteSpace(req.SmtpUsername) ? null : req.SmtpUsername.Trim(),
            SmtpPasswordEnc = passwordEnc,
            FromName = string.IsNullOrWhiteSpace(req.FromName) ? null : req.FromName.Trim(),
            FromEmail = string.IsNullOrWhiteSpace(req.FromEmail) ? null : req.FromEmail.Trim()
        };

        await emailService.SendTestEmailAsync(sender, testRecipient);
        audit.Record(context, "EmailSmtpTest", true, $"server={server};to={testRecipient}");
        return Results.Ok(new { success = true, message = $"Test email sent to {testRecipient}." });
    }
    catch (FormatException)
    {
        audit.Record(context, "EmailSmtpTest", false, "validation-error");
        return Results.BadRequest(new { success = false, message = "Check the email addresses and try again." });
    }
    catch (Exception ex)
    {
        audit.Record(context, "EmailSmtpTest", false, "send-failed");
        return Results.Ok(new { success = false, message = $"SMTP test failed: {ex.Message}" });
    }
}).RequireAuthorization(ItcmAuthDefaults.AdministratorPolicy);

static int? GetUserId(ClaimsPrincipal user)
{
    var raw = user.FindFirstValue(ClaimTypes.NameIdentifier);
    return int.TryParse(raw, out var id) ? id : null;
}

static bool IsFinalTicketStatus(string? status)
{
    return string.Equals(status, "Solved", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Resolved (Temporary)", StringComparison.OrdinalIgnoreCase)
        || string.Equals(status, "Closed", StringComparison.OrdinalIgnoreCase);
}

static async Task<int?> ResolveTicketIdAsync(IItcmRepository repo, string ticketIdOrCode)
{
    if (int.TryParse((ticketIdOrCode ?? string.Empty).Trim(), out var id) && id > 0)
        return id;
    return await repo.GetTicketIdByCodeAsync(ticketIdOrCode ?? string.Empty);
}

static bool IsValidEmailList(string? value, bool single, bool allowEmpty)
{
    if (string.IsNullOrWhiteSpace(value))
        return allowEmpty;
    var parts = single
        ? [(value ?? string.Empty).Trim()]
        : (value ?? string.Empty).Split([';', ',', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    if (parts.Length == 0)
        return allowEmpty;
    foreach (var part in parts)
    {
        try
        {
            var address = new System.Net.Mail.MailAddress(part).Address;
            if (!string.Equals(address, part, StringComparison.OrdinalIgnoreCase))
                return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }
    return true;
}

static async Task<bool> ValidateCsrfAsync(
    HttpContext context,
    IAntiforgery antiforgery,
    ItcmAuditService audit,
    string action)
{
    try
    {
        await antiforgery.ValidateRequestAsync(context);
        return true;
    }
    catch (AntiforgeryValidationException)
    {
        audit.Record(context, action, false, "csrf-validation-failed");
        return false;
    }
    catch (Exception ex)
    {
        // Infrastructure failure (e.g. session/token store) surfaces as a
        // 400 with audit detail instead of an unaudited 500.
        audit.Record(context, action, false, $"csrf-validation-error: {ex.Message}");
        return false;
    }
}

app.Run();

record PresenceBeatRequest(string? MachineName, string? UserName, string? Module, string? ClientVersion);

record TicketAssignRequest(int? AssignedToEmpId);

record TicketEscalateRequest(string? Note);

record EmailSettingsSaveRequest(
    string? SmtpServer,
    int SmtpPort,
    bool UseSsl,
    string? SmtpUsername,
    string? Password,
    string? FromName,
    string? FromEmail);

record NotificationRulesSaveRequest(
    bool NotifyOnNewTicket,
    bool NotifyOnStatusChange,
    bool NotifyOnEscalation,
    bool NotifyOnReminder,
    string? GroupEmail,
    string? EscalationEmail,
    int ReminderDays);

record SmtpTestRequest(
    string? SmtpServer,
    int SmtpPort,
    bool UseSsl,
    string? SmtpUsername,
    string? Password,
    string? FromName,
    string? FromEmail,
    string? TestRecipient);

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
