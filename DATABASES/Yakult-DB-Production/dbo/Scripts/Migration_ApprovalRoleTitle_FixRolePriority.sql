-- Migration: Fix RolePriority in dbo.ApprovalRoleTitle
-- All entries had RolePriority = 1 (or 2/3 for sub-variants), causing the routing
-- to assign via UserId ASC tie-breaker instead of actual rank.
-- Lower RolePriority = higher rank = routed first.

UPDATE dbo.ApprovalRoleTitle SET RolePriority = 1  WHERE PositionTitle = 'MANAGER';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 2  WHERE PositionTitle = 'ASST. MANAGER';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 3  WHERE PositionTitle = 'JR. ASST. MANAGER';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 4  WHERE PositionTitle = 'ACTING JR. ASST. MANAGER';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 5  WHERE PositionTitle = 'SUPERVISOR';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 6  WHERE PositionTitle = 'ACTING HEAD DEPARTMENT';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 7  WHERE PositionTitle = 'COORDINATOR';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 8  WHERE PositionTitle = 'ACCOUNT COORDINATOR';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 9  WHERE PositionTitle = 'ACTING ACCOUNT COORDINATOR';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 10 WHERE PositionTitle = 'ASST. COORDINATOR';
UPDATE dbo.ApprovalRoleTitle SET RolePriority = 11 WHERE PositionTitle = 'LADY COORDINATOR';

-- Verify
SELECT TitleId, PositionTitle, ApprovalRole, RolePriority
FROM dbo.ApprovalRoleTitle
ORDER BY RolePriority;
