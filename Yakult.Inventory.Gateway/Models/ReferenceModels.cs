namespace Yakult.Inventory.Gateway.Models;

// Wire shapes for the phase 2 reference-data endpoints. Property names match the
// desktop DTOs in Yakult.Inventory.App (Pages\Dtos.cs, ConditionDto.cs,
// Repositories\HolidayRepository.cs, Repositories\CategoryRepository.cs) so the
// desktop can deserialize straight into its own classes. Keep them in sync.

public sealed class BranchModel
{
    public int BranchId { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public DateTime DateCreated { get; set; }
    public int CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? DateModified { get; set; }
    public int? ModifiedByUserId { get; set; }
    public string? ModifiedByName { get; set; }
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public string? CompanyName { get; set; }
    public string? DepartmentName { get; set; }
    public bool IsFactory { get; set; }
    public bool IsDepot { get; set; }
    public bool IsCenter { get; set; }
    public bool IsDistributor { get; set; }
    public string? CenterRegion { get; set; }
}

public sealed class BulkAssignDepartmentRequest
{
    public List<int> BranchIds { get; set; } = new();
    public int TargetDeptId { get; set; }
}

public sealed class DepartmentModel
{
    public string? Name { get; set; }
    public string? Section { get; set; }
    public string? Description { get; set; }
}

public sealed class ConditionModel
{
    public int ConditionId { get; set; }
    public string ConditionName { get; set; } = "";
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ItemCategoryModel
{
    public int CategoryId { get; set; }
    public string? Name { get; set; }
    public bool Active { get; set; } = true;
    public DateTime DateCreated { get; set; }
    public int CreatedBy { get; set; }
    public string? CreatedByName { get; set; }
    public bool IsArchived { get; set; }
    public int ItemCount { get; set; }
}

public sealed class CategoryItemLocationModel
{
    public int ItemId { get; set; }
    public string ItemName { get; set; } = "";
    public string? SerialNumber { get; set; }
    public int StockOnHand { get; set; }
    public bool Active { get; set; }
    public string CurrentLocation { get; set; } = "";
}

public sealed class VendorModel
{
    public int VendorId { get; set; }
    public string? VendorName { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsArchived { get; set; }
    public bool IsRefiller { get; set; }
    public bool IsDisposer { get; set; }
    public bool IsBuyer { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? TIN { get; set; }
}

public sealed class ArchiveRequest
{
    public string? Reason { get; set; }
}

public sealed class HolidayModel
{
    public int HolidayId { get; set; }
    public string? HolidayName { get; set; }
    public DateTime HolidayDate { get; set; }
    public string? HolidayType { get; set; }
    public bool IsRecurring { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
    public int? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
}

public sealed class SmtpEnabledRequest
{
    public bool Enabled { get; set; }
}

/// <summary>The (Success, Message) tuple several desktop delete methods return.</summary>
public sealed record OperationResult(bool Success, string Message);
