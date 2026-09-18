using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.Configure<PortalOptions>(builder.Configuration.GetSection(PortalOptions.SectionName));
builder.Services.AddSingleton<ApplicationInfo>();
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("MediaImport", client => { client.Timeout = TimeSpan.FromMinutes(20); client.DefaultRequestHeaders.UserAgent.ParseAdd("Yakult-SystemsPortal/1.0"); });

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 524_288_000;
});

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 524_288_000;
});

var runtimeStatePath = PortalRuntimeStorage.Resolve(builder.Configuration, builder.Environment);
var dataProtectionKeyPath = Path.Combine(runtimeStatePath, "DataProtection-Keys");
Directory.CreateDirectory(dataProtectionKeyPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeyPath))
    .SetApplicationName("Yakult.SystemsPortal");

// Server-side session is available for authenticated portal workflows.
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Database authentication services
builder.Services.AddSingleton<IProtectedPortalStateStore, ProtectedPortalStateStore>();
builder.Services.AddSingleton<IConnectionStringProvider, ConnectionStringProvider>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPortalCardRepository, PortalCardRepository>();
builder.Services.AddScoped<IPortalContentRepository, PortalContentRepository>();
builder.Services.AddScoped<IEmployeeResourceRepository, EmployeeResourceRepository>();
builder.Services.AddSingleton<IEmployeeResourceCatalog, EmployeeResourceDemoCatalog>();
builder.Services.AddScoped<IDemoModeService, DemoModeService>();
builder.Services.AddScoped<IAccountAdministrationRepository, AccountAdministrationRepository>();
builder.Services.AddScoped<IHelpItRepository, HelpItRepository>();
builder.Services.AddScoped<IConnectionPageAccessGate, ConnectionPageAccessGate>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Home/Login";
        options.LogoutPath = "/Home/Logout";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("ContentEditor", policy => policy.RequireAssertion(context =>
        context.User.IsInRole("ContentEditor") || context.User.HasClaim("IsDeveloper", "true")));
    options.AddPolicy("ContentPublisher", policy => policy.RequireClaim("IsDeveloper", "true"));
    options.AddPolicy("EmployeeResourceEditor", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("IsDeveloper", "true")
        || context.User.IsInRole("ContentEditor")
        || context.User.IsInRole("EmployeeResourceEditor")
        || context.User.IsInRole("EmployeeResourcePublisher")));
    options.AddPolicy("EmployeeResourcePublisher", policy => policy.RequireAssertion(context =>
        context.User.HasClaim("IsDeveloper", "true")
        || context.User.IsInRole("EmployeeResourcePublisher")));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
    app.UseHsts();
}
else
{
    app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/ContentAdmin", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsync("Not Found");
        return;
    }

    await next();
});
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseMiddleware<PortalMaintenanceGateMiddleware>();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Public}/{action=Index}/{id?}");

app.Run();
