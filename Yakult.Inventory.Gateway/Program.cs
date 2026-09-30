using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;
using Yakult.Inventory.Gateway.Endpoints;
using Yakult.Inventory.Gateway.Models;
using Yakult.Inventory.Gateway.Security;
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
builder.Services.AddSingleton<GatewayEnvironments>();
builder.Services.AddScoped<LoginService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<DbSession>();
builder.Services.AddScoped<AccessPolicy>();

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

if (app.Services.GetRequiredService<GatewayEnvironments>().Find(null) is null)
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

// Errors every endpoint can hit, as { error } bodies the desktop shows as-is.
// SQL errors keep SQL's own message (the desktop's ForeignKeyErrorHelper reads
// "REFERENCE constraint" from it) plus the error number.
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (SqlException ex) when (!context.Response.HasStarted)
    {
        app.Logger.LogWarning(ex, "SQL error {Number} on {Path}.", ex.Number, context.Request.Path);
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message, sqlNumber = ex.Number });
    }
    catch (SessionEnvironmentGoneException ex) when (!context.Response.HasStarted)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// ── Health ──────────────────────────────────────────────────────────────────
// databaseReachable is the default environment (install script checks it);
// environments lists every one the switcher can pick.
app.MapGet("/api/health", async (GatewayEnvironments environments, SqlConnectionFactory db, CancellationToken ct) =>
{
    var results = new List<object>();
    var defaultReachable = false;
    foreach (var env in environments.All())
    {
        bool reachable;
        try
        {
            await using var con = await db.OpenAsync(env, ct);
            reachable = true;
        }
        catch
        {
            reachable = false;
        }
        if (env.IsDefault) defaultReachable = reachable;
        results.Add(new { name = env.Name, databaseReachable = reachable });
    }
    return Results.Ok(new { status = "ok", databaseReachable = defaultReachable, environments = results });
});

// ── Database switcher ───────────────────────────────────────────────────────
// What the desktop's Ctrl+Shift+D picker lists. Names and kinds only; the
// database names and connection strings stay on the server.
app.MapGet("/api/environments", (GatewayEnvironments environments) =>
    Results.Ok(environments.All().Select(ToInfo)));

// ── Sign in ─────────────────────────────────────────────────────────────────
app.MapPost("/api/auth/login", async (
    LoginRequest request,
    LoginService login,
    GatewayEnvironments environments,
    IOptionsMonitor<BearerTokenOptions> bearerOptions,
    ILogger<Program> logger,
    HttpContext http,
    CancellationToken ct) =>
{
    var userName = request.UserName?.Trim() ?? "";
    var password = request.Password ?? "";
    if (userName.Length == 0 || password.Length == 0)
        return Results.BadRequest(new { error = "Please enter both name and password." });

    var environment = environments.Find(request.Environment);
    if (environment is null)
        return Results.BadRequest(new { error = $"The database '{request.Environment}' is not available on the sign-in server. Press Ctrl+Shift+D to pick another one." });

    var session = await login.SignInAsync(environment, userName, password, ct);
    if (session is null)
    {
        logger.LogInformation("Failed sign-in for {UserName} to {Environment} from {Ip}.", userName, environment.Name, http.Connection.RemoteIpAddress);
        return Results.Json(new { error = "Invalid name or password." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, session.UserId.ToString()),
        new(ClaimTypes.Name, session.UserName),
        new("dept_account", (session.DepartmentAccount != null).ToString()),
        // Locks the session to one database: the bridge and future endpoints read it from here.
        new(EnvClaim, environment.Name),
        // Read by AccessPolicy for server-side permission checks.
        new(AccessPolicy.DeveloperClaim, session.IsDeveloper ? "true" : "false"),
        new(AccessPolicy.SuperAdminClaim, session.IsSuperAdmin ? "true" : "false")
    };
    claims.AddRange(session.Roles.Select(r => new Claim(ClaimTypes.Role, r)));
    var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, BearerTokenDefaults.AuthenticationScheme));

    var (token, expiresAt) = IssueToken(principal, bearerOptions);
    logger.LogInformation("User {UserId} signed in to {Environment} from {Ip}.", session.UserId, environment.Name, http.Connection.RemoteIpAddress);
    return Results.Ok(new LoginResponse
    {
        AccessToken = token,
        ExpiresAt = expiresAt,
        Environment = ToInfo(environment),
        Session = session
    });
}).RequireRateLimiting("login");

