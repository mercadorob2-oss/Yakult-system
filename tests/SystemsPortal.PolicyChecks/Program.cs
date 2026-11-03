using System.Reflection;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Controllers;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

// Isolated host: no production configuration or database. Saves exist only in memory.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Yakult.SystemsPortal"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection();
builder.WebHost.UseUrls("http://127.0.0.1:5188");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(CompanyInfoAdminController).Assembly)
    .ConfigureApplicationPartManager(manager => manager.FeatureProviders.Add(new PolicyControllersOnly()));
builder.Services.AddAuthentication("fixture").AddScheme<AuthenticationSchemeOptions, FixtureAuthentication>("fixture", _ => { });
builder.Services.AddAuthorization(options => {
    options.AddPolicy("ContentEditor", policy => policy.RequireRole("ContentEditor"));
    options.AddPolicy("EmployeeResourceEditor", policy => policy.RequireRole("ContentEditor"));
});
builder.Services.AddSingleton<IDemoModeService, NoDemo>();
builder.Services.AddSingleton<ICompanyInfoRepository>(DispatchProxy.Create<ICompanyInfoRepository, ReadOnlyCompanyInfo>());
var app = builder.Build();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/media/company-policies/{name}", (string name) => CompanyPolicyDocument.IsPolicyImageUrl("/media/company-policies/" + name)
    ? Results.File(Path.Combine(root, "App_Data", "CompanyPolicyMedia", name), "image/png") : Results.NotFound());
app.MapControllerRoute("default", "{controller=Public}/{action=CompanyPolicyDetails}/{id?}");
app.Run();

public sealed class PublicController : Controller
{
    private readonly IWebHostEnvironment _environment;
    public PublicController(IWebHostEnvironment environment) => _environment = environment;
    [HttpGet("/company-policies/it-policy---employee-guidelines")]
    public IActionResult CompanyPolicyDetails()
    {
        var item = ItPolicyContent.Load(_environment.ContentRootPath);
        return View("~/Views/Public/CompanyPolicyStructured.cshtml", new CompanyPolicyStructuredViewModel { Policy = item, Document = CompanyPolicyDocument.Parse(item.Body)! });
    }
    [HttpGet("/policy-preview/it")]
    public async Task<IActionResult> SavedPreview([Microsoft.AspNetCore.Mvc.FromServices] ICompanyInfoRepository repository,
        [Microsoft.AspNetCore.Mvc.FromServices] Microsoft.AspNetCore.Authorization.IAuthorizationService authorization)
    {
        var controller = new Yakult.SystemsPortal.Controllers.PublicController(null!, null!, null!, null!, repository,
            new NoDemo(), Options.Create(new PortalOptions()), Microsoft.Extensions.Logging.Abstractions.NullLogger<Yakult.SystemsPortal.Controllers.PublicController>.Instance, _environment);
        controller.ControllerContext = ControllerContext;
        return await controller.PreparedItPolicy(authorization);
    }
    [HttpGet("/fixture/document")]
    public IActionResult Document() => Json(CompanyPolicyDocument.Parse(ItPolicyContent.Load(_environment.ContentRootPath).Body));
}

public class ReadOnlyCompanyInfo : DispatchProxy
{
    private CompanyPolicyItem? _saved;
    private CompanyPolicyItem Current => _saved ??= Initial();
    private static CompanyPolicyItem Initial()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Yakult.SystemsPortal"));
        var template = ItPolicyContent.Load(root);
        return new CompanyPolicyItem { PolicyId = 3, Title = template.Title, Slug = template.Slug,
            Body = template.Body, Summary = template.Summary, Category = template.Category, IsPublished = true };
    }
    protected override object? Invoke(MethodInfo? method, object?[]? args)
    {
        if (method?.Name == nameof(ICompanyInfoRepository.GetPolicyByIdAsync)) return Task.FromResult<CompanyPolicyItem?>(Current);
        if (method?.Name == nameof(ICompanyInfoRepository.GetManagedPoliciesAsync)) return Task.FromResult<IReadOnlyList<CompanyPolicyItem>>(new[] { Current });
        if (method?.Name == nameof(ICompanyInfoRepository.GetManagedFaqsAsync)) return Task.FromResult<IReadOnlyList<CompanyFaqItem>>(Array.Empty<CompanyFaqItem>());
        if (method?.Name == nameof(ICompanyInfoRepository.SavePolicyAsync))
        {
            var request = (SaveCompanyPolicyRequest)args![0]!;
            if (request.PolicyId != 3) throw new InvalidOperationException("Save must preserve policy ID.");
            _saved = new CompanyPolicyItem { PolicyId = 3, Slug = Current.Slug, Title = request.Title,
                Body = request.Body, Summary = request.Summary, Category = request.Category, IsPublished = Current.IsPublished };
            return Task.FromResult(_saved);
        }
        throw new InvalidOperationException("Unexpected fixture repository access: " + method?.Name);
    }
}

public sealed class FixtureAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public FixtureAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Policy-Test-Role"].ToString();
        if (role.Length == 0) return Task.FromResult(AuthenticateResult.NoResult());
        var user = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "1"), new Claim(ClaimTypes.Name, "Policy Review"), new Claim(ClaimTypes.Role, role)], Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, Scheme.Name)));
    }
}
public sealed class PolicyControllersOnly : IApplicationFeatureProvider<ControllerFeature>
{
    public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
    {
        foreach (var controller in feature.Controllers.Where(type => type.AsType() != typeof(CompanyInfoAdminController) && type.AsType() != typeof(PublicController)).ToArray()) feature.Controllers.Remove(controller);
    }
}
public sealed class NoDemo : IDemoModeService
{
    public bool IsAvailable => false;
    public bool CanUse(HttpContext context) => false;
    public bool IsEnabled(HttpContext context) => false;
    public void SetEnabled(HttpContext context, bool enabled) { }
}
