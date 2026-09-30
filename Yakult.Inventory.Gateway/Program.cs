using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Services;

// Yakult Inventory Gateway: the desktop app signs in here instead of opening SQL
// itself. The database connection string lives only on this server (in
// appsettings.Local.json beside the exe, gitignored), the same way
// Yakult.ITCM.Server keeps its own.

var builder = WebApplication.CreateBuilder(args);

var localConfigPath = Path.Combine(AppContext.BaseDirectory, "appsettings.Local.json");
builder.Configuration.AddJsonFile(localConfigPath, optional: true, reloadOnChange: true);

var gateway = builder.Configuration.GetSection("Gateway");
var requireHttps = gateway.GetValue("RequireHttps", false);

// Keep token keys outside the publish folder so a redeploy does not sign everyone out.
var keyPath = gateway["DataProtectionKeysPath"];
if (string.IsNullOrWhiteSpace(keyPath))
{
    keyPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "Yakult", "Gateway", "DataProtection-Keys");
}
Directory.CreateDirectory(keyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keyPath))
    .SetApplicationName("Yakult.Inventory.Gateway");

builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddScoped<LoginService>();

builder.Services
    .AddAuthentication(BearerTokenDefaults.AuthenticationScheme)
    .AddBearerToken(options =>
    {
        options.BearerTokenExpiration = TimeSpan.FromHours(gateway.GetValue("TokenLifetimeHours", 12));
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = gateway.GetValue("LoginAttemptsPerMinute", 10),
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        }));
});

var app = builder.Build();

if (!requireHttps)
    app.Logger.LogWarning("Gateway is accepting HTTP. Passwords and tokens cross the network unencrypted; bind an HTTPS certificate and set Gateway:RequireHttps before production use.");

if (!app.Services.GetRequiredService<SqlConnectionFactory>().IsConfigured)
    app.Logger.LogError("ConnectionStrings:Yakult_Inventory_System is empty. Put it in {Path}.", localConfigPath);

if (requireHttps)
{
    app.Use(async (context, next) =>
    {
        if (!context.Request.IsHttps)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            await context.Response.WriteAsync("HTTPS required.");
            return;
        }
        await next();
    });
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Health ──────────────────────────────────────────────────────────────────
app.MapGet("/api/health", async (SqlConnectionFactory db, CancellationToken ct) =>
{
    bool databaseReachable;
    try
    {
        await using var con = await db.OpenAsync(ct);
        databaseReachable = true;
    }
    catch
    {
        databaseReachable = false;
    }
    return Results.Ok(new { status = "ok", databaseReachable });
});

// ── Sign in ─────────────────────────────────────────────────────────────────
app.MapPost("/api/auth/login", async (
    LoginRequest request,
    LoginService login,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    ILogger<Program> logger,
    HttpContext http,
    CancellationToken ct) =>
{
    var userName = request.UserName?.Trim() ?? "";
    var password = request.Password ?? "";
    if (userName.Length == 0 || password.Length == 0)
        return Results.BadRequest(new { error = "Please enter both name and password." });

    var session = await login.SignInAsync(userName, password, ct);
    if (session is null)
    {
        logger.LogInformation("Failed sign-in for {UserName} from {Ip}.", userName, http.Connection.RemoteIpAddress);
        return Results.Json(new { error = "Invalid name or password." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
        new(ClaimTypes.Name, session.UserName),
        new("dept_account", (session.DepartmentAccount != null).ToString())
    };
    claims.AddRange(session.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme));

    // Issue the token the same way the bearer handler's SignIn does, so it can go
    // back in one response together with the session.
    var options = bearerOptions.Get(BearerTokenDefaults.AuthenticationScheme);
    var now = DateTimeOffset.UtcNow;
    var properties = new AuthenticationProperties
    {
        IssuedUtc = now,
        ExpiresUtc = now + options.BearerTokenExpiration
    };
    var ticket = new AuthenticationTicket(principal, properties, $"{BearerTokenDefaults.AuthenticationScheme}:AccessToken");

    logger.LogInformation("User {UserId} signed in from {Ip}.", session.UserId, http.Connection.RemoteIpAddress);
    return Results.Ok(new LoginResponse
    {
        AccessToken = options.BearerTokenProtector.Protect(ticket),
        ExpiresAt = properties.ExpiresUtc.Value,
        Session = session
    });
}).RequireRateLimiting("login");

app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new
{
    userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
    userName = user.Identity?.Name,
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
})).RequireAuthorization();

// ── Migration bridge ────────────────────────────────────────────────────────
// Screens not yet moved to gateway endpoints still query SQL directly. Until
// they are, a signed-in desktop gets the (restricted) client connection string
// here instead of from a file shipped with the app. Turn this off with
// Gateway:LegacyClientConnection:Enabled=false once every module is migrated.
app.MapGet("/api/session/db-connection", (IConfiguration config, ClaimsPrincipal user, ILogger<Program> logger, HttpContext http) =>
{
    if (!config.GetValue("Gateway:LegacyClientConnection:Enabled", false))
        return Results.NotFound();

    var cs = config.GetConnectionString("LegacyClient");
    if (string.IsNullOrWhiteSpace(cs))
        return Results.Problem("ConnectionStrings:LegacyClient is not configured on the gateway.", statusCode: StatusCodes.Status503ServiceUnavailable);

    logger.LogInformation("Issued client connection to user {UserId} at {Ip}.",
        user.FindFirstValue(ClaimTypes.NameIdentifier), http.Connection.RemoteIpAddress);
    return Results.Ok(new { connectionString = cs });
}).RequireAuthorization();

app.Run();
