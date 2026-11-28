using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Controllers;

/// <summary>
/// Employee "Help IT" ticketing feature: lets domain-authenticated employees submit IT support tickets
/// that land in the same dbo.CallTicket queue used by the ITCM desktop app, and
/// track their status. Tickets are created unassigned; assignment and status
/// changes remain exclusively in the desktop app.
///
/// Submission and contact lookup require a verified domain sign-in. Tracking
/// and ticket details remain accessible without signing in.
/// </summary>
[AllowAnonymous]
public sealed class HelpItController : Controller
{
    private const string SessionTicketCountKey = "HelpIt:TicketCount";
    private const string SessionLastTicketCodeKey = "HelpIt:LastTicketCode";

    private readonly IHelpItRepository _helpItRepository;
    private readonly IPortalCardRepository _portalCardRepository;
    private readonly ILogger<HelpItController> _logger;
    private readonly IHelpItNotificationService _notifications;

    public HelpItController(
        IHelpItRepository helpItRepository,
        IPortalCardRepository portalCardRepository,
        ILogger<HelpItController> logger,
        IHelpItNotificationService notifications)
    {
        _helpItRepository = helpItRepository;
        _portalCardRepository = portalCardRepository;
        _logger = logger;
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (RequireDomainLogin() is { } loginRequired) return loginRequired;
        ViewData["Title"] = "Help IT";
        return View(await BuildSubmitViewModelAsync());
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HasDomainLogin()) return RedirectToAction(nameof(Index));
        return RedirectToAction("Help", "Public", new { ticketLogin = "true" });
    }

    private bool HasDomainLogin() => User.Identity?.IsAuthenticated == true
        && (User.HasClaim("AuthenticationSource", "ActiveDirectory")
            || User.HasClaim("DomainAuthenticated", "true"));

    private IActionResult? RequireDomainLogin()
    {
        if (HasDomainLogin()) return null;
        var loginUrl = Url.Action(nameof(Login), "HelpIt");
        return IsDialogSubmission()
            ? StatusCode(StatusCodes.Status401Unauthorized, new
            {
                error = "Log in to your domain account to submit a ticket.",
                loginUrl
            })
            : RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveContactEmail(int? companyId, int? departmentId, int? branchId)
    {
        if (RequireDomainLogin() is { } loginRequired) return loginRequired;
        if (!companyId.HasValue || (!departmentId.HasValue && !branchId.HasValue))
            return BadRequest(new { error = "Select a company and a department or branch first." });

        var organization = await _helpItRepository.ValidateOrganizationSelectionAsync(
            companyId.Value,
            departmentId,
            branchId);

        if (organization == null)
            return BadRequest(new { error = "The selected organization combination is not valid." });

        var contact = await _helpItRepository.GetOrganizationContactEmailAsync(
            organization.CompanyId,
            organization.DepartmentId,
            organization.BranchId);

        return Json(new { email = contact.Email, source = contact.Source });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateHelpItTicketRequest request)
    {
        if (RequireDomainLogin() is { } loginRequired) return loginRequired;
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

        request.IssueType = request.IssueType?.Trim();
        request.Classification = request.Classification?.Trim();
        request.DeviceName = request.DeviceName?.Trim();
        if (!ModelState.IsValid
            || request.Issue.Trim().Length > 2000
            || !HelpItSubmitViewModel.Classifications.TryGetValue(request.IssueType ?? "", out var classifications)
            || !classifications.Contains(request.Classification ?? "", StringComparer.Ordinal)
            || (request.DeviceName?.Length ?? 0) > 150)
            return await SubmissionFailureAsync(request, "Select an issue type and matching classification. Keep the concern within 2,000 characters and the name within 150 characters.");
        if ((request.Classification == "Computer" || request.Classification == "Laptop") && string.IsNullOrWhiteSpace(request.DeviceName))
            return await SubmissionFailureAsync(request, "Enter the computer or laptop name so IT can identify the device.");
        if (Yakult.Inventory.App.Models.CallMonitoring.CallTicketDescription.Format(
                request.IssueType, request.Classification, request.DeviceName, request.Issue).Length > 2000)
            return await SubmissionFailureAsync(request, "Please shorten the concern so the complete ticket fits within 2,000 characters.");

        var isIndependentAdUser = User.HasClaim("AuthenticationSource", "ActiveDirectory");
        var (companyId, departmentId, branchId, sessionCallerName, userId) = GetCurrentUserContext();

        if (isIndependentAdUser)
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

        var callerName = sessionCallerName;

        if (string.IsNullOrWhiteSpace(callerName))
        {
            return await SubmissionFailureAsync(request, "Please enter your name before submitting.");
        }

        if (callerName.Length > 150)
            return await SubmissionFailureAsync(request, "Keep the requester name within 150 characters.");

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
            _logger.LogWarning("Help IT submission blocked: contact email or classification storage is not installed.");
            return await SubmissionFailureAsync(
                request,
                "Ticket submission is temporarily unavailable. Please contact IT Support directly.",
                StatusCodes.Status503ServiceUnavailable);
        }

        request.ContactEmail = requiresFallbackContact ? fallbackContactEmail : organizationContact.Email;

        try
        {
            var ticket = await _helpItRepository.CreateTicketAsync(
                request,
                companyId,
                departmentId,
                branchId,
                callerName,
                userId);

            if (isIndependentAdUser)
            {
                try
                {
                    await _portalCardRepository.LogPortalEventAsync(
                        "Create",
                        "PortalTicket",
                        ticket.TicketId,
                        userId,
                        User.Identity?.Name,
                        $"Submitted Help IT ticket {ticket.TicketCode}.",
                        HttpContext.Connection.RemoteIpAddress?.ToString());
                }
                catch (Exception auditException)
                {
                    _logger.LogWarning(auditException, "Unable to write ticket audit event for {TicketCode}.", ticket.TicketCode);
                }
            }

            // Remember this submission so the next ticket in this session is
            // gated by the "already submitted a ticket" confirmation.
            HttpContext.Session.SetInt32(SessionTicketCountKey, (HttpContext.Session.GetInt32(SessionTicketCountKey) ?? 0) + 1);
            HttpContext.Session.SetString(SessionLastTicketCodeKey, ticket.TicketCode);

            var detailsUrl = Url.RouteUrl("HelpItTicketDetails", new { ticketCode = ticket.TicketCode }, protocol: Request.Scheme)!;
            HelpItNotificationResult notification;
            try
            {
                notification = await _notifications.NotifyAsync(ticket, request.ContactEmail!, detailsUrl);
            }
            catch (Exception notificationException)
            {
                _logger.LogError(notificationException, "Notification failed for saved ticket {TicketCode}.", ticket.TicketCode);
                notification = new(false, "Your ticket is saved. The email notification is unavailable; you do not need to submit again.");
            }
            if (IsDialogSubmission())
            {
                var confirmationTicket = ticket;
                var contactEmail = requiresFallbackContact ? fallbackContactEmail : organizationContact.Email;
                var contactEmailSource = requiresFallbackContact
                    ? "Email provided for this ticket"
                    : organizationContact.Source;

                return Json(new
                {
                    ticketCode = confirmationTicket.TicketCode,
                    issue = confirmationTicket.Issue,
                    issueType = confirmationTicket.IssueType,
                    classification = confirmationTicket.Classification,
                    deviceName = confirmationTicket.DeviceName,
                    notificationSent = notification.Sent,
                    notificationMessage = notification.Message,
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

            TempData["HelpItNotificationMessage"] = notification.Message;
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
        var isAnonymous = User.Identity?.IsAuthenticated != true;
        var isIndependentAdUser = User.HasClaim("AuthenticationSource", "ActiveDirectory");

        string? companyName = null;
        string? branchName = null;
        string? departmentName = null;

        var employeeIdClaim = User.FindFirstValue("EmployeeId");
        if (int.TryParse(employeeIdClaim, out var employeeId) && employeeId > 0)
        {
            (companyName, branchName, departmentName) = await _helpItRepository.GetEmployeeOrgNamesAsync(employeeId);
        }

        HelpItOrganizationSelection? adOrganization = null;
        if (isIndependentAdUser)
        {
            var adEmail = User.FindFirstValue("AdEmail");
            if (!string.IsNullOrWhiteSpace(adEmail))
                adOrganization = await _helpItRepository.FindUniqueEmployeeOrganizationByEmailAsync(adEmail);

            if (adOrganization != null)
            {
                companyId = adOrganization.CompanyId;
                departmentId = adOrganization.DepartmentId;
                branchId = adOrganization.BranchId;
                companyName = adOrganization.CompanyName;
                departmentName = adOrganization.DepartmentName;
                branchName = adOrganization.BranchName;
            }
        }

        var showOrganizationPicker = isAnonymous || isIndependentAdUser;
        var organizationOptions = showOrganizationPicker
            ? await _helpItRepository.GetOrganizationOptionsAsync()
            : Array.Empty<HelpItOrganizationCompanyOption>();

        var organizationContact = await _helpItRepository.GetOrganizationContactEmailAsync(companyId, departmentId, branchId);
        var model = new HelpItSubmitViewModel
        {
            CallerName = callerName,
            CompanyName = companyName,
            BranchName = branchName,
            DepartmentName = departmentName,
            OrganizationOptions = organizationOptions,
            OrganizationContactEmail = organizationContact.Email,
            OrganizationContactEmailSource = organizationContact.Source,
            IsAnonymous = isAnonymous,
            ShowOrganizationPicker = showOrganizationPicker,
            HasPriorSubmission = HasPriorSubmission(),
            PriorTicketCode = HttpContext.Session.GetString(SessionLastTicketCodeKey)
        };

        if (adOrganization != null)
        {
            model.Request.CompanyId = adOrganization.CompanyId;
            model.Request.DepartmentId = adOrganization.DepartmentId;
            model.Request.BranchId = adOrganization.BranchId;
        }

        return model;
    }

    private bool HasPriorSubmission() =>
        (HttpContext.Session.GetInt32(SessionTicketCountKey) ?? 0) > 0;

    private (int? CompanyId, int? DepartmentId, int? BranchId, string CallerName, int? UserId) GetCurrentUserContext()
    {
        int? companyId = int.TryParse(User.FindFirstValue("CompanyId"), out var comId) ? comId : null;
        int? departmentId = int.TryParse(User.FindFirstValue("DepartmentId"), out var deptId) ? deptId : null;
        int? branchId = int.TryParse(User.FindFirstValue("BranchId"), out var brId) ? brId : null;
        int? userId = int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uId) ? uId : null;
        var callerName = User.HasClaim("AuthenticationSource", "ActiveDirectory")
            ? User.FindFirstValue("AdDisplayName") ?? User.Identity?.Name ?? string.Empty
            : User.Identity?.Name ?? string.Empty;

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
