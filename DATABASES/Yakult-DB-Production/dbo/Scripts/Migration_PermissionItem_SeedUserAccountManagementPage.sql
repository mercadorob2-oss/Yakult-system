-- =============================================================================
-- Migration: Seed UserAccountManagementPage PermissionItem + nav entries, and
--            restrict it to Super Admins
-- Date: 2026-09-25
--
-- Purpose:
--   The Account Management page (user accounts, roles, Developer link) is Super
--   Admin only. The app enforces that in code (AppSession.IsSuperAdmin) at both
--   entry points:
--     - Admin Portal  > Account Management > Account Management
--     - Inventory System > Admin > Users
--   This script puts the same rule in the database page-permission tables, so it
--   shows (and can be managed) on Admin Portal > User Access > Pages like any
--   other restrictable page:
--     1. dbo.PermissionItem  ('Page', 'UserAccountManagementPage')
--     2. dbo.PageNavEntry    one row per entry point above
--     3. dbo.UserPermissionItem  IsGranted = 0 for every active user who is NOT a
--        Super Admin (Developers are skipped: they bypass page permissions and are
--        hidden from the User Access page).
--
--   The page opens only when BOTH checks pass: IsSuperAdmin = 1 AND the page is not
--   restricted for that user here. So a Super Admin can still be blocked from it on
--   the Pages tab, and ticking it for a non-Super Admin does NOT open it for them.
--
--   Note: a user promoted to Super Admin later keeps the deny row written here
--   until it is ticked on the Pages tab (or the row is deleted).
--
-- Run after: Migration_PermissionItem_CreateTables.sql,
--            Migration_PageNavEntry_CreateAndSeed.sql
-- Safe to re-run: existing rows (including overrides an admin changed on the
-- Pages tab) are never modified.
-- =============================================================================

-- 1. PermissionItem
IF NOT EXISTS (
    SELECT 1 FROM dbo.PermissionItem
    WHERE PermissionType = 'Page' AND ItemKey = 'UserAccountManagementPage'
)
BEGIN
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName)
    VALUES ('Page', 'UserAccountManagementPage', 'Account Management');

    PRINT 'PermissionItem UserAccountManagementPage seeded.';
END
ELSE
BEGIN
    PRINT 'PermissionItem UserAccountManagementPage already exists. Skipped.';
END
GO

-- 2. PageNavEntry (one per place the page is reachable from)
DECLARE @Nav TABLE (PortalKey VARCHAR(50), MenuGroup VARCHAR(100), DisplayName VARCHAR(150), SortOrder INT);
INSERT INTO @Nav (PortalKey, MenuGroup, DisplayName, SortOrder) VALUES
    ('AdminPortal',     'Account Management', 'Account Management', 2),
    ('InventorySystem', 'Admin',              'Users',              1);

INSERT INTO dbo.PageNavEntry (PermissionItemId, PortalKey, MenuGroup, DisplayName, SortOrder)
SELECT pi.PermissionItemId, n.PortalKey, n.MenuGroup, n.DisplayName, n.SortOrder
FROM @Nav n
CROSS JOIN dbo.PermissionItem pi
WHERE pi.PermissionType = 'Page'
  AND pi.ItemKey = 'UserAccountManagementPage'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.PageNavEntry e
      WHERE e.PermissionItemId = pi.PermissionItemId
        AND e.PortalKey = n.PortalKey
        AND e.MenuGroup = n.MenuGroup
  );

PRINT CONCAT('PageNavEntry rows added for UserAccountManagementPage: ', @@ROWCOUNT);
GO

-- 3. Restrict for everyone who is not a Super Admin
INSERT INTO dbo.UserPermissionItem (UserId, PermissionItemId, IsGranted, ModifiedByUserId)
SELECT u.UserId, pi.PermissionItemId, 0, NULL
FROM dbo.[User] u
CROSS JOIN dbo.PermissionItem pi
WHERE pi.PermissionType = 'Page'
  AND pi.ItemKey = 'UserAccountManagementPage'
  AND u.IsActive = 1
  AND ISNULL(u.IsSuperAdmin, 0) = 0
  AND ISNULL(u.IsDeveloper, 0) = 0
  AND NOT EXISTS (
      SELECT 1 FROM dbo.UserPermissionItem x
      WHERE x.UserId = u.UserId AND x.PermissionItemId = pi.PermissionItemId
  );

PRINT CONCAT('UserAccountManagementPage restricted for non-Super Admin users: ', @@ROWCOUNT);
GO
