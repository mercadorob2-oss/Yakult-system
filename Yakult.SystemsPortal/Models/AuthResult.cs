namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Result from authentication attempt.
/// Ported from Inventory.RequestPortal.Models.AuthResult
/// </summary>
public class AuthResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsDeveloper { get; set; }
    public int LevelRank { get; set; }
    public bool MustChangePassword { get; set; }
    public int? EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeePosition { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public List<string> Roles { get; set; } = new List<string>();

    public bool IsDepartmentAccountSession { get; set; }
    public int? DepartmentAccountId { get; set; }
    public int? DepartmentAccountCompanyId { get; set; }
    public string? DepartmentAccountCompanyName { get; set; }
    public int? DepartmentAccountDepartmentId { get; set; }
    public string? DepartmentAccountDepartmentName { get; set; }
    public int? DepartmentAccountBranchId { get; set; }
    public string? DepartmentAccountBranchName { get; set; }
}
