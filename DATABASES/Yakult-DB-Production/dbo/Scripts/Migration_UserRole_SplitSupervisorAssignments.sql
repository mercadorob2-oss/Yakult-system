-- Decide who holds IT Supervisor and who holds the new Supervisor role.
--
--   Keep IT Supervisor : the names listed under "EDIT HERE" below
--   Everyone else who currently holds the (renamed) IT Supervisor role -> Supervisor
--
-- Prerequisite: Migration_Role_SplitSupervisor.sql has been run.
--
-- Works on any database (production, dev, dummy):
--   * Names are matched against the account name OR the linked employee name. First and last
--     name only need to appear, in any order and with any middle initial, so "JOSEPH B. JASMIN"
--     and "JASMIN, JOSEPH" both match "JOSEPH" + "JASMIN".
--   * A name with no account is reported and skipped, it does not stop the script
--     (set @StrictNames = 1 to make a missing name stop it instead).
--   * If NONE of the names match, the script stops rather than moving every supervisor to the
--     plain Supervisor role (set @AllowNoMatches = 1 to allow that on purpose).
--   * @DryRun = 1 shows what would change and changes nothing.
--   * Re-running is safe: once the Supervisor role has members it does nothing, unless
--     @Reapply = 1.

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ==================== EDIT HERE ====================
DECLARE @DryRun         BIT = 0;   -- 1 = preview only, change nothing
DECLARE @StrictNames    BIT = 0;   -- 1 = stop if any name below has no account
DECLARE @AllowNoMatches BIT = 0;   -- 1 = allow the case where no name matches at all
DECLARE @Reapply        BIT = 0;   -- 1 = run again even if Supervisor already has members

CREATE TABLE #Keep (
    KeepId     INT IDENTITY(1, 1) PRIMARY KEY,
    FirstName  VARCHAR(50) NOT NULL,
    LastName   VARCHAR(50) NOT NULL,
    UserId     INT NULL,
    MatchCount INT NOT NULL DEFAULT (0)
);
INSERT INTO #Keep (FirstName, LastName) VALUES
    ('JOSEPH',  'JASMIN'),
    ('CAROLYN', 'BANZON'),
    ('FROILAN', 'ARTATES'),
    ('RACHEL',  'POTOT');
-- ================== END EDIT HERE ==================

DECLARE @ItSup INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'IT Supervisor');
DECLARE @Sup   INT = (SELECT RoleId FROM dbo.Role WHERE RoleName = 'Supervisor');

IF @ItSup IS NULL OR @Sup IS NULL
    THROW 50001, 'IT Supervisor and Supervisor roles must exist. Run Migration_Role_SplitSupervisor.sql first.', 1;

IF @Reapply = 0 AND EXISTS (SELECT 1 FROM dbo.UserRole WHERE RoleId = @Sup)
BEGIN
    PRINT 'Supervisor role already has members. Assignments were applied before, nothing changed. Set @Reapply = 1 to run again.';
    RETURN;
END

-- Find the account behind each name (prefer one that already holds IT Supervisor).
SELECT k.KeepId, u.UserId,
       CASE WHEN EXISTS (SELECT 1 FROM dbo.UserRole ur WHERE ur.UserId = u.UserId AND ur.RoleId = @ItSup)
            THEN 0 ELSE 1 END AS Pref
INTO #Matches
FROM #Keep k
INNER JOIN dbo.[User] u
    ON (u.Name LIKE '%' + k.FirstName + '%' AND u.Name LIKE '%' + k.LastName + '%')
    OR EXISTS (SELECT 1 FROM dbo.Employee e
               WHERE e.EmpId = u.EmpId
                 AND e.Name LIKE '%' + k.FirstName + '%' AND e.Name LIKE '%' + k.LastName + '%');

