using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Yakult.Inventory.App.Core;
using Yakult.Inventory.App.Session;

namespace Yakult.Inventory.App.Security
{
    /// <summary>
    /// Centralized permission resolver for role-based portal access control.
    /// Access rules are loaded from dbo.RolePortalAccess at login and cached for
    /// the session. The hardcoded fallback is used only if the DB load fails.
    /// To change access: edit rows in dbo.RolePortalAccess — no code changes required.
    /// </summary>
    public static class PermissionResolver
    {
        public enum Portal
        {
            InventorySystem,
            CallITMonitoring,
            RequesterPortal,
            CartridgeManagement,
            BorrowItems,
            Reports,
            AdminPortal,
            RepairTechnicianPortal
        }

        // ── DB-loaded map: role name → set of PortalKey strings ───────────────
        private static Dictionary<string, HashSet<string>> _dbMap;

        // ── DB-loaded map: userId → PortalKey → IsGranted (explicit per-user override,
        // authoritative over role access: true grants even without role, false denies
        // even with role) ──────────────────────────────────────────────────────────
        private static Dictionary<int, Dictionary<string, bool>> _userPortalMap;

        // ── DB-loaded map: userId → PermissionType → ItemKey → IsGranted
        // (fine-grained overrides for pages / item categories / actions / future types;
        // default-allow — absence of an entry means allowed) ───────────────────────────
        private static Dictionary<int, Dictionary<string, Dictionary<string, bool>>> _userItemOverrides;

        // ── DB-loaded map: roleName → PermissionType → ItemKey → IsGranted
        // (role-level baseline for the same fine-grained overrides, from dbo.RolePermissionItem;
        // a user-level override above always wins over this) ───────────────────────────────
        private static Dictionary<string, Dictionary<string, Dictionary<string, bool>>> _roleItemOverrides;

