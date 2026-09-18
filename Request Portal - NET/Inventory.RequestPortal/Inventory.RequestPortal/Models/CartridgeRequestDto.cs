namespace Inventory.RequestPortal.Models
{
    /// <summary>
    /// A pending cartridge exchange request awaiting first-pass IT fulfillment.
    /// PORTED FROM: Yakult.Inventory.App/Forms/CartridgeManagement/CartridgeManagementForm.cs (CartridgeRequestDto)
    /// </summary>
    public class CartridgeRequestDto
    {
        public int ReqId { get; set; }
        public int ItemId { get; set; }
        public int? CartridgeModelId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string? ModelNumber { get; set; }
        public string? TypedModelNumber { get; set; }
        public int? RequestModelId { get; set; }
        public int Quantity { get; set; }
        public string? ConditionType { get; set; }
        public string PhysicalCondition { get; set; } = "Good";
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DateCreated { get; set; }
        public int EmpId { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string? BranchName { get; set; }
        public string? DepartmentName { get; set; }
        public Guid? SubmissionSessionId { get; set; }
        public int OriginalSubmissionCount { get; set; }
        public string? Description { get; set; }
        public string DistributionMethod { get; set; } = "N/A";
        public string? ReceivedByName { get; set; }
        public string? AdditionalRemarks { get; set; }

        public string DisplayModel => TypedModelNumber ?? ModelNumber ?? "N/A";
    }

    /// <summary>
    /// A fulfilled portal cartridge Set for the history page.
    /// PORTED FROM: Yakult.Inventory.App/Models (FulfilledCartridgeRowDto)
    /// </summary>
    public class FulfilledCartridgeRowDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; } = string.Empty;
        public DateTime FulfilledAt { get; set; }
        public int ReqId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string CartridgeModel { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public string ReceivedByName { get; set; } = string.Empty;
        public int TotalIssuedQty { get; set; }

        public string FulfilledAtDisplay => FulfilledAt == DateTime.MinValue ? "—" : FulfilledAt.ToString("MM/dd/yyyy");
    }

    public class FulfilledCartridgeModelLineDto
    {
        public string CartridgeModel { get; set; } = string.Empty;
        public int RequestedQty { get; set; }
        public int GoodEmptyQty { get; set; }
        public int DamagedEmptyQty { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class FulfilledCartridgeDetailDto
    {
        public int SetId { get; set; }
        public string SetCode { get; set; } = string.Empty;
        public DateTime FulfilledAt { get; set; }
        public int ReqId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string CompanyName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public string ReceivedByName { get; set; } = string.Empty;
        public int TotalIssuedQty { get; set; }
        public List<FulfilledCartridgeModelLineDto> Models { get; set; } = new();

        public string FulfilledAtDisplay => FulfilledAt == DateTime.MinValue ? "—" : FulfilledAt.ToString("MM/dd/yyyy");
    }
}
