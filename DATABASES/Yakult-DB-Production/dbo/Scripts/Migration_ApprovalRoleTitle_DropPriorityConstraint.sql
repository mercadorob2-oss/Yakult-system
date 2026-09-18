-- Migration: Drop the RolePriority CHECK constraint and fix LADY COORDINATOR priority.
-- The constraint capped RolePriority at 10, but we now have 11 active positions.
-- Previous migration already set priorities 1-10 correctly; only LADY COORDINATOR failed.

IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_ApprovalRoleTitle_Priority'
      AND parent_object_id = OBJECT_ID('dbo.ApprovalRoleTitle')
)
BEGIN
    ALTER TABLE dbo.ApprovalRoleTitle DROP CONSTRAINT CK_ApprovalRoleTitle_Priority;
    PRINT 'Dropped CK_ApprovalRoleTitle_Priority';
END

UPDATE dbo.ApprovalRoleTitle SET RolePriority = 11 WHERE PositionTitle = 'LADY COORDINATOR';

-- Verify final state
SELECT TitleId, PositionTitle, ApprovalRole, RolePriority
FROM dbo.ApprovalRoleTitle
ORDER BY RolePriority;
