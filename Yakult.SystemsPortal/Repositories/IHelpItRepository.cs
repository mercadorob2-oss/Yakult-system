using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Repositories;

/// <summary>
/// Repository backing the public-facing "Help IT" feature: submitting IT support
/// tickets that land in the same dbo.CallTicket queue used by the ITCM desktop app,
/// and a read-only tracker over that queue.
/// </summary>
public interface IHelpItRepository
{
    /// <summary>
    /// Resolves the display name of the employee's company/branch/department for
    /// the submission form, given the EmpId from the logged-in user's claims.
    /// </summary>
    Task<(string? CompanyName, string? BranchName, string? DepartmentName)> GetEmployeeOrgNamesAsync(int employeeId);

    /// <summary>
    /// Gets the public-safe organization hierarchy used by anonymous ticket submitters.
    /// The hierarchy contains names and IDs only; it does not expose employee or email data.
    /// </summary>
    Task<IReadOnlyList<HelpItOrganizationCompanyOption>> GetOrganizationOptionsAsync();

    /// <summary>
    /// Validates that the selected company, department, and branch form a valid active
    /// organization combination before an anonymous ticket is created.
    /// </summary>
    Task<HelpItOrganizationSelection?> ValidateOrganizationSelectionAsync(
        int companyId,
        int? departmentId,
        int? branchId);

    /// <summary>
    /// Resolves the active organization contact address before a ticket is created.
    /// Branch email takes precedence over department email.
    /// </summary>
    Task<HelpItTicketContactEmail> GetOrganizationContactEmailAsync(int? companyId, int? departmentId, int? branchId);

    /// <summary>
    /// Indicates whether the database has the ticket-scoped fallback contact-email
    /// column required for a requester-provided address.
    /// </summary>
    Task<bool> CanStoreTicketContactEmailAsync();

    /// <summary>
    /// Creates a new ticket via dbo.sp_Call_CreateTicket - the same stored procedure
    /// used by the ITCM desktop app and the mobile API. The ticket is created
    /// unassigned (AssignedToEmpId = NULL) so it appears in the desktop app's normal
    /// unassigned queue for an IT staff member to pick up.
    /// </summary>
    Task<HelpItTicketSummary> CreateTicketAsync(
        CreateHelpItTicketRequest request,
        int? companyId,
        int? departmentId,
        int? branchId,
        string callerName,
        int? createdByUserId);

    /// <summary>
    /// Lists tickets from dbo.vw_Call_TicketList for the tracker, with optional
    /// filters and server-side paging.
    /// </summary>
    Task<(List<HelpItTicketSummary> Tickets, int TotalCount)> GetTicketsAsync(HelpItTicketFilter filter);

    /// <summary>
    /// Gets a public-safe detail view for any valid ticket code in dbo.CallTicket.
    /// Returns null for invalid or missing tickets regardless of ticket source.
    /// </summary>
    Task<HelpItTicketDetailsViewModel?> GetPortalTicketDetailsAsync(string ticketCode);

    /// <summary>
    /// Resolves the contact address for a newly submitted portal ticket. Branch
    /// email takes precedence over department email, followed by the ticket's own
    /// user-provided fallback. This data is confirmation-only and not public.
    /// </summary>
    Task<HelpItTicketContactEmail?> GetPortalTicketContactEmailAsync(string ticketCode);

    /// <summary>
    /// Saves a requester-provided fallback contact address on one Portal ticket.
    /// It never changes shared branch or department email configuration.
    /// </summary>
    Task<bool> SetPortalTicketContactEmailAsync(string ticketCode, string contactEmail);
}
