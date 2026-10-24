using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Services;

namespace LearningChecks;

public static class Program
{
    public static void Main(string[] args)
    {
        Checks.Run();
        DemoChecks.Run().GetAwaiter().GetResult();
        ItInformationChecks.Run().GetAwaiter().GetResult();
        if (!args.Contains("--serve")) return;
        // This host never runs the portal's Program and registers no repositories or database configuration.
        var workspace = new DirectoryInfo(AppContext.BaseDirectory);
        while (workspace != null && !Directory.Exists(Path.Combine(workspace.FullName, "Yakult.SystemsPortal"))) workspace = workspace.Parent;
        if (workspace == null) throw new InvalidOperationException("Run these checks from the repository checkout.");
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = workspace.FullName, WebRootPath = Path.Combine(workspace.FullName, "Yakult.SystemsPortal", "wwwroot") });
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls("http://127.0.0.1:5187");
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(Yakult.SystemsPortal.Controllers.PublicController).Assembly)
            .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new FixtureControllersOnly()));
        builder.Services.AddSingleton<IDemoModeService, NoDemo>();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy("ContentEditor", policy => policy.RequireAssertion(_ => true));
            options.AddPolicy("EmployeeResourceEditor", policy => policy.RequireAssertion(_ => true));
        });
        var app = builder.Build();
        app.UseStaticFiles();
        app.Use(async (context, next) =>
        {
            context.User = context.Request.Cookies["learning-fixture-anonymous"] == "true"
                ? new ClaimsPrincipal(new ClaimsIdentity())
                : new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "fixture-user"), new Claim(ClaimTypes.Name, "Design Review"), new Claim(ClaimTypes.Role, "PortalAdmin")], "fixture"));
            await next();
        });
        app.MapControllerRoute("default", "{controller=Public}/{action=Learning}/{id?}");
        app.Run();
    }
}

public sealed class FixtureControllersOnly : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(x => x.Namespace != "LearningChecks").ToArray()) feature.Controllers.Remove(controller);
    }
}

public sealed class NoDemo : IDemoModeService
{
    public bool IsAvailable => false;
    public bool CanUse(HttpContext context) => false;
    public bool IsEnabled(HttpContext context) => context.Request.Cookies["learning-fixture-demo"] == "true";
    public void SetEnabled(HttpContext context, bool enabled) { }
}

public static class Fixtures
{
    public static LearningCourseDocument Document() => new()
    {
        Audience = "All employees", Level = "Foundation", Objectives = ["Recognize suspicious requests.", "Report concerns through an official channel."],
        Modules = [new() { Title = "Recognize the signs", Lessons = [new() { Id = "spot-phishing", Title = "Spot a suspicious email", Minutes = 6,
            Blocks = [new() { Type = "text", Title = "A moment to check", Text = "An unexpected email can look convincing. **Pause before you click.**\n\nCheck the sender, the request, and the link." }, new() { Type = "comparison", Title = "Compare the request", Text = "## Trusted request\nThe sender uses an official channel and a familiar process.", Extra = "## Warning signs\nAn unfamiliar address asks you to act urgently." }, new() { Type = "callout", Title = "When in doubt, verify", Text = "Contact the sender using a phone number or address you already trust." }] },
            new() { Id = "practice", Title = "Make the safe choice", Minutes = 3, Blocks = [new() { Type = "quiz", Title = "A message asks for your password. What should you do?", Options = ["Reply with the password", "Verify and report through an official channel"], Answer = 1, Text = "Never share your password. Verify the request using an official channel." }] }] },
            new() { Title = "Put it into practice", Lessons = [new() { Id = "report", Title = "Report and follow up", Minutes = 4, Blocks = [new() { Type = "steps", Title = "Your reporting checklist", Text = "Keep the suspicious message.\nReport it to IT using an official channel.\nFollow the instructions you receive." }, new() { Type = "accordion", Title = "What if I already clicked?", Text = "Contact IT promptly and explain what happened." }, new() { Type = "download", Title = "Employee portal", Text = "Open the reference portal.", Url = "https://example.com/reference" }] }] }]
    };
    public static PortalContentItem Course = new() { ContentId = 9001, ContentType = "Course", Title = "Cybersecurity essentials", Slug = "cybersecurity-essentials", Summary = "Simple habits that protect your accounts, your information, and your working day.", Body = LearningCourseDocument.Prefix + JsonSerializer.Serialize(Document(), new JsonSerializerOptions(JsonSerializerDefaults.Web)), CategoryName = "IT Cybersecurity", CategorySlug = "cybersecurity", Status = "Draft", UpdatedAtUtc = new DateTime(2026, 10, 23) };
    public static PortalContentItem Legacy = new() { ContentId = 9002, ContentType = "Guide", Title = "Getting started with system access", Slug = "system-access", Summary = "Find your application and request the access you need.", Body = "## Before you start\nUse the official application link.\n\n## Request access\nContact IT with the details of your request.", CategoryName = "IT Technical", CategorySlug = "learning", Status = "Draft" };
    public static readonly PortalContentCategory[] Categories = [new() { CategoryId = 1, Name = "IT Cybersecurity", Slug = "cybersecurity" }, new() { CategoryId = 2, Name = "IT Technical", Slug = "learning" }];
    public static PortalContentItem ManagedLegacy = Legacy;
}

