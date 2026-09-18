using System;
using System.Collections.Generic;

namespace Inventory.RequestPortal.Models.ViewModels
{
    public class CartridgeAuthorizationViewModel
    {
        public int       AuthorizationId      { get; set; }
        public int       EmployeeId           { get; set; }
        public int       DepartmentId         { get; set; }
        public string    Status               { get; set; }   // Pending | Approved | Rejected | Used
        public int?      SignedBySupervisorId { get; set; }
        public string?   SignedByName         { get; set; }
        public DateTime? SignedDate           { get; set; }
        public DateTime  CreatedDate          { get; set; }

        // Display fields from JOINs
        public string? EmployeeName     { get; set; }
        public string? DepartmentName   { get; set; }
        public string? BranchName       { get; set; }
        public string? CompanyName      { get; set; }
        public string? EmployeePosition { get; set; }

        // Derived approval level based on requester's position
        // "PendingCoordinator" | "PendingSupervisor" | "PendingManager" | "Approved" | "Rejected"
        public string ApprovalLevel
        {
            get
            {
                var s = (Status ?? "").Trim().ToLowerInvariant();
                if (s == "approved") return "Approved";
                if (s == "rejected") return "Rejected";
                var p = (EmployeePosition ?? "").ToLowerInvariant();
                if (p.Contains("manager") || p.Contains("director") || p.Contains("vp") || p.Contains("president"))
                    return "PendingManager";
                if (p.Contains("supervisor") || p.Contains("team lead") || p.Contains("lead"))
                    return "PendingSupervisor";
                return "PendingCoordinator";
            }
        }

        public string ApprovalLevelTooltip => ApprovalLevel switch
        {
            "PendingManager"    => "Awaiting manager approval",
            "PendingSupervisor" => "Awaiting supervisor approval",
            "Approved"          => "Fully approved",
            "Rejected"          => "Request was rejected",
            _                   => "Awaiting coordinator approval"
        };

        // Digital signature fields (added via migration)
        public string? SignatureData     { get; set; }  // base64 data URL
        public string? SignatureSource   { get; set; }  // 'draw' | 'upload'
        public string? SignatureFileName { get; set; }
        public string? RequestedModels  { get; set; }
        public string? SignerPosition   { get; set; }
        public string? SignerCompany    { get; set; }
        public string? SignerBranch     { get; set; }

        // Distribution — fetched from dbo.Request via SubmissionSessionId
        public string? FulfillmentMethod { get; set; }  // "Pickup" | "Delivery" | null
        public string? ReceivedByName    { get; set; }  // null unless Pickup

        // Routing — who is assigned to approve this request (null = no approver account yet)
        public int?    AssignedToUserId { get; set; }
        public string? AssignedToName   { get; set; }

        // IT-assisted tracking — which IT user submitted on behalf of the employee
        public int?    SubmittedByUserId { get; set; }
        public string? SubmittedByName   { get; set; }

        // IT Manual Authorization — remarks recorded at submission time
        public string? Remarks { get; set; }

        // Submission origin
        public Guid?   SubmissionSessionId { get; set; }
        public string? SourceApp           { get; set; }   // 'Web' | 'Desktop' from DB column
        public string  Source => SourceApp == "Web" ? "Web Portal" : "Desktop App";

        public bool IsPending  => Status == "Pending";
        public bool IsApproved => Status == "Approved";
        public bool IsRejected => Status == "Rejected";
        public bool IsUsed     => Status == "Used";
    }

    /// <summary>Input model posted from the signature form on the Details page.</summary>
    public class ApproveSignatureInputModel
    {
        public int     AuthorizationId  { get; set; }
        public string  SignatureData    { get; set; } = string.Empty;  // base64 data URL
        public string  SignatureSource  { get; set; } = "draw";        // 'draw' | 'upload'
        public string? SignatureFileName { get; set; }
        public string  RequestedModels  { get; set; } = string.Empty;
        public bool    IsSelfSign       { get; set; }
    }

    /// <summary>Signature + signer metadata passed to the repository.</summary>
    public class ApproveSignatureData
    {
        public string  SignatureData    { get; set; } = string.Empty;
        public string  SignatureSource  { get; set; } = "draw";
        public string? SignatureFileName { get; set; }
        public string  RequestedModels  { get; set; } = string.Empty;
        public string  SignerPosition   { get; set; } = string.Empty;
        public string  SignerCompany    { get; set; } = string.Empty;
        public string  SignerBranch     { get; set; } = string.Empty;
    }

    public class EmployeeAuthorizationStatusViewModel
    {
        public CartridgeAuthorizationViewModel? Authorization { get; set; }
    }

    public class SupervisorAuthorizationQueueViewModel
    {
        public List<CartridgeAuthorizationViewModel> Pending { get; set; } = new();
    }

    public class AuthorizationDetailsViewModel
    {
        public CartridgeAuthorizationViewModel Authorization { get; set; }
    }

    public class RejectAuthorizationInputModel
    {
        public int AuthorizationId { get; set; }
    }

    /// <summary>
    /// A user eligible to authorize an IT-assisted cartridge request.
    /// Returned by GetApproversByScopeAsync; scoped to the employee's branch/department.
    /// </summary>
    public class ITApproverViewModel
    {
        public int    EmpId        { get; set; }
        public string DisplayName  { get; set; } = string.Empty;
        public string Position     { get; set; } = string.Empty;
        public string ApprovalRole { get; set; } = string.Empty;  // Manager | Supervisor | Coordinator
    }
}
