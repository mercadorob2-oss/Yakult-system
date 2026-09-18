namespace Yakult.SystemsPortal.Models;

public sealed class AccountRequestsViewModel
{
    public List<AccountRequestListItem> Requests { get; init; } = new();
    public List<ManagedUserListItem> Users { get; init; } = new();
    public List<string> Roles { get; init; } = new();
}

public sealed class AccountRequestListItem
{
    public int AccountRequestId { get; init; }
    public int EmployeeId { get; init; }
    public string EmployeeNumber { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public string WorkEmail { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Branch { get; init; } = string.Empty;
    public string Department { get; init; } = string.Empty;
    public string Position { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public DateTime SubmittedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public string? ReviewedBy { get; init; }
    public string? ReviewRemarks { get; init; }
}

public sealed class ManagedUserListItem
{
    public int UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool IsDeveloper { get; init; }
    public bool MustChangePassword { get; init; }
    public string Roles { get; init; } = string.Empty;
}

public sealed class ReviewAccountRequest
{
    public int AccountRequestId { get; init; }
    public string? Remarks { get; init; }
}

public sealed class UpdateManagedUserRequest
{
    public int UserId { get; init; }
    public bool IsActive { get; init; }
    public string RoleName { get; init; } = string.Empty;
}

public sealed class ResetManagedUserPasswordRequest
{
    public int UserId { get; init; }
    public string Mode { get; init; } = "temporary";
    public string? Password { get; init; }
}
