namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// Internal DTO for request data.
    /// COPIED FROM: Yakult.Inventory.App/Pages/Dtos.cs (RequestDto)
    /// Used by Repository layer only - not exposed to Views.
    /// </summary>
    public class RequestDto
    {
        public int ReqId { get; set; }
        public DateTime DateRequested { get; set; }
        public string? Description { get; set; }
        public string? Remarks { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? EntryType { get; set; }
        public int Quantity { get; set; }
        public int IssuedQty { get; set; }
        public decimal UnitPrice { get; set; }
        public DateTime DateCreated { get; set; }
        public int CreatedByUserId { get; set; }
        public int ModifiedByUserId { get; set; }

        // Foreign Keys
        public int ItemId { get; set; }

        // Nullable: NULL for department-level requests (no specific employee), which instead
        // carry ComId/BranchId/DeptId directly.
        public int? EmpId { get; set; }
        public int? SetId { get; set; }

        // Org-unit assignment, used only when EmpId is NULL (department-level requests).
        public int? ComId { get; set; }
        public int? DeptId { get; set; }
        public int? BranchId { get; set; }

        // Submission tracking (nullable for backward compatibility)
        public Guid? SubmissionSessionId { get; set; }

        // Designated receiver for PICKUP fulfillment (NULL for DELIVERY / legacy records)
        public int? ReceivedById { get; set; }

        // Explicit workflow ownership: 'CartridgeManagement' (pure-cartridge submissions) or
        // 'RequestSetManagement' (mixed/Ink/Printhead/Toner submissions). Assigned once at
        // submission time by RequesterPortalService — see dbo.Request.WorkflowType.
        public string? WorkflowType { get; set; }

        // Display names (for UI reference, not saved to DB)
        public string? EmployeeName { get; set; }
        public string? ItemName { get; set; }
        public string? ModelNumber { get; set; }
        public string? Category { get; set; }
        public string? SerialNumber { get; set; }

        // Organization data (from employee master record)
        // Used by fulfillment and SMTP logic - single source of truth
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
    }
}
