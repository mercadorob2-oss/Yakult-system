-- Give existing accounts that have NO role yet a system role based on the employee's position.
--
--   Manager positions (MANAGER, ASST. MANAGER, JR. ASST. MANAGER, ...) -> Manager
--   SUPERVISOR                                                          -> Supervisor
--
-- "IT Manager" and "IT Supervisor" are IT Department roles and are never assigned from a position.
-- Coordinators get no role (the Coordinator role is deprecated).
-- Accounts that already hold any role are left alone. Safe to re-run.
--
-- @DryRun = 1 shows what would be assigned and changes nothing.

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @DryRun BIT = 1;   -- 1 = preview only, 0 = apply

DECLARE @Manager    INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Manager'    AND IsActive = 1);
DECLARE @Supervisor INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Supervisor' AND IsActive = 1);

-- Position -> role. Uses dbo.ApprovalRoleTitle when it has the position, else the built-in names.
SELECT u.UserId, u.Name AS AccountName, e.Name AS EmployeeName, e.Position,
       CASE
           WHEN art.ApprovalRole IN ('Manager', 'Supervisor') THEN art.ApprovalRole
           WHEN art.ApprovalRole IS NULL AND UPPER(LTRIM(RTRIM(e.Position))) IN
                ('MANAGER', 'ASST. MANAGER', 'ASSISTANT MANAGER', 'JR. ASST. MANAGER',
                 'JUNIOR ASSISTANT MANAGER', 'ACTING JR. ASST. MANAGER') THEN 'Manager'
           WHEN art.ApprovalRole IS NULL AND UPPER(LTRIM(RTRIM(e.Position))) = 'SUPERVISOR' THEN 'Supervisor'
       END AS NewRole
INTO #Plan
FROM dbo.[User] u
INNER JOIN dbo.Employee e ON e.EmpId = u.EmpId
LEFT JOIN dbo.ApprovalRoleTitle art
       ON UPPER(LTRIM(RTRIM(art.PositionTitle))) = UPPER(LTRIM(RTRIM(e.Position))) AND art.IsActive = 1
WHERE NOT EXISTS (SELECT 1 FROM dbo.UserRole ur WHERE ur.UserId = u.UserId);

SELECT UserId, AccountName, EmployeeName, Position, ISNULL(NewRole, '(none)') AS NewRole
FROM #Plan
ORDER BY CASE WHEN NewRole IS NULL THEN 1 ELSE 0 END, AccountName;

IF @DryRun = 1
    PRINT 'DRY RUN: nothing was changed. Set @DryRun = 0 to apply.';
ELSE
BEGIN
    IF @Manager IS NULL OR @Supervisor IS NULL
        THROW 50010, 'The Manager and Supervisor roles must exist and be active. Nothing was changed.', 1;

    INSERT INTO dbo.UserRole (UserId, RoleId, DateAssigned)
    SELECT UserId, CASE NewRole WHEN 'Manager' THEN @Manager ELSE @Supervisor END, GETDATE()
    FROM #Plan
    WHERE NewRole IS NOT NULL;

    PRINT CONCAT(@@ROWCOUNT, ' role assignment(s) added.');
END

DROP TABLE #Plan;
