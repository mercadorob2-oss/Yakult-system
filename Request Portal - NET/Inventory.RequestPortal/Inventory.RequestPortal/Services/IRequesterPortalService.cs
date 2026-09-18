using Inventory.RequestPortal.Models.ViewModels;

namespace Inventory.RequestPortal.Services
{
    // IMPORTANT:
    // Users select cartridges by MODEL and QUANTITY for usability.
    // Internally, the system always resolves requests to specific
    // cartridges using ItemId / SerialNumber.
    // Model-based auto-matching is forbidden and must never be reintroduced.

    /// <summary>
    /// Service interface for Requester Portal functionality.
    /// Connects directly to the database.
    /// </summary>
    public interface IRequesterPortalService
    {
        // Cartridge Models with Availability (NEW - for Model + Quantity selection)
        List<CartridgeModelAvailabilityViewModel> GetCartridgeModelsWithAvailability();

        // Ink / Printhead / Toner Cartridge models (from dbo.ConsumableModel), for the same
        // Model + Quantity dropdown pattern as GetCartridgeModelsWithAvailability.
        // category must be one of: "Ink", "Printhead", "Toner Cartridge".
        List<CartridgeModelAvailabilityViewModel> GetConsumableModelsWithAvailability(string category);

        // [LEGACY] Individual Cartridge Items - kept for backwards compatibility
        List<CartridgeItemViewModel> GetCartridgeItems();

        // Destination Queries
        List<CompanyViewModel> GetActiveCompanies();
        List<BranchViewModel> GetAllBranches();
        List<DepartmentViewModel> GetAllDepartments();
        List<BranchViewModel> GetBranchesByCompany(int companyId);
        List<DepartmentViewModel> GetDepartmentsByCompany(int companyId);

        // Employee Queries (for Received By dropdown)
        List<EmployeeViewModel> GetActiveEmployees();

        // Department Account: employees scoped to company+department+branch
        List<EmployeeViewModel> GetEmployeesByAccount(int companyId, int departmentId, int branchId);

        // Request Submission (NEW - Model + Quantity based)
        // HARD REQUIREMENT: empId must come from session (User.EmpId)
        // Portal is READ-ONLY - NEVER creates employees
        List<int> CreateCartridgeRequestByModel(
            CartridgeRequestViewModel request,
            List<CartridgeRequestItemViewModel> requestItems,
            int empId,
            out Guid submissionSessionId);

        // IT Assisted: submit on behalf of any employee; requests start at 'Awaiting Authorization'
        // so that CreateITAssistedAsync can flip them to 'Under Review' atomically.
        // targetEmpId is null for department-level requests (request.IsDeptLevel == true), in which
        // case Request.EmpId stays NULL and request.DestinationCompanyId/BranchId/DepartmentId
        // (all optional in that mode) are stored directly on the Request row instead.
        List<int> CreateAssistedCartridgeRequestByModel(
            CartridgeRequestViewModel request,
            List<CartridgeRequestItemViewModel> requestItems,
            int? targetEmpId,
            out Guid submissionSessionId);

        // [LEGACY] Request Submission - kept for backwards compatibility
        // HARD REQUIREMENT: empId must come from session (User.EmpId)
        // Portal is READ-ONLY - NEVER creates employees
        int CreateCartridgeRequest(
            CartridgeRequestViewModel request,
            int empId);

        // IT Manual Authorization — approvers scoped to the target employee's company+branch+department
        Task<List<ITApproverViewModel>> GetApproversByScope(int comId, int branchId, int deptId);

        // Request Status Tracking (Read-Only)
        List<PortalRequestStatusViewModel> GetPortalRequestsByUser(int userId);
        PortalRequestStatusViewModel? GetRequestStatus(int reqId);
    }
}