UPDATE k
SET k.MatchCount = (SELECT COUNT(*) FROM #Matches m WHERE m.KeepId = k.KeepId),
    k.UserId     = (SELECT TOP 1 m.UserId FROM #Matches m WHERE m.KeepId = k.KeepId ORDER BY m.Pref, m.UserId)
FROM #Keep k;

-- Report how each name resolved.
SELECT k.FirstName, k.LastName, k.MatchCount,
       CASE WHEN k.MatchCount = 0 THEN 'NO ACCOUNT FOUND - skipped'
            WHEN k.MatchCount > 1 THEN 'several matches - using the best one'
            ELSE 'ok' END AS Result,
       u.UserId, u.Name AS AccountName, e.Name AS EmployeeName
FROM #Keep k
LEFT JOIN dbo.[User] u ON u.UserId = k.UserId
LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
ORDER BY k.KeepId;

IF @StrictNames = 1 AND EXISTS (SELECT 1 FROM #Keep WHERE UserId IS NULL)
    THROW 50002, 'One or more names did not match an account (@StrictNames = 1). Nothing was changed.', 1;

IF @AllowNoMatches = 0 AND NOT EXISTS (SELECT 1 FROM #Keep WHERE UserId IS NOT NULL)
    THROW 50003, 'None of the names matched an account, so every supervisor would move to the plain Supervisor role. Check the names, or set @AllowNoMatches = 1. Nothing was changed.', 1;

IF EXISTS (SELECT 1 FROM #Keep WHERE UserId IS NULL)
    PRINT 'Warning: some names had no account and were skipped, see the result table above.';

IF @DryRun = 1
BEGIN
    PRINT 'DRY RUN: nothing was changed. Rows below are what WOULD move to Supervisor.';
    SELECT u.UserId, u.Name AS AccountName, e.Name AS EmployeeName, e.Position, 'would move to Supervisor' AS Action
    FROM dbo.UserRole ur
    INNER JOIN dbo.[User] u ON u.UserId = ur.UserId
    LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
    WHERE ur.RoleId = @ItSup
      AND ur.UserId NOT IN (SELECT UserId FROM #Keep WHERE UserId IS NOT NULL)
    ORDER BY u.Name;

    DROP TABLE #Matches;
    DROP TABLE #Keep;
    RETURN;
END

BEGIN TRANSACTION;

-- The matched accounts hold IT Supervisor.
INSERT INTO dbo.UserRole (UserId, RoleId)
SELECT k.UserId, @ItSup
FROM #Keep k
WHERE k.UserId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole ur WHERE ur.UserId = k.UserId AND ur.RoleId = @ItSup);

-- Every other current IT Supervisor holder becomes a plain Supervisor.
INSERT INTO dbo.UserRole (UserId, RoleId)
SELECT ur.UserId, @Sup
FROM dbo.UserRole ur
WHERE ur.RoleId = @ItSup
  AND ur.UserId NOT IN (SELECT UserId FROM #Keep WHERE UserId IS NOT NULL)
  AND NOT EXISTS (SELECT 1 FROM dbo.UserRole x WHERE x.UserId = ur.UserId AND x.RoleId = @Sup);

DELETE FROM dbo.UserRole
WHERE RoleId = @ItSup
  AND UserId NOT IN (SELECT UserId FROM #Keep WHERE UserId IS NOT NULL);

COMMIT TRANSACTION;

-- Result check: who holds each role now.
SELECT r.RoleName, u.UserId, u.Name AS AccountName, e.Name AS EmployeeName, e.Position
FROM dbo.UserRole ur
INNER JOIN dbo.Role r ON r.RoleId = ur.RoleId
INNER JOIN dbo.[User] u ON u.UserId = ur.UserId
LEFT JOIN dbo.Employee e ON e.EmpId = u.EmpId
WHERE r.RoleName IN ('IT Supervisor', 'Supervisor')
ORDER BY r.RoleName, u.Name;

DROP TABLE #Matches;
DROP TABLE #Keep;