        // ── Hardcoded fallback (used only if LoadAsync has not been called or failed) ──
        private static readonly Dictionary<string, HashSet<string>> _fallback =
            new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            { "Developer",        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","CallITMonitoring","RequesterPortal","CartridgeManagement","BorrowItems","Reports","AdminPortal","RepairTechnicianPortal" } },
            { "Admin",            new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","CallITMonitoring","RequesterPortal","CartridgeManagement","BorrowItems","Reports","RepairTechnicianPortal" } },
            { "InventoryManager", new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","Reports" } },
            { "Requester",        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "RequesterPortal" } },
            { "Viewer",           new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","CallITMonitoring","RequesterPortal" } },
            { "IT Manager",       new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","CallITMonitoring","CartridgeManagement","BorrowItems","Reports","AdminPortal","RepairTechnicianPortal" } },
            { "Supervisor",       new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "InventorySystem","CallITMonitoring","CartridgeManagement","BorrowItems","Reports" } },
            { "Tech Support",     new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "CallITMonitoring","CartridgeManagement","BorrowItems","RepairTechnicianPortal" } }
        };

        private static Dictionary<string, HashSet<string>> ActiveMap => _dbMap ?? _fallback;

        // ── DB loading ────────────────────────────────────────────────────────

        /// <summary>
        /// Loads role → portal access from dbo.RolePortalAccess.
        /// Idempotent: skips if already loaded. Call at login alongside role loading.
        /// </summary>
        public static async Task LoadAsync()
        {
            if (_dbMap != null) return; // already loaded this session

            try
            {
                string cs = DatabaseConfig.ConnectionString;
                if (string.IsNullOrEmpty(cs)) return;

                var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

                const string sql = @"
                    SELECT r.RoleName, p.PortalKey
                    FROM dbo.RolePortalAccess rpa
                    INNER JOIN dbo.Role   r ON r.RoleId   = rpa.RoleId
                    INNER JOIN dbo.Portal p ON p.PortalId = rpa.PortalId
                    WHERE r.IsActive = 1 AND p.IsActive = 1";

                using (var con = new SqlConnection(cs))
                {
                    await con.OpenAsync();
                    using (var cmd = new SqlCommand(sql, con))
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            string roleName  = reader.GetString(0);
                            string portalKey = reader.GetString(1);

                            if (!map.ContainsKey(roleName))
                                map[roleName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                            map[roleName].Add(portalKey);
                        }
                    }
                }

                _dbMap = map;

                // Load user-level portal overrides (authoritative over role access)
                var userMap = new Dictionary<int, Dictionary<string, bool>>();

                const string userSql = @"
                    SELECT upa.UserId, p.PortalKey, upa.IsGranted
                    FROM dbo.UserPortalAccess upa
                    INNER JOIN dbo.Portal p ON p.PortalId = upa.PortalId
                    WHERE p.IsActive = 1";

                using (var con2 = new SqlConnection(cs))
                {
                    await con2.OpenAsync();
                    using (var cmd2 = new SqlCommand(userSql, con2))
                    using (var r2 = await cmd2.ExecuteReaderAsync())
                    {
                        while (await r2.ReadAsync())
                        {
                            int    userId    = r2.GetInt32(0);
                            string portalKey = r2.GetString(1);
                            bool   isGranted = r2.GetBoolean(2);

                            if (!userMap.ContainsKey(userId))
                                userMap[userId] = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                            userMap[userId][portalKey] = isGranted;
                        }
                    }
                }

                _userPortalMap = userMap;

                // Load fine-grained per-user overrides (pages / item categories / actions / future types)
                var itemMap = new Dictionary<int, Dictionary<string, Dictionary<string, bool>>>();

                const string itemSql = @"
                    SELECT upi.UserId, pi.PermissionType, pi.ItemKey, upi.IsGranted
                    FROM dbo.UserPermissionItem upi
                    INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = upi.PermissionItemId
                    WHERE pi.IsActive = 1";

                using (var con3 = new SqlConnection(cs))
                {
                    await con3.OpenAsync();
                    using (var cmd3 = new SqlCommand(itemSql, con3))
                    using (var r3 = await cmd3.ExecuteReaderAsync())
                    {
                        while (await r3.ReadAsync())
                        {
                            int    userId         = r3.GetInt32(0);
                            string permissionType = r3.GetString(1);
                            string itemKey        = r3.GetString(2);
                            bool   isGranted      = r3.GetBoolean(3);

                            if (!itemMap.TryGetValue(userId, out var byType))
                                itemMap[userId] = byType = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);

                            if (!byType.TryGetValue(permissionType, out var byKey))
                                byType[permissionType] = byKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                            byKey[itemKey] = isGranted;
                        }
                    }
                }

                _userItemOverrides = itemMap;

                // Load role-level baseline for the same fine-grained overrides
                var roleItemMap = new Dictionary<string, Dictionary<string, Dictionary<string, bool>>>(StringComparer.OrdinalIgnoreCase);

                const string roleItemSql = @"
                    SELECT r.RoleName, pi.PermissionType, pi.ItemKey, rpi.IsGranted
                    FROM dbo.RolePermissionItem rpi
                    INNER JOIN dbo.Role r ON r.RoleId = rpi.RoleId
                    INNER JOIN dbo.PermissionItem pi ON pi.PermissionItemId = rpi.PermissionItemId
                    WHERE r.IsActive = 1 AND pi.IsActive = 1";

                using (var con4 = new SqlConnection(cs))
                {
                    await con4.OpenAsync();
                    using (var cmd4 = new SqlCommand(roleItemSql, con4))
                    using (var r4 = await cmd4.ExecuteReaderAsync())
                    {
                        while (await r4.ReadAsync())
                        {
                            string roleName       = r4.GetString(0);
                            string permissionType = r4.GetString(1);
                            string itemKey        = r4.GetString(2);
                            bool   isGranted      = r4.GetBoolean(3);

                            if (!roleItemMap.TryGetValue(roleName, out var byType))
                                roleItemMap[roleName] = byType = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);

                            if (!byType.TryGetValue(permissionType, out var byKey))
                                byType[permissionType] = byKey = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

                            byKey[itemKey] = isGranted;
                        }
                    }
                }

