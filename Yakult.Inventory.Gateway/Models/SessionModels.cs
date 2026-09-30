namespace Yakult.Inventory.Gateway.Models;

public sealed class LoginRequest
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
}

/// <summary>
/// Everything the desktop LoginPage used to load itself at sign-in, so the
/// client can fill AppSession and PermissionResolver without touching SQL.
/// </summary>
public sealed class LoginResponse
{
    public string AccessToken { get; set; } = "";
    public DateTimeOffset ExpiresAt { get; set; }
    public GatewaySession Session { get; set; } = new();
}

public sealed class GatewaySession
{
    public int UserId { get; set; }
    public string UserName { get; set; } = "";
    public string? Email { get; set; }
    public bool IsDeveloper { get; set; }
    public bool IsSuperAdmin { get; set; }
    public List<string> Roles { get; set; } = new();
    public bool NotificationsEnabled { get; set; } = true;

    /// <summary>True when the name matched a department account rather than a user (no welcome box on the desktop).</summary>
    public bool IsDepartmentAccountLogin { get; set; }

    public EmployeeInfo? Employee { get; set; }
    public DepartmentAccountInfo? DepartmentAccount { get; set; }
    public PermissionSnapshot Permissions { get; set; } = new();
}

public sealed class EmployeeInfo
{
    public int EmployeeId { get; set; }
    public string Name { get; set; } = "";
    public string? Position { get; set; }
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
}

public sealed class DepartmentAccountInfo
{
    public int AccountId { get; set; }
    public int? CompanyId { get; set; }
    public int? DepartmentId { get; set; }
    public int? BranchId { get; set; }
}

/// <summary>
/// Same shape as the maps PermissionResolver loads, but the per-user maps
/// only carry the signed-in user's rows.
/// </summary>
public sealed class PermissionSnapshot
{
    /// <summary>False when the load failed: the client then keeps its hardcoded fallback.</summary>
    public bool Loaded { get; set; }

    /// <summary>RoleName → PortalKeys (dbo.RolePortalAccess).</summary>
    public Dictionary<string, List<string>> RolePortals { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>PortalKey → IsGranted for this user (dbo.UserPortalAccess).</summary>
    public Dictionary<string, bool> UserPortals { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>RoleName → PermissionType → ItemKey → IsGranted (dbo.RolePermissionItem).</summary>
    public Dictionary<string, Dictionary<string, Dictionary<string, bool>>> RoleItems { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>PermissionType → ItemKey → IsGranted for this user (dbo.UserPermissionItem).</summary>
    public Dictionary<string, Dictionary<string, bool>> UserItems { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
