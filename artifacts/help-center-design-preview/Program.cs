using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;
var root = Path.GetFullPath("../../Yakult.SystemsPortal");
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
builder.Configuration.Sources.Clear();
builder.WebHost.UseUrls("http://127.0.0.1:5196");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Yakult.SystemsPortal.Controllers.PublicController).Assembly);
builder.Services.AddSingleton<IDemoModeService, NoDemo>();
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute("default", "{controller=Preview}/{action=Help}/{id?}");
app.Run();
public sealed class PreviewController : Controller {
 public IActionResult Help(bool empty = false, bool other = false) => View("~/Views/Public/ContentList.cshtml", new ContentListViewModel {
 Title = other ? "Company" : "IT Help Center", Description = "Company information and resources.", AvailableTypes = ["Guide", "FAQ"],
 Items = empty ? [] : new List<PortalContentItem> {
 new() { ContentId = 1, Title = "Having trouble signing in?", Summary = "Find the right access path and the information to include when requesting account support.", ContentType = "Guide", CategoryName = "Account support", Slug = "account-support" },
 new() { ContentId = 2, Title = "Before you report a device issue", Summary = "A few simple checks can help you describe the problem and get the right support.", ContentType = "Guide", CategoryName = "Devices", Slug = "device-support" },
 new() { ContentId = 3, Title = "How do I follow up on a ticket?", Summary = "Use your ticket code to see the latest status and updates from IT.", ContentType = "FAQ", CategoryName = "IT support", Slug = "follow-up" }
 }});
}
public sealed class NoDemo : IDemoModeService {
 public bool IsAvailable => false;
 public bool CanUse(HttpContext c) => false;
 public bool IsEnabled(HttpContext c) => false;
 public void SetEnabled(HttpContext c, bool e) { }
}

