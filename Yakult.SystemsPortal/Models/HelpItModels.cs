namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Form input for submitting a new Help IT ticket. Caller identity (company, branch,
/// department, name) is resolved server-side from the logged-in user's session/claims,
/// not supplied by the form.
/// </summary>
public sealed class CreateHelpItTicketRequest
{
    public string Issue { get; set; } = string.Empty;
    public string? IssueType { get; set; }

    /// <summary>
    /// Selected only for anonymous submissions. Authenticated submissions use the
    /// organization resolved from the signed-in account on the server.
    /// </summary>
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }

    /// <summary>
    /// Collected in the pre-submit review dialog only when no active branch or
    /// department contact email exists. It is stored on the submitted ticket.
    /// </summary>
    public string? ContactEmail { get; set; }

    /// <summary>
    /// Set only by the explicit "Confirm & submit ticket" dialog action. A plain
    /// form post never creates a ticket.
    /// </summary>
    public bool ConfirmedSubmission { get; set; }

    /// <summary>
    /// Required only when the submitter already created a ticket in this browser
    /// session. Mirrors <see cref="ConfirmedSubmission"/>: the browser must
    /// explicitly acknowledge the "already submitted a ticket" warning in the
    /// review dialog before another ticket can be created.
    /// </summary>
    public bool ConfirmedRepeatSubmission { get; set; }

    /// <summary>
    /// Only used when the submitter is not logged in (anonymous access). Ignored
    /// when a logged-in user's session/claims already resolve a caller name.
    /// </summary>
    public string? CallerNameInput { get; set; }
}

public sealed class HelpItOrganizationCompanyOption
{
    public int CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public List<HelpItOrganizationDepartmentOption> Departments { get; init; } = new();
    public List<HelpItOrganizationBranchOption> Branches { get; init; } = new();
    public List<HelpItOrganizationCombination> Combinations { get; init; } = new();
}

public sealed class HelpItOrganizationDepartmentOption
{
    public int DepartmentId { get; init; }
    public string DepartmentName { get; init; } = string.Empty;
}

public sealed class HelpItOrganizationBranchOption
{
    public int BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
}

public sealed class HelpItOrganizationCombination
{
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? BranchId { get; init; }
    public string? BranchName { get; init; }
}

public sealed class HelpItOrganizationSelection
{
    public int CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public int? DepartmentId { get; init; }
    public string? DepartmentName { get; init; }
    public int? BranchId { get; init; }
    public string? BranchName { get; init; }
}

/// <summary>
/// Row shown on the Help IT ticket tracker, sourced from dbo.vw_Call_TicketList
/// (the same view used by the ITCM desktop app and the mobile API).
/// </summary>
public sealed class HelpItTicketSummary
{
    public int TicketId { get; init; }
    public string TicketCode { get; init; } = string.Empty;
    public string? Company { get; init; }
    public string? Department { get; init; }
    public string? Branch { get; init; }
    public string Issue { get; init; } = string.Empty;
    public string? IssueType { get; init; }
    public string Status { get; init; } = string.Empty;
    public string Priority { get; init; } = string.Empty;
    public string CallerName { get; init; } = string.Empty;
    public string? ResponsiblePerson { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? SolvedAt { get; init; }
}

/// <summary>
/// Filter/paging state for the Help IT ticket tracker. Bound directly from the
/// query string on GET so the view stays a plain, bookmarkable/shareable link.
/// </summary>
public sealed class HelpItTicketFilter
{
    public string? Status { get; set; }
    public string? Search { get; set; }
    public string? Priority { get; set; }
    public string? IssueType { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 25;
}

public sealed class HelpItSubmitViewModel
{
    public CreateHelpItTicketRequest Request { get; set; } = new();

    public string CallerName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? BranchName { get; set; }
    public string? DepartmentName { get; set; }
    public IReadOnlyList<HelpItOrganizationCompanyOption> OrganizationOptions { get; set; } = Array.Empty<HelpItOrganizationCompanyOption>();
    public string? OrganizationContactEmail { get; set; }
    public string? OrganizationContactEmailSource { get; set; }
    public bool IsAnonymous { get; set; }

    public bool RequiresContactEmail => string.IsNullOrWhiteSpace(OrganizationContactEmail);

    /// <summary>
    /// True when this browser session already created a ticket, so the review
    /// dialog must ask for an explicit second confirmation before submitting
    /// another one.
    /// </summary>
    public bool HasPriorSubmission { get; set; }

    /// <summary>Ticket code of the most recent ticket created in this session.</summary>
    public string? PriorTicketCode { get; set; }

    public string? ErrorMessage { get; set; }

    public static readonly IReadOnlyList<string> IssueTypeOptions = new[]
    {
        "Hardware",
        "Software",
        "Network",
        "Account/Access",
        "Other"
    };

    public static readonly IReadOnlyList<string> PriorityOptions = new[]
    {
        "Low",
        "Medium",
        "High",
        "Critical"
    };
}

public sealed class HelpItTrackerViewModel
{
    public List<HelpItTicketSummary> Tickets { get; set; } = new();
    public HelpItTicketFilter Filter { get; set; } = new();
    public int TotalCount { get; set; }
    public bool IsAdministrator { get; set; }
    public bool IsAllTicketsExpanded { get; set; }
    public bool HasTicketSearch => !string.IsNullOrWhiteSpace(Filter.Search);
    public bool CanShowTicketTable => IsAdministrator && IsAllTicketsExpanded || HasTicketSearch;

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)Math.Max(Filter.PageSize, 1));

    public static readonly IReadOnlyList<string> IssueTypeOptions = HelpItSubmitViewModel.IssueTypeOptions;
    public static readonly IReadOnlyList<string> PriorityOptions = HelpItSubmitViewModel.PriorityOptions;
}

/// <summary>Effective contact email resolved for a portal ticket without exposing it publicly.</summary>
public sealed class HelpItTicketContactEmail
{
    public string? Email { get; init; }
    public string? Source { get; init; }
}

/// <summary>Public-safe detail and status data for a valid ticket from any source.</summary>
public sealed class HelpItTicketDetailsViewModel
{
    public HelpItTicketSummary Ticket { get; init; } = new();
    public List<HelpItTicketTimelineEvent> Timeline { get; init; } = new();
}

public sealed class HelpItTicketTimelineEvent
{
    public DateTime OccurredAt { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public string Kind { get; init; } = "history";
}
