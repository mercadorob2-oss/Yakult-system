using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;

namespace LearningChecks;

public static class ItInformationChecks
{
    public static async Task Run()
    {
        var count = 0;
        void Check(bool condition, string label) { if (!condition) throw new Exception(label); count++; }
        var repository = DispatchProxy.Create<IPortalContentRepository, NoDatabaseRepository>();
        var guard = (NoDatabaseRepository)(object)repository;
        var demo = new DemoFlag();
        var controller = DemoChecks.Public(new DefaultHttpContext(), demo, new DemoEnvironment(), repository);
        var index = (ViewResult)await controller.Learning();
        Check(index.ViewName == "ItInformation" && index.Model is ItInformationIndexViewModel { Resources.Count: 2 }, "Public demo contains two references");
        foreach (var resource in PublicItInformationSamples.Resources)
        {
            var result = await controller.ContentDetails(resource.Slug);
            Check(result is ViewResult { ViewName: "ItInformationDetails", Model: ItInformationResource }, "Anonymous readers can open a resource without course sign-in or progress");
            Check(resource.Item is { ContentType: "Article", IsDemo: true, MediaUrl: null, Links.Count: 0 }, "Reference is an article with no original attachment");
        }
        var search = (ViewResult)await controller.Learning(q: "Comptroller");
        Check(search.Model is ItInformationIndexViewModel { Resources.Count: 1 }, "Search includes approval instructions within sections");
        Check(((ItInformationIndexViewModel)((ViewResult)await controller.Learning(q: "ERP")).Model!).Resources.Single().ContentId == -3102, "Search finds system cards");
        Check(((ItInformationIndexViewModel)((ViewResult)await controller.Learning(q: "unmatched-phrase")).Model!).Resources.Count == 0, "Unmatched search returns empty state");
        Check(((ItInformationIndexViewModel)((ViewResult)await controller.Learning(q: new string('x', 220))).Model!).Query.Length == 200, "Query is bounded");
        var publicSearch = (ContentListViewModel)((ViewResult)await controller.Search("Comptroller")).Model!;
        Check(publicSearch.Items.Any(item => item.ContentId == -3101), "Portal search includes selected reference text");
        var policy = PublicItInformationSamples.Resources[0];
        Check(policy.Sections.Select(section => section.Title).SequenceEqual(new[] { "Workplace Guidelines", "Profile Account", "Internet Access", "Flowcharts" }), "Policy focuses on the four requested sections");
        Check(policy.Sections.Single(section => section.Id == "flowcharts").Flowcharts.Count == 3, "All three policy flowcharts are represented");
        Check(((ItInformationIndexViewModel)((ViewResult)await controller.Learning(q: "transmittal")).Model!).Resources.Single().ContentId == -3101, "Flowchart node text can be searched");
        var account = policy.Sections.Single(section => section.Id == "accounts");
        Check(account.Points.Count == 7 && account.ProcessStages.Count == 3, "Profile Account preserves seven requirements and the complete three-stage account process");
        Check(account.SearchText.Contains("7 characters") && account.SearchText.Contains("3–6 months") && account.SearchText.Contains("PROXY"), "Previously omitted account details are retained as source content");
        Check(policy.Sections.Single(section => section.Id == "flowcharts").Flowcharts[2].Paths.Count == 3, "File-sharing includes User and both Administrator paths");
        Check(policy.Sections.Single(section => section.Id == "flowcharts").Flowcharts[2].SourceNote != null, "Different source timings are identified rather than silently normalized");
        Check(controller.PolicyDemoAsset("../appsettings.json") is NotFoundResult, "Source asset route rejects unlisted paths");
        var text = string.Join(" ", PublicItInformationSamples.Resources.Select(resource => resource.SearchText));
        foreach (var excluded in new[] { "Network Layout", "FortiGate", "FortiWeb", "MailMarshal", "El Salvador", "Contract Renewal", "[Details]", "Internet Explorer", "Business Central", "To be discussed", "2GB" })
            Check(!text.Contains(excluded, StringComparison.OrdinalIgnoreCase), "Excluded operational or unfinished content: " + excluded);
        Check(await controller.ContentDetails(PublicItInformationSamples.SlugPrefix + "unknown") is NotFoundResult, "Unknown reference does not query a repository");
        demo.Enabled = false;
        Check(controller.PolicyDemoAsset("profile-account.svg") is NotFoundResult, "Original source-section asset is unavailable outside demo mode");
        Check(await controller.ContentDetails(PublicItInformationSamples.Resources[0].Slug) is NotFoundResult, "Reference remains gated after demo exit");
        var admin = DemoChecks.Admin(new DefaultHttpContext(), demo, new DemoEnvironment(), repository);
        Check(await admin.Save(new SavePortalContentRequest { Slug = PublicItInformationSamples.Resources[0].Slug }) is BadRequestObjectResult, "Preview slug cannot be saved as a managed record");
        Check(guard.Calls == 0, "Reference catalog, details, search and mode guards never call a content repository");
        Console.WriteLine($"PASS: {count} public IT information checks, including anonymous readers, selected content, and zero database calls.");
    }
}
