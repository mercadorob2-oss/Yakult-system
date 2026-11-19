using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Logging.Abstractions;
using Yakult.SystemsPortal.Controllers;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;
using Yakult.Inventory.App.Models.CallMonitoring;

// Regression: pre-fix tickets stored metadata separately from the plain concern.
var existingTicket = new CallTicketListItem {
    TicketCode = "TCK-000119", IssueType = "Hardware", Classification = "Computer",
    DeviceName = "test-1234", Issue = "testing for new"
};
var expectedDescription = "Issue Type: Hardware\nClassification: Computer\nComputer Name: test-1234\n\nConcern:\ntesting for new";
if (existingTicket.IssueDescription != expectedDescription) throw new Exception("Existing-ticket metadata was omitted.");
existingTicket.Issue = expectedDescription;
if (existingTicket.IssueDescription != expectedDescription) throw new Exception("Formatted ticket was duplicated.");
if (CallTicketDescription.ForDisplay(null, null, null, "Older concern") != "Older concern") throw new Exception("Legacy concern changed.");
if (!CallTicketDescription.Format("Software", "Business application", "Payroll", "Cannot open").Contains("Device / application name: Payroll"))
    throw new Exception("Application name omitted.");

// An isolated fixture: no production configuration, SQL connection, or SMTP connection.
var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../Yakult.SystemsPortal"));
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = root, WebRootPath = Path.Combine(root, "wwwroot") });
builder.Configuration.Sources.Clear();
builder.Configuration.AddInMemoryCollection();
builder.WebHost.UseUrls("http://127.0.0.1:5194");
builder.Services.AddControllersWithViews().AddApplicationPart(typeof(HelpItController).Assembly);
builder.Services.AddSingleton<IControllerActivator, FixtureActivator>();
builder.Services.AddSingleton<FixtureRepository>();
builder.Services.AddSingleton<FixtureNotifications>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddSingleton<IDemoModeService, DemoModeService>();
builder.Services.Configure<PortalOptions>(_ => { });
builder.Services.AddAuthentication().AddCookie();
builder.Services.AddAuthorization();
var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/fixture/state", (FixtureRepository repo, FixtureNotifications mail) => new { created = repo.Created, notifications = mail.Attempts });
app.MapPost("/fixture/reset", (FixtureRepository repo, FixtureNotifications mail) => { repo.Reset(); mail.Reset(); });
app.MapPost("/fixture/ready/{ready:bool}", (bool ready, FixtureRepository repo) => repo.Ready = ready);
app.MapPost("/fixture/mail/{fail:bool}", (bool fail, FixtureNotifications mail) => mail.Fail = fail);
app.MapControllerRoute("default", "{controller=HelpIt}/{action=Index}/{id?}");

// Exercise the production email formatter and missing-configuration behavior without sending mail.
var sample = new HelpItTicketSummary { TicketCode = "TCK-000001", Classification = "Computer", DeviceName = "TEST-PC", Issue = "Does not start" };
if (!HelpItNotificationService.BuildBody(sample, "requester@example.invalid", "/Ticket/Details").Contains("Classification: Computer\nDevice / application name: TEST-PC"))
    throw new Exception("Email classification missing.");
var disabled = new HelpItNotificationService(builder.Configuration, NullLogger<HelpItNotificationService>.Instance);
if ((await disabled.NotifyAsync(sample, "requester@example.invalid", "/Ticket/Details")).Sent)
    throw new Exception("Unconfigured email must not report success.");
app.Run();

sealed class FixtureActivator(FixtureRepository repo, FixtureNotifications mail) : IControllerActivator
{
    public object Create(ControllerContext context) => new HelpItController(repo, null!, NullLogger<HelpItController>.Instance, mail);
    public void Release(ControllerContext context, object controller) { }
}

sealed class FixtureNotifications : IHelpItNotificationService
{
    public bool Fail { get; set; }
    public int Attempts { get; private set; }
    public void Reset() { Attempts = 0; Fail = false; }
    public Task<HelpItNotificationResult> NotifyAsync(HelpItTicketSummary ticket, string contactEmail, string detailsUrl)
    {
        Attempts++;
        if (Fail) throw new IOException("Simulated SMTP failure");
        return Task.FromResult(new HelpItNotificationResult(true, "IT Support has been notified by email."));
    }
}

sealed class FixtureRepository : IHelpItRepository
{
    public bool Ready { get; set; } = true;
    public int Created { get; private set; }
    public void Reset() { Created = 0; Ready = true; _ticket = null; }
    private HelpItTicketSummary? _ticket;
    public Task<(string?, string?, string?)> GetEmployeeOrgNamesAsync(int employeeId) => Task.FromResult<(string?, string?, string?)>(("Example Company", null, "Finance"));
    public Task<HelpItOrganizationSelection?> FindUniqueEmployeeOrganizationByEmailAsync(string email) => Task.FromResult<HelpItOrganizationSelection?>(null);
    public Task<IReadOnlyList<HelpItOrganizationCompanyOption>> GetOrganizationOptionsAsync() => Task.FromResult<IReadOnlyList<HelpItOrganizationCompanyOption>>(new[] {
        new HelpItOrganizationCompanyOption { CompanyId = 1, CompanyName = "Example Company",
            Departments = new() { new() { DepartmentId = 2, DepartmentName = "Finance" } },
            Branches = new() { new() { BranchId = 3, BranchName = "Head Office" } },
            Combinations = new() { new() { DepartmentId = 2, BranchId = 3 } } }
    });
    public Task<HelpItOrganizationSelection?> ValidateOrganizationSelectionAsync(int companyId, int? departmentId, int? branchId) =>
        Task.FromResult<HelpItOrganizationSelection?>(companyId == 1 && (departmentId == 2 || branchId == 3) ? new() { CompanyId = 1, DepartmentId = departmentId, BranchId = branchId } : null);
    public Task<HelpItTicketContactEmail> GetOrganizationContactEmailAsync(int? companyId, int? departmentId, int? branchId) => Task.FromResult(new HelpItTicketContactEmail());
    public Task<bool> CanStoreTicketContactEmailAsync() => Task.FromResult(Ready);
    public Task<HelpItTicketSummary> CreateTicketAsync(CreateHelpItTicketRequest request, int? companyId, int? departmentId, int? branchId, string callerName, int? createdByUserId)
    {
        Created++;
        _ticket = new() { TicketId = Created, TicketCode = $"TCK-{Created:000000}", Company = "Example Company", Department = "Finance", Branch = "Head Office",
            Issue = request.Issue, IssueType = request.IssueType, Classification = request.Classification, DeviceName = request.DeviceName,
            CallerName = callerName, Status = "Pending", Priority = "Medium", CreatedAt = DateTime.UtcNow };
        return Task.FromResult(_ticket);
    }
    public Task<(List<HelpItTicketSummary>, int)> GetTicketsAsync(HelpItTicketFilter filter) => Task.FromResult((new List<HelpItTicketSummary>(), 0));
    public Task<HelpItTicketDetailsViewModel?> GetPortalTicketDetailsAsync(string ticketCode) => Task.FromResult<HelpItTicketDetailsViewModel?>(_ticket == null ? null : new() { Ticket = _ticket });
    public Task<HelpItTicketContactEmail?> GetPortalTicketContactEmailAsync(string ticketCode) => Task.FromResult<HelpItTicketContactEmail?>(null);
    public Task<bool> SetPortalTicketContactEmailAsync(string ticketCode, string contactEmail) => throw new Exception("Contact must be saved with ticket creation.");
}