// Swaps a still-valid token for a fresh one so an open app never expires mid-work.
// Refuses when the account was deactivated since sign-in.
app.MapPost("/api/auth/refresh", async (ClaimsPrincipal user, DbSession db, IOptionsMonitor<BearerTokenOptions> bearerOptions, CancellationToken ct) =>
{
    await using (var con = await db.OpenAsync(ct))
    await using (var cmd = new SqlCommand("SELECT CASE WHEN ISNULL(IsActive, 1) = 1 THEN 1 ELSE 0 END FROM dbo.[User] WHERE UserId = @UserId", con))
    {
        cmd.Parameters.AddWithValue("@UserId", db.UserId);
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync(ct) ?? 0) != 1)
            return Results.Json(new { error = "Your account is no longer active." }, statusCode: StatusCodes.Status401Unauthorized);
    }

    var principal = new ClaimsPrincipal(new ClaimsIdentity(user.Claims, BearerTokenDefaults.AuthenticationScheme));
    var (token, expiresAt) = IssueToken(principal, bearerOptions);
    return Results.Ok(new { accessToken = token, expiresAt });
}).RequireAuthorization();

app.MapGet("/api/auth/me", (ClaimsPrincipal user) => Results.Ok(new
{
    userId = user.FindFirstValue(ClaimTypes.NameIdentifier),
    userName = user.Identity?.Name,
    environment = user.FindFirstValue(EnvClaim),
    roles = user.FindAll(ClaimTypes.Role).Select(c => c.Value)
})).RequireAuthorization();

// ── Migration bridge ────────────────────────────────────────────────────────
// Screens not yet moved to gateway endpoints still query SQL directly. Until
// they are, a signed-in desktop gets the (restricted) client connection string
// here instead of from a file shipped with the app. Turn this off with
// Gateway:LegacyClientConnection:Enabled=false once every module is migrated.
app.MapGet("/api/session/db-connection", (IConfiguration config, GatewayEnvironments environments, ClaimsPrincipal user, ILogger<Program> logger, HttpContext http) =>
{
    if (!config.GetValue("Gateway:LegacyClientConnection:Enabled", false))
        return Results.NotFound();

    // The database the user signed in to, never one the client asks for.
    var environment = environments.Find(user.FindFirstValue(EnvClaim));
    if (environment is null)
        return Results.Json(new { error = "That database was removed from the sign-in server. Please sign in again." }, statusCode: StatusCodes.Status401Unauthorized);

    if (string.IsNullOrWhiteSpace(environment.ClientConnection))
        return Results.Problem($"LegacyClient is not configured for '{environment.Name}' on the gateway.", statusCode: StatusCodes.Status503ServiceUnavailable);

    logger.LogInformation("Issued {Environment} client connection to user {UserId} at {Ip}.",
        environment.Name, user.FindFirstValue(ClaimTypes.NameIdentifier), http.Connection.RemoteIpAddress);
    return Results.Ok(new { connectionString = environment.ClientConnection });
}).RequireAuthorization();

app.MapOrganizationEndpoints();
app.MapCatalogEndpoints();
app.MapAdminSettingsEndpoints();

app.Run();

// Issues a token the same way the bearer handler's SignIn does, so it can go back
// in one response together with the session.
static (string Token, DateTimeOffset ExpiresAt) IssueToken(ClaimsPrincipal principal, IOptionsMonitor<BearerTokenOptions> bearerOptions)
{
    var options = bearerOptions.Get(BearerTokenDefaults.AuthenticationScheme);
    var now = DateTimeOffset.UtcNow;
    var properties = new AuthenticationProperties
    {
        IssuedUtc = now,
        ExpiresUtc = now + options.BearerTokenExpiration
    };
    var ticket = new AuthenticationTicket(principal, properties, $"{BearerTokenDefaults.AuthenticationScheme}:AccessToken");
    return (options.BearerTokenProtector.Protect(ticket), properties.ExpiresUtc.Value);
}

static EnvironmentInfo ToInfo(GatewayEnvironment env) => new()
{
    Name = env.Name,
    DisplayName = env.DisplayName,
    Kind = env.Kind,
    IsDefault = env.IsDefault
};

public partial class Program
{
    public const string EnvClaim = "env";
}
