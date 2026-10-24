using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace LearningChecks;

public class NoDatabaseRepository : DispatchProxy
{
    public int Calls { get; private set; }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        Calls++;
        throw new InvalidOperationException("Demo checks must never call a database repository: " + method?.Name);
    }
}

public sealed class DemoFlag : IDemoModeService
{
    public bool Enabled { get; set; } = true;
    public bool IsAvailable => true;
    public bool CanUse(HttpContext context) => true;
    public bool IsEnabled(HttpContext context) => Enabled;
    public void SetEnabled(HttpContext context, bool enabled) => Enabled = enabled;
}

public static class DemoChecks
{
    public static Yakult.SystemsPortal.Controllers.PublicController Public(HttpContext context, IDemoModeService demo, IWebHostEnvironment environment, IPortalContentRepository? repository = null) => new(repository ?? DispatchProxy.Create<IPortalContentRepository, NoDatabaseRepository>(), null!, null!, null!, null!, demo, Options.Create(new PortalOptions()), NullLogger<Yakult.SystemsPortal.Controllers.PublicController>.Instance, environment) { ControllerContext = new ControllerContext { HttpContext = context } };
    public static Yakult.SystemsPortal.Controllers.ContentAdminController Admin(HttpContext context, IDemoModeService demo, IWebHostEnvironment environment, IPortalContentRepository? repository = null) => new(repository ?? DispatchProxy.Create<IPortalContentRepository, NoDatabaseRepository>(), environment, null!, new ConfigurationBuilder().Build(), demo) { ControllerContext = new ControllerContext { HttpContext = context } };

    public static async Task Run()
    {
        var count = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); count++; }
        Check(LearningDemoSamples.Items.Count == 15, "Fifteen sample courses");
        Check(LearningDemoSamples.Items.All(item => item.IsDemo && item.Slug.StartsWith(LearningDemoSamples.SlugPrefix) && LearningCourseDocument.Parse(item.Body) != null), "Every sample is a valid structured course");
        var blocks = LearningDemoSamples.Items.SelectMany(item => LearningCourseDocument.Parse(item.Body)!.Lessons).SelectMany(lesson => lesson.Blocks).Select(block => block.Type).Distinct().ToList();
        Check(blocks.Count == 9, "All nine content block types represented");
        var categories = LearningDemoSamples.Categories.Select(category => category.Slug).ToList();
        Check(LearningDemoSamples.Catalog(categories, null, null, "recommended", 1).Items.Count == 12, "Demo first page");
        Check(LearningDemoSamples.Catalog(categories, null, null, "recommended", 2).Items.Count == 3, "Demo second page");
        Check(LearningDemoSamples.Catalog(["it-help"], null, null, "recommended", 1).TotalCount == 5, "Demo topic filter");
        Check(LearningDemoSamples.Catalog(categories, "phishing", null, "title", 1).TotalCount == 1, "Demo search");
        Check(LearningCourseDocument.BrowserScope("1", true) != LearningCourseDocument.BrowserScope("1"), "Demo progress scope separated");
        var repository = DispatchProxy.Create<IPortalContentRepository, NoDatabaseRepository>();
        var guard = (NoDatabaseRepository)(object)repository;
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Name, "Demo review")], "fixture")) };
        var demo = new DemoFlag();
        var environment = new DemoEnvironment();
        var controller = Public(context, demo, environment, repository);
        var library = (ViewResult)await controller.Learning();
        Check(library.Model is ItInformationIndexViewModel { Resources.Count: 2 }, "Real learning controller uses the two employee references");
        var details = (ViewResult)await controller.ContentDetails(LearningDemoSamples.Items[0].Slug);
        Check(details.ViewName == "Course" && details.Model is PortalContentDetailsViewModel { Item.IsDemo: true }, "Real details controller resolves demo course");
        Check(((ViewResult)await controller.MyLearning()).Model is MyLearningViewModel { IsDemoData: true }, "Demo My Learning avoids account records");
        Check(await controller.Progress(new SaveCourseProgressRequest { ContentId = -3001 }) is BadRequestObjectResult, "Demo cannot write official progress");
        var admin = Admin(context, demo, environment, repository);
        Check(((ViewResult)await admin.Index()).Model is ContentEditorViewModel { IsDemoData: true, CanPublish: false }, "Real editor loads preview courses");
        Check(await admin.Save(new SavePortalContentRequest { ContentId = -3001 }) is BadRequestObjectResult, "Demo save blocked");
        Check(await admin.Upload(null) is BadRequestObjectResult, "Demo upload blocked");
        Check(await admin.ImportUrl(new ImportUrlRequest()) is BadRequestObjectResult, "Demo import blocked");
        Check(await admin.Publish(new ContentWorkflowRequest { ContentId = -3001 }) is BadRequestObjectResult, "Demo publish blocked");
        Check(await admin.Archive(new ContentWorkflowRequest { ContentId = -3001 }) is BadRequestObjectResult, "Demo archive blocked");
        Check(await admin.Restore(new ContentWorkflowRequest { ContentId = -3001 }) is BadRequestObjectResult, "Demo restore blocked");
        demo.Enabled = false;
        Check(await controller.ContentDetails(LearningDemoSamples.Items[0].Slug) is NotFoundResult, "Demo courses unavailable after exiting demo view");
        Check(controller.LearningDemoAsset("lesson-preview.webm") is NotFoundResult, "Demo asset route gated");
        Check(await admin.Save(new SavePortalContentRequest { ContentId = -3001 }) is BadRequestObjectResult, "Stale demo form cannot save after mode exit");
        Check(await admin.Save(new SavePortalContentRequest { Slug = LearningDemoSamples.Items[0].Slug }) is BadRequestObjectResult, "Sample slug cannot become a managed record");
        Check(guard.Calls == 0, "No repository calls in demo paths");
        Console.WriteLine($"PASS: {count} demo checks, including real controller routes and zero database repository calls.");
    }
}

public sealed class DemoEnvironment : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "LearningChecks";
    public string EnvironmentName { get; set; } = "Development";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
}
