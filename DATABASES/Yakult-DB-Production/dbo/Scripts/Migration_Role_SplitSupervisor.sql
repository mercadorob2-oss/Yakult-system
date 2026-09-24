-- Split the Supervisor role into IT Supervisor and Supervisor.
--
--   IT Supervisor : the existing "Supervisor" role, renamed. It keeps its RoleId, so every
--                   dbo.RolePortalAccess, dbo.RolePermissionItem and dbo.UserRole row already
--                   pointing at it carries over unchanged (Inventory System, IT Call Monitoring,
--                   Consumable Management, Borrow Items, Reports).
--   Supervisor    : a NEW role with access to the Request Portal only.
--
-- Run this FIRST, then Migration_UserRole_SplitSupervisorAssignments.sql to decide who gets which.
-- Idempotent: safe to run more than once.

SET NOCOUNT ON;

-- 1. Rename the existing role. Skipped once IT Supervisor exists, so a re-run never renames
--    the new Supervisor role created in step 2.
IF EXISTS (SELECT 1 FROM dbo.Role WHERE RoleName = 'Supervisor')
   AND NOT EXISTS (SELECT 1 FROM dbo.Role WHERE RoleName = 'IT Supervisor')
BEGIN
    UPDATE dbo.Role
    SET RoleName    = 'IT Supervisor',
        Description = 'IT supervisor - Inventory, IT Call Monitoring, Consumable Management, Borrow Items and Reports'
    WHERE RoleName = 'Supervisor';

    PRINT 'Renamed role Supervisor to IT Supervisor.';
END

-- 2. Create the new Supervisor role.
IF NOT EXISTS (SELECT 1 FROM dbo.Role WHERE RoleName = 'Supervisor')
BEGIN
    INSERT INTO dbo.Role (RoleName, Description, IsActive)
    VALUES ('Supervisor', 'Supervisor - Request Portal access only', 1);

    PRINT 'Created role Supervisor.';
END

-- 3. New Supervisor role: Request Portal only.
INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Supervisor'
  AND p.PortalKey = 'RequesterPortal'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );

-- Result check: each role and the portals it grants.
SELECT r.RoleName, p.PortalKey
FROM dbo.Role r
LEFT JOIN dbo.RolePortalAccess a ON a.RoleId = r.RoleId
LEFT JOIN dbo.Portal p ON p.PortalId = a.PortalId
WHERE r.RoleName IN ('IT Supervisor', 'Supervisor')
ORDER BY r.RoleName, p.PortalKey;
