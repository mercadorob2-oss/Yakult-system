namespace Inventory.RequestPortal.Models.ViewModels
{
    public class PendingApprovalViewModel
    {
        public int ApprovalId { get; set; }
        public Guid ApprovalToken { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string EmployeePosition { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime RequestedAt { get; set; }
        public string? RecentCartridgeModels { get; set; }
        public string? EmployeeEmail { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class ApprovalQueueViewModel
    {
        public List<PendingApprovalViewModel>          PendingApprovals       { get; set; } = new();
        public List<CartridgeAuthorizationViewModel>   PendingAuthorizations  { get; set; } = new();
        public List<CartridgeAuthorizationViewModel>   ApprovedAuthorizations { get; set; } = new();
    }

    public class ApproveRequestModel
    {
        public int ApprovalId { get; set; }
        public Guid ApprovalToken { get; set; }
        public string? Notes { get; set; }
        // Signature as base64 PNG from canvas draw
        public string? SignatureBase64 { get; set; }
        // Signature via file upload
        public IFormFile? SignatureFile { get; set; }
    }

    public class RejectRequestModel
    {
        public int ApprovalId { get; set; }
        public Guid ApprovalToken { get; set; }
        public string Notes { get; set; } = string.Empty;
    }
}