public sealed class PublicController : Controller
{
    public async Task<IActionResult> Learning(string? q = null, string? type = null, int page = 1, string sort = "recommended", string? track = null, bool unavailable = false)
    {
        var demo = HttpContext.RequestServices.GetRequiredService<IDemoModeService>();
        if (demo.IsEnabled(HttpContext)) return await DemoChecks.Public(HttpContext, demo, HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).Learning(type, track, q, sort, page);
        var all = Enumerable.Range(1, 29).Select(index => index == 1 ? Fixtures.Course : new PortalContentItem { ContentId = 9100 + index, ContentType = index % 2 == 0 ? "Guide" : "Video", Slug = "resource-" + index, Title = index % 2 == 0 ? "Practical system guide " + index : "Working safely online " + index, Summary = "Learn useful skills for everyday tasks, with clear examples and guidance.", CategoryName = "IT Technical", CategorySlug = "learning", Body = "A short resource for your working day." }).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q)) all = all.Where(x => x.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type)) all = all.Where(x => x.ContentType == type);
        if (track == "cybersecurity") all = all.Where(x => x.CategorySlug == "cybersecurity");
        if (sort == "title") all = all.OrderBy(x => x.Title);
        var filtered = all.ToList();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(filtered.Count / 12.0)));
        return View("/Views/Public/Learning.cshtml", new ContentListViewModel { Description = "Build practical skills for your working day. Explore courses, short lessons, and guides at your own pace.", Items = filtered.Skip((page - 1) * 12).Take(12).ToList(), TotalCount = filtered.Count, Page = page, Query = q, SelectedType = type, SelectedTrack = track, Sort = sort, CatalogUnavailable = unavailable, AvailableTypes = ["Course", "Guide", "Video"], Tracks = [new() { Value = "cybersecurity", Label = "IT Cybersecurity" }, new() { Value = "technical", Label = "IT Technical" }, new() { Value = "support", Label = "IT Support" }] });
    }
    [Route("/content/{slug}")]
    public async Task<IActionResult> ContentDetails(string slug)
    {
        if (LearningDemoSamples.IsSampleSlug(slug) || PublicItInformationSamples.IsSampleSlug(slug)) return await DemoChecks.Public(HttpContext, HttpContext.RequestServices.GetRequiredService<IDemoModeService>(), HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).ContentDetails(slug);
        return View(slug == "system-access" ? "/Views/Public/Content.cshtml" : "/Views/Public/Course.cshtml", new PortalContentDetailsViewModel { Item = slug == "system-access" ? Fixtures.Legacy : Fixtures.Course });
    }
    [HttpGet("/media/learning/demo/{file}")]
    public IActionResult DemoAsset(string file) => DemoChecks.Public(HttpContext, HttpContext.RequestServices.GetRequiredService<IDemoModeService>(), HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).LearningDemoAsset(file);
    [HttpGet("/media/learning/demo/policy/{file}")]
    public IActionResult PolicyDemoAsset(string file) => DemoChecks.Public(HttpContext, HttpContext.RequestServices.GetRequiredService<IDemoModeService>(), HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).PolicyDemoAsset(file);
    public IActionResult MyLearning() => View("/Views/Public/MyLearning.cshtml", new MyLearningViewModel { IsDemoData = HttpContext.RequestServices.GetRequiredService<IDemoModeService>().IsEnabled(HttpContext) });
    public Task<IActionResult> Search(string? q) => DemoChecks.Public(HttpContext, HttpContext.RequestServices.GetRequiredService<IDemoModeService>(), HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).Search(q);
}

