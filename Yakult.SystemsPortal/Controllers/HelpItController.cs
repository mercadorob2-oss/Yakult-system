using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;

namespace Yakult.SystemsPortal.Controllers;

/// <summary>
/// Public-facing "Help IT" ticketing feature: lets anyone submit IT support tickets
/// that land in the same dbo.CallTicket queue used by the ITCM desktop app, and
/// track their status. Tickets are created unassigned; assignment and status
/// changes remain exclusively in the desktop app.
///
/// NOTE: the pages and submission endpoint are intentionally anonymous-accessible
/// (no portal login required) per product decision. This means submissions from
/// anonymous visitors carry no verified employee identity - CallerName is
/// whatever the visitor types in, and CreatedByUserId is null. There is currently
/// no rate limiting/CAPTCHA on ticket creation, so this endpoint is exposed to
/// spam/abuse without additional protections.
/// </summary>
[AllowAnonymous]
public sealed class HelpItController : Controller
{
    private const string SessionTicketCountKey = "HelpIt:TicketCount";
    private const string SessionLastTicketCodeKey = "HelpIt:LastTicketCode";

    private readonly IHelpItRepository _helpItRepository;
    private readonly ILogger<HelpItController> _logger;

    public HelpItController(IHelpItRepository helpItRepository, ILogger<HelpItController> logger)
    {
        _helpItRepository = helpItRepository;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "Help IT";
        return View(await BuildSubmitViewModelAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateHelpItTicketRequest request)
    {
        ViewData["Title"] = "Help IT";

        // A normal form post is intentionally not enough to create a ticket. The
        // browser must first open the review dialog and send this explicit flag
        // from its Confirm & submit ticket action.
        if (!request.ConfirmedSubmission)
        {
            return await SubmissionFailureAsync(
                request,
                "Review the ticket details and select Confirm & submit ticket before it is sent.");
        }

        if (HasPriorSubmission() && !request.ConfirmedRepeatSubmission)
        {
            return await SubmissionFailureAsync(
                request,
                "You've already submitted a ticket in this session. Confirm that this is a separate issue before submitting another.");
        }

        if (string.IsNullOrWhiteSpace(request.Issue))
        {
            return await SubmissionFailureAsync(request, "Please describe the issue before submitting.");
        }

        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var (companyId, departmentId, branchId, sessionCallerName, userId) = GetCurrentUserContext();

        if (!isAuthenticated)
        {
            if (!request.CompanyId.HasValue
                || (!request.DepartmentId.HasValue && !request.BranchId.HasValue))
            {
                return await SubmissionFailureAsync(
                    request,
                    "Select a company and at least a department or branch before submitting.");
            }

            var selectedOrganization = await _helpItRepository.ValidateOrganizationSelectionAsync(
                request.CompanyId.Value,
                request.DepartmentId,
                request.BranchId);

            if (selectedOrganization == null)
            {
                return await SubmissionFailureAsync(
                    request,
                    "The selected organization combination is not valid. Select a company and either a department, a branch, or both.");
            }

            companyId = selectedOrganization.CompanyId;
            departmentId = selectedOrganization.DepartmentId;
            branchId = selectedOrganization.BranchId;
        }

        var callerName = isAuthenticated
            ? sessionCallerName
            : (request.CallerNameInput ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(callerName))
        {
            return await SubmissionFailureAsync(request, "Please enter your name before submitting.");
        }

        var organizationContact = await _helpItRepository.GetOrganizationContactEmailAsync(companyId, departmentId, branchId);
        var requiresFallbackContact = string.IsNullOrWhiteSpace(organizationContact.Email);
        var fallbackContactEmail = (request.ContactEmail ?? string.Empty).Trim();

        if (requiresFallbackContact)
        {
            if (fallbackContactEmail.Length > 255 || !System.Net.Mail.MailAddress.TryCreate(fallbackContactEmail, out _))
            {
                return await SubmissionFailureAsync(request, "Enter a valid department or branch contact email before submitting.");
            }
        }

        if (!await _helpItRepository.CanStoreTicketContactEmailAsync())
        {
            return await SubmissionFailureAsync(
                request,
                "Ticket contact email storage is not ready yet. Please ask IT to apply the ContactEmail database migration before submitting.",
                StatusCodes.Status503ServiceUnavailable);
        }

        try
        {
            var ticket = await _helpItRepository.CreateTicketAsync(
                request,
                companyId,
                departmentId,
                branchId,
                callerName,
                userId);

            // Remember this submission so the next ticket in this session is
            // gated by the "already submitted a ticket" confirmation.
            HttpContext.Session.SetInt32(SessionTicketCountKey, (HttpContext.Session.GetInt32(SessionTicketCountKey) ?? 0) + 1);
            HttpContext.Session.SetString(SessionLastTicketCodeKey, ticket.TicketCode);

            var ticketContactEmail = requiresFallbackContact ? fallbackContactEmail : organizationContact.Email;
            if (!string.IsNullOrWhiteSpace(ticketContactEmail)
                && (!await _helpItRepository.CanStoreTicketContactEmailAsync()
                    || !await _helpItRepository.SetPortalTicketContactEmailAsync(ticket.TicketCode, ticketContactEmail)))
            {
                throw new InvalidOperationException("The newly submitted ticket could not be assigned its contact email.");
            }

            if (IsDialogSubmission())
            {
                var ticketDetails = await _helpItRepository.GetPortalTicketDetailsAsync(ticket.TicketCode);
                var confirmationTicket = ticketDetails?.Ticket ?? ticket;
                var contactEmail = requiresFallbackContact ? fallbackContactEmail : organizationContact.Email;
                var contactEmailSource = requiresFallbackContact
                    ? "Email provided for this ticket"
                    : organizationContact.Source;

                return Json(new
                {
                    ticketCode = confirmationTicket.TicketCode,
                    issue = confirmationTicket.Issue,
                    issueType = confirmationTicket.IssueType,
                    callerName = confirmationTicket.CallerName,
                    company = confirmationTicket.Company,
                    department = confirmationTicket.Department,
                    branch = confirmationTicket.Branch,
                    status = confirmationTicket.Status,
                    priority = confirmationTicket.Priority,
                    submittedAt = confirmationTicket.CreatedAt.ToLocalTime().ToString("MMM d, yyyy h:mm tt"),
                    contactEmail,
                    contactEmailSource,
                    detailsUrl = Url.RouteUrl("HelpItTicketDetails", new { ticketCode = confirmationTicket.TicketCode })
                });
            }

            return RedirectToRoute("HelpItTicketDetails", new { ticketCode = ticket.TicketCode });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to submit Help IT ticket.");
            return await SubmissionFailureAsync(
                request,
                "Unable to submit the ticket right now. Please try again or contact IT directly.",
                StatusCodes.Status500InternalServerError);
        }
    }

    private async Task<IActionResult> SubmissionFailureAsync(
        CreateHelpItTicketRequest request,
        string message,
        int statusCode = StatusCodes.Status400BadRequest)
    {
        if (IsDialogSubmission())
            return StatusCode(statusCode, new { error = message });

        var model = await BuildSubmitViewModelAsync();
        model.Request = request;
        model.ErrorMessage = message;
        return View("Index", model);
    }

    private bool IsDialogSubmission() =>
        Request.Headers.TryGetValue("X-Requested-With", out var requestedWith)
        && string.Equals(requestedWith.ToString(), "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    [HttpGet]
    public async Task<IActionResult> Tracker(HelpItTicketFilter filter, bool viewAll = false)
    {
        ViewData["Title"] = "Help IT - Ticket Tracker";

        filter ??= new HelpItTicketFilter();
        var isAdministrator = User.IsInRole("Administrator") || User.HasClaim("IsDeveloper", "true");
        var hasSearch = !string.IsNullOrWhiteSpace(filter.Search);
        var showAllTickets = isAdministrator && viewAll && !hasSearch;

        // Regular employees may search, but cannot request the unfiltered queue.
        // Administrators must explicitly opt in with the small View all tickets action.
        if (!isAdministrator && !hasSearch)
        {
            filter.Status = null;
            filter.Priority = null;
            filter.IssueType = null;
        }

        var (tickets, totalCount) = hasSearch || showAllTickets
            ? await _helpItRepository.GetTicketsAsync(filter)
            : (new List<HelpItTicketSummary>(), 0);

        var model = new HelpItTrackerViewModel
        {
            Tickets = tickets,
            Filter = filter,
            TotalCount = totalCount,
            IsAdministrator = isAdministrator,
            IsAllTicketsExpanded = showAllTickets
        };

        if (TempData["HelpItSuccessMessage"] is string successMessage)
        {
            ViewData["SuccessMessage"] = successMessage;
        }

        return View(model);
    }

    private async Task<HelpItSubmitViewModel> BuildSubmitViewModelAsync()
    {
        var (companyId, departmentId, branchId, callerName, _) = GetCurrentUserContext();

        string? companyName = null;
        string? branchName = null;
        string? departmentName = null;

        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (int.TryParse(employeeIdClaim, out var employeeId) && employeeId > 0)
        {
            (companyName, branchName, departmentName) = await _helpItRepository.GetEmployeeOrgNamesAsync(employeeId);
        }

        var isAnonymous = User.Identity?.IsAuthenticated != true;
        var organizationOptions = isAnonymous
            ? await _helpItRepository.GetOrganizationOptionsAsync()
            : Array.Empty<HelpItOrganizationCompanyOption>();

        var organizationContact = await _helpItRepository.GetOrganizationContactEmailAsync(companyId, departmentId, branchId);
        return new HelpItSubmitViewModel
        {
            CallerName = callerName,
            CompanyName = companyName,
            BranchName = branchName,
            DepartmentName = departmentName,
            OrganizationOptions = organizationOptions,
            OrganizationContactEmail = organizationContact.Email,
            OrganizationContactEmailSource = organizationContact.Source,
            IsAnonymous = isAnonymous,
            HasPriorSubmission = HasPriorSubmission(),
            PriorTicketCode = HttpContext.Session.GetString(SessionLastTicketCodeKey)
        };
    }

    private bool HasPriorSubmission() =>
        (HttpContext.Session.GetInt32(SessionTicketCountKey) ?? 0) > 0;

    private (int? CompanyId, int? DepartmentId, int? BranchId, string CallerName, int? UserId) GetCurrentUserContext()
    {
        int? companyId = int.TryParse(User.FindFirstValue("CompanyId"), out var comId) ? comId : null;
        int? departmentId = int.TryParse(User.FindFirstValue("DepartmentId"), out var deptId) ? deptId : null;
        int? branchId = int.TryParse(User.FindFirstValue("BranchId"), out var brId) ? brId : null;
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uId) ? uId : null;
        var callerName = User.Identity?.Name ?? string.Empty;

        return (companyId, departmentId, branchId, callerName, userId);
    }

    [HttpGet("/Ticket/Details", Name = "HelpItTicketDetails")]
    public async Task<IActionResult> Details(string? ticketCode)
    {
        var normalizedCode = NormalizeTicketCode(ticketCode);
        if (normalizedCode == null)
            return NotFound();

        var model = await _helpItRepository.GetPortalTicketDetailsAsync(normalizedCode);
        if (model == null)
            return NotFound();

        ViewData["Title"] = $"Ticket {model.Ticket.TicketCode}";
        return View("Details", model);
    }

    private static string? NormalizeTicketCode(string? ticketCode)
    {
        var value = (ticketCode ?? string.Empty).Trim().ToUpperInvariant();
        return System.Text.RegularExpressions.Regex.IsMatch(value, @"^TCK-[0-9]{6}$") ? value : null;
    }
}
