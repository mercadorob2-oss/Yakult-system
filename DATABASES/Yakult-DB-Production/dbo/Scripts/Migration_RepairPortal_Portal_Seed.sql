-- Migration: Register the Repair Technician Portal in dbo.Portal and seed dbo.RolePortalAccess.
-- PortalKey must match the C# PermissionResolver.Portal enum member name exactly
-- (RepairTechnicianPortal). The app re-reads this table on each login (cached per session).
-- Run AFTER Migration_Portal_CreateAndSeed.sql (dbo.Portal / dbo.RolePortalAccess must already exist).

-- ── 1. Seed the portal (idempotent) ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'RepairTechnicianPortal')
    INSERT INTO dbo.Portal (PortalKey, DisplayName) VALUES ('RepairTechnicianPortal', 'Repair Technician Portal');

-- ── 2. Seed role access (idempotent) ─────────────────────────────────────────
-- Ships to Developer, Admin, IT Manager, and Tech Support by default.

INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName IN ('Developer', 'Admin', 'IT Manager', 'Tech Support')
  AND p.PortalKey = 'RepairTechnicianPortal'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );
