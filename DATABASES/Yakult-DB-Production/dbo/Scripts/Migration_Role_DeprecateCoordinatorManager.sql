-- Migration: Deprecate dbo.Role entries for Coordinator (8) and Manager (9)
-- Reason: These roles were incorrectly derived from employee position titles.
--         Cartridge-access approvals for Coordinators/Managers are handled
--         exclusively via dbo.ApprovalRoleTitle (AccountPermissionsPage).
--         dbo.Role is reserved for IT-specific roles only.

-- Step 1: Remove any existing UserRole assignments for these roles
DELETE FROM dbo.UserRole
WHERE RoleId IN (8, 9);

-- Step 2: Deactivate the roles so they no longer appear in the UI or load at login
UPDATE dbo.Role
SET IsActive = 0
WHERE RoleId IN (8, 9)
  AND RoleName IN ('Coordinator', 'Manager'); -- guard against RoleId drift
