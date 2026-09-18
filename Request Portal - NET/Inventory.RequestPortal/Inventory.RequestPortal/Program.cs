using Inventory.RequestPortal.Repositories;
using Inventory.RequestPortal.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Data.SqlClient;
using Serilog;

// Bootstrap logger — active only until the host builds its real Serilog pipeline below,
// so a failure during configuration/DI setup itself still gets written somewhere instead
// of vanishing (previously the only sink was the default console/EventLog ILogger, which
// meant production diagnostics depended entirely on IIS still being able to surface them).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);

// Structured logging to console + a rolling daily file under Logs/, so production
// diagnostics don't depend solely on IIS/Windows Event Log defaults. Reads Logging:LogLevel
// from appsettings.json so existing level configuration still applies.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        Path.Combine(AppContext.BaseDirectory, "Logs", "log-.txt"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}"));

// Local, gitignored overrides (per-machine connection strings, secrets) — layered on
// top of appsettings.json/appsettings.{Environment}.json, mirrors the desktop app's
// appsettings.Local.json convention. Optional so a machine without one still builds.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// Trust forwarded headers from IIS reverse proxy so HttpContext.Request.Host
// reflects the real server IP/port rather than the internal Kestrel address.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;
    // Clear network/proxy restrictions so IIS on the same machine is trusted.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Add services to the container.
builder.Services.AddControllersWithViews();

// Add HttpContextAccessor for session access in services
builder.Services.AddHttpContextAccessor();

// CORS — allow requests from the server IP; origins are read from config so
// the value can be updated in appsettings.json without a code change.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { builder.Configuration["BaseUrl"] ?? "http://192.168.100.186:8080" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("ServerPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Configure anti-forgery to work over plain HTTP from any device on the network.
// Default SameSite=Strict blocks cross-device form submissions over HTTP.
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.None; // allow over HTTP (no HTTPS)
    options.Cookie.HttpOnly = true;
});

// ============================================
// Session Configuration
// TRANSLATED FROM: WinForms AppSession static class
// ============================================
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.Name = ".RequestPortal.Session";
});

// Register services and repositories for dependency injection
// TRANSLATED FROM: WinForms services instantiated in constructors
builder.Services.AddScoped<IConnectionStringProvider, ConnectionStringProvider>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IRequestRepository, RequestRepository>();
builder.Services.AddScoped<IRequestFulfillmentRepository, RequestFulfillmentRepository>();
builder.Services.AddScoped<ICartridgeFulfillmentRepository, CartridgeFulfillmentRepository>();
builder.Services.AddScoped<ICartridgeExchangeRepository, CartridgeExchangeRepository>();
builder.Services.AddScoped<ICartridgeMasterDataRepository, CartridgeMasterDataRepository>();
builder.Services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
builder.Services.AddScoped<IRequesterPortalService, RequesterPortalService>();
builder.Services.AddScoped<ICartridgeApprovalWebRepository, CartridgeApprovalWebRepository>();
builder.Services.AddScoped<ICartridgeAuthorizationWebRepository, CartridgeAuthorizationWebRepository>();

builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IUserNotificationSettingRepository, UserNotificationSettingRepository>();

builder.Services.AddScoped<IDevToolsService, DevToolsService>();

builder.Services.AddScoped<ITourRepository, TourRepository>();

var app = builder.Build();

// ============================================
// DIAGNOSTIC: Test SQL Server connection at startup
// Remove this block after confirming connection works
// ============================================
if (app.Environment.IsDevelopment())
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    Console.WriteLine("========================================");
    Console.WriteLine("SQL CONNECTION DIAGNOSTIC");
    Console.WriteLine("========================================");
    Console.WriteLine($"Connection String (masked): {MaskConnectionString(connectionString)}");

    try
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        Console.WriteLine("SUCCESS: Connected to SQL Server!");
        Console.WriteLine($"Server Version: {connection.ServerVersion}");
        Console.WriteLine($"Database: {connection.Database}");
        connection.Close();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAILED: {ex.Message}");
    }
    Console.WriteLine("========================================");
}

// Helper to mask password in connection string for logging
static string MaskConnectionString(string? cs)
{
    if (string.IsNullOrEmpty(cs)) return "(empty)";
    return System.Text.RegularExpressions.Regex.Replace(
        cs,
        @"(Password|Pwd)\s*=\s*[^;]+",
        "$1=*****",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
// ============================================

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

// One structured log line per HTTP request (method, path, status, duration) —
// separate from the per-action LogError/LogWarning calls controllers already make.
app.UseSerilogRequestLogging();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();

app.UseRouting();

app.UseCors("ServerPolicy");

// Enable session middleware (must be before UseAuthorization)
app.UseSession();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

try
{
    app.Run();
}
finally
{
    // Flushes any buffered file/console sink writes before the process exits.
    Log.CloseAndFlush();
}