[Route("/Admin/LearningContent")]
public sealed class ContentAdminController : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(bool legacy = false)
    {
        var demo = HttpContext.RequestServices.GetRequiredService<IDemoModeService>();
        if (demo.IsEnabled(HttpContext)) return await DemoChecks.Admin(HttpContext, demo, HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).Index();
        return View("/Views/ContentAdmin/Index.cshtml", new ContentEditorViewModel { Items = legacy ? [Fixtures.ManagedLegacy] : [Fixtures.Course, Fixtures.ManagedLegacy], Categories = Fixtures.Categories, CanPublish = false });
    }
    [HttpPost("Save")]
    public async Task<IActionResult> Save([FromBody] SavePortalContentRequest request)
    {
        var demo = HttpContext.RequestServices.GetRequiredService<IDemoModeService>();
        if (demo.IsEnabled(HttpContext)) return await DemoChecks.Admin(HttpContext, demo, HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).Save(request);
        if (request.Body.StartsWith("yakult-course:") && LearningCourseDocument.Parse(request.Body) == null) return BadRequest(new { success = false, message = "Invalid course" });
        var saved = new PortalContentItem { ContentId = request.ContentId ?? 9001, ContentType = request.ContentType, Slug = request.Slug, Title = request.Title, Summary = request.Summary, Body = request.Body, CategoryName = "IT Cybersecurity", CategorySlug = "cybersecurity", Status = "Draft" };
        if (saved.ContentId == 9002) Fixtures.ManagedLegacy = saved; else Fixtures.Course = saved;
        return Json(new { success = true, item = saved });
    }
    [HttpPost("Upload")]
    public async Task<IActionResult> Upload()
    {
        var demo = HttpContext.RequestServices.GetRequiredService<IDemoModeService>();
        if (demo.IsEnabled(HttpContext)) return await DemoChecks.Admin(HttpContext, demo, HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>()).Upload(null);
        return Json(new { success = true, url = "/media/learning/fixtures/uploaded.png" });
    }
}

public static class Checks
{
    public static void Run()
    {
        var count = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; }
        var encoded = Fixtures.Course.Body;
        Check(LearningCourseDocument.Parse(encoded)?.Lessons.Count() == 3, "Document round trip");
        Check(LearningCourseDocument.Parse(Fixtures.Legacy.Body) == null, "Legacy Markdown preserved");
        Check(LearningCourseDocument.Parse(LearningCourseDocument.Prefix + "{") == null, "Malformed JSON rejected");
        Check(LearningCourseDocument.Parse(LearningCourseDocument.Prefix + "{\"version\":1,\"modules\":[null]}") == null, "Null module rejected");
        var doc = Fixtures.Document(); doc.Modules[1].Lessons[0].Id = doc.Modules[0].Lessons[0].Id;
        Check(LearningCourseDocument.Parse(LearningCourseDocument.Prefix + JsonSerializer.Serialize(doc, new JsonSerializerOptions(JsonSerializerDefaults.Web))) == null, "Duplicate identifiers rejected");
        foreach (var url in new[] { "javascript:alert(1)", "//example.com/test", "/media/../secret", "/media/%2e%2e/secret", "https://user:password@example.com/file", "https://example.com\\evil", "data:text/html,test" }) Check(!LearningCourseDocument.IsSafeUrl(url), "Unsafe URL rejected: " + url);
        Check(LearningCourseDocument.IsSafeUrl("/media/learning/videos/test.mp4"), "Media path accepted");
        Check(LearningCourseDocument.IsSafeUrl("https://example.com/video.mp4"), "HTTPS accepted");
        Check(LearningCourseDocument.YouTubeEmbed("https://www.youtube.com/watch?v=dQw4w9WgXcQ") == "https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ", "YouTube allowlist");
        Check(LearningCourseDocument.YouTubeEmbed("https://youtube.com.evil.example/watch?v=dQw4w9WgXcQ") == null, "YouTube lookalike rejected");
        Check(LearningCourseDocument.BrowserKey(Fixtures.Course, "one") != LearningCourseDocument.BrowserKey(Fixtures.Course, "two"), "User progress isolated");
        doc = Fixtures.Document(); doc.Modules[0].Lessons[1].Blocks[0].Answer = 9;
        Check(LearningCourseDocument.Parse(LearningCourseDocument.Prefix + JsonSerializer.Serialize(doc, new JsonSerializerOptions(JsonSerializerDefaults.Web))) == null, "Invalid quiz answer rejected");
        Console.WriteLine($"PASS: {count} course document checks (no database).");
    }
}