                _roleItemOverrides = roleItemMap;
            }
            catch (Exception ex)
            {
                // DB load failed — app will use the hardcoded fallback
                System.Diagnostics.Debug.WriteLine($"PermissionResolver.LoadAsync failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Clears the cached map so the next LoadAsync call re-reads from DB.
        /// Call after modifying dbo.RolePortalAccess in an admin UI.
        /// </summary>
        public static void Invalidate()
        {
            _dbMap             = null;
            _userPortalMap     = null;
            _userItemOverrides = null;
            _roleItemOverrides = null;
        }

        // ── Access checks ─────────────────────────────────────────────────────

        /// <summary>
        /// Returns true if the user's roles grant access to the given portal.
        /// Developer flag bypasses all checks (full access).
        /// </summary>
        public static bool HasPortalAccess(List<string> userRoles, Portal portal)
        {
            // IsDeveloper = unrestricted
            if (AppSession.IsDeveloper) return true;

            // Admin Portal requires Developer or Super Admin — role-based access cannot grant it
            if (portal == Portal.AdminPortal)
                return AppSession.IsSuperAdmin;

            string key = portal.ToString();

            // User-level override — authoritative over role access, in either direction
            if (_userPortalMap != null &&
                _userPortalMap.TryGetValue(AppSession.CurrentUserId, out var userPortals) &&
                userPortals.TryGetValue(key, out var isGranted))
                return isGranted;

            if (userRoles == null || userRoles.Count == 0)
                return portal == Portal.RequesterPortal;

            foreach (var role in userRoles)
            {
                if (ActiveMap.TryGetValue(role, out var portals) && portals.Contains(key))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Returns all portals accessible by the user's roles.
        /// </summary>
        public static List<Portal> GetAccessiblePortals(List<string> userRoles)
        {
            var result = new HashSet<Portal>();

            if (AppSession.IsDeveloper)
                return Enum.GetValues(typeof(Portal)).Cast<Portal>().ToList();

            if (userRoles == null || userRoles.Count == 0)
            {
                result.Add(Portal.RequesterPortal);
                return result.ToList();
            }

            foreach (var role in userRoles)
            {
                if (ActiveMap.TryGetValue(role, out var portalKeys))
                {
                    foreach (var key in portalKeys)
                    {
                        if (Enum.TryParse(key, ignoreCase: true, out Portal p))
                            result.Add(p);
                    }
                }
            }

            return result.ToList();
        }

        /// <summary>
        /// Returns true if the user has only the Viewer role (read-only access).
        /// </summary>
        public static bool IsReadOnly(List<string> userRoles)
        {
            if (userRoles == null || userRoles.Count == 0) return false;
            return userRoles.Count == 1 && userRoles.Contains("Viewer");
        }

        /// <summary>
        /// Returns true if the user has admin privileges (Admin role or Developer flag).
        /// </summary>
        public static bool IsAdmin(List<string> userRoles, bool isDeveloper)
        {
            if (isDeveloper) return true;
            if (userRoles == null || userRoles.Count == 0) return false;
            return userRoles.Contains("Admin", StringComparer.OrdinalIgnoreCase);
        }

        // ── Fine-grained overrides (pages / item categories / actions / future types) ──

        /// <summary>Returns true unless this page is explicitly restricted for the current user.</summary>
        public static bool HasPageAccess(string pageKey) => CheckItemOverride("Page", pageKey);

        /// <summary>Returns true unless this item category is explicitly restricted for the current user.</summary>
        public static bool CanUseItemCategory(string categoryKey) => CheckItemOverride("ItemCategory", categoryKey);

        /// <summary>Returns true unless this action is explicitly restricted for the current user.</summary>
        public static bool CanPerformAction(string actionKey) => CheckItemOverride("Action", actionKey);

        private static bool CheckItemOverride(string permissionType, string itemKey)
        {
            if (AppSession.IsDeveloper) return true;
            if (string.IsNullOrEmpty(itemKey)) return true;

            // User-level override is always authoritative (grant or deny), regardless of role.
            if (_userItemOverrides != null &&
                _userItemOverrides.TryGetValue(AppSession.CurrentUserId, out var byType) &&
                byType.TryGetValue(permissionType, out var byKey) &&
                byKey.TryGetValue(itemKey, out var isGranted))
                return isGranted;

            // Role-level baseline: if ANY of the user's roles restricts this item, it's
            // restricted (most-restrictive-role-wins), unless a user-level override above
            // already granted it back.
            if (_roleItemOverrides != null && AppSession.CurrentUserRoles != null)
            {
                foreach (var role in AppSession.CurrentUserRoles)
                {
                    if (_roleItemOverrides.TryGetValue(role, out var roleByType) &&
                        roleByType.TryGetValue(permissionType, out var roleByKey) &&
                        roleByKey.TryGetValue(itemKey, out var roleGranted) &&
                        !roleGranted)
                        return false;
                }
            }

            return true; // default-allow
        }

        public static string GetDefaultRole() => "Requester";

        /// <summary>
        /// Returns all role names present in the active map (DB or fallback).
        /// </summary>
        public static List<string> GetSupportedRoles() => ActiveMap.Keys.ToList();
    }
}
