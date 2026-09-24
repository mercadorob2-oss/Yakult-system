-- Manager role: grant access to the Requester Portal.
-- Without this row the "Manager" role sees no Request/s Portal card on the portal dashboard,
-- even though "IT Manager" does. Idempotent: safe to run more than once.

INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
SELECT r.RoleId, p.PortalId
FROM dbo.Role r CROSS JOIN dbo.Portal p
WHERE r.RoleName = 'Manager'
  AND p.PortalKey = 'RequesterPortal'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.RolePortalAccess x
      WHERE x.RoleId = r.RoleId AND x.PortalId = p.PortalId
  );
