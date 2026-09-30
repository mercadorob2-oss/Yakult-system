using System.Data;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using Yakult.Inventory.Gateway.Data;

namespace Yakult.Inventory.Gateway.Security;

/// <summary>
/// Server-side write permissions. The desktop's PermissionResolver checks are only
/// UI hints; these are the rules the gateway actually enforces. They mirror where
/// each screen lives in the desktop app:
///   - Master data (company, branch, department, category): Inventory System portal
///   - Vendors: Inventory System or Cartridge Management portal (both open the vendor screens)
///   - Holidays: Admin Portal (Developer or SuperAdmin)
///   - SMTP send switch: Admin Portal, or the main app's Email/SMTP menu (Admin role)
/// Portal access uses the same rules as PermissionResolver.HasPortalAccess:
/// Developer = everything, a per-user UserPortalAccess row wins over roles,
/// otherwise any role with a RolePortalAccess row. Viewer-only users never write.
/// </summary>
public sealed class AccessPolicy
{
    public const string DeveloperClaim = "dev";
    public const string SuperAdminClaim = "superadmin";

    public const string InventorySystem = "InventorySystem";
    public const string CartridgeManagement = "CartridgeManagement";

    private readonly DbSession _db;

    public AccessPolicy(DbSession db)
    {
        _db = db;
    }

    private bool IsDeveloper => _db.User.HasClaim(DeveloperClaim, "true");
    private bool IsSuperAdmin => _db.User.HasClaim(SuperAdminClaim, "true");
    private List<string> Roles => _db.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

    private bool IsViewerOnly
    {
        get
        {
            var roles = Roles;
            return roles.Count == 1 && roles[0].Equals("Viewer", StringComparison.OrdinalIgnoreCase);
        }
    }

    public Task<bool> CanEditMasterDataAsync(CancellationToken ct) => CanWriteInPortalsAsync(ct, InventorySystem);

    public Task<bool> CanEditVendorsAsync(CancellationToken ct) => CanWriteInPortalsAsync(ct, InventorySystem, CartridgeManagement);

    public bool CanEditHolidays() => IsDeveloper || IsSuperAdmin;

    public bool CanEditSmtpSwitch() =>
        IsDeveloper || IsSuperAdmin || Roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase));

    private async Task<bool> CanWriteInPortalsAsync(CancellationToken ct, params string[] portalKeys)
    {
        if (IsDeveloper) return true;
        if (IsViewerOnly) return false;
        if (IsSuperAdmin) return true; // Admin Portal users manage reference data too

        foreach (var portal in portalKeys)
        {
            if (await HasPortalAccessAsync(portal, ct))
                return true;
        }
        return false;
    }

    private async Task<bool> HasPortalAccessAsync(string portalKey, CancellationToken ct)
    {
        await using var con = await _db.OpenAsync(ct);

        // A per-user row is authoritative in either direction.
        await using (var cmd = new SqlCommand(@"
            SELECT TOP 1 upa.IsGranted
            FROM dbo.UserPortalAccess upa
            INNER JOIN dbo.Portal p ON p.PortalId = upa.PortalId
            WHERE upa.UserId = @UserId AND p.PortalKey = @PortalKey AND p.IsActive = 1", con))
        {
            cmd.Parameters.Add("@UserId", SqlDbType.Int).Value = _db.UserId;
            cmd.Parameters.Add("@PortalKey", SqlDbType.NVarChar, 100).Value = portalKey;
            var userOverride = await cmd.ExecuteScalarAsync(ct);
            if (userOverride is bool granted)
                return granted;
        }

        var roles = Roles;
        if (roles.Count == 0)
            return false;

        var roleParams = string.Join(", ", roles.Select((_, i) => "@Role" + i));
        await using (var cmd = new SqlCommand($@"
            SELECT CASE WHEN EXISTS (
                SELECT 1
                FROM dbo.RolePortalAccess rpa
                INNER JOIN dbo.Role   r ON r.RoleId   = rpa.RoleId
                INNER JOIN dbo.Portal p ON p.PortalId = rpa.PortalId
                WHERE r.IsActive = 1 AND p.IsActive = 1
                  AND p.PortalKey = @PortalKey
                  AND r.RoleName IN ({roleParams})
            ) THEN 1 ELSE 0 END", con))
        {
            cmd.Parameters.Add("@PortalKey", SqlDbType.NVarChar, 100).Value = portalKey;
            for (int i = 0; i < roles.Count; i++)
                cmd.Parameters.Add("@Role" + i, SqlDbType.NVarChar, 100).Value = roles[i];
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
        }
    }
}
