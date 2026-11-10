using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Controllers;
using Yakult.SystemsPortal.Models;

// Exercise the real action and compiled Razor view without production configuration or a database.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Yakult.SystemsPortal"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = root,
    WebRootPath = Path.Combine(root, "wwwroot")
});
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection();
builder.WebHost.UseUrls("http://127.0.0.1:5192");
builder.Logging.ClearProviders();
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HomeController).Assembly);
builder.Services.AddSingleton<IControllerActivator, ErrorControllerActivator>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseExceptionHandler("/Home/Error");
app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/fixture/throw", () => ThrowForCheck());
app.MapMethods("/fixture/status/{code:int}", new[] { "GET", "POST" }, (int code) => Results.StatusCode(code));
app.MapControllerRoute("default", "{controller=Public}/{action=Index}/{id?}");
app.Run();

static IResult ThrowForCheck() => throw new InvalidOperationException("Private exception detail must never appear in the error page.");

public sealed class ErrorControllerActivator : IControllerActivator
{
    public object Create(ControllerContext context) => new HomeController(
        Options.Create(new PortalOptions()), null!, null!, null!, null!, null!,
        Microsoft.Extensions.Logging.Abstractions.NullLogger<HomeController>.Instance,
        new ConfigurationBuilder().Build());

    public void Release(ControllerContext context, object controller) { }
}
