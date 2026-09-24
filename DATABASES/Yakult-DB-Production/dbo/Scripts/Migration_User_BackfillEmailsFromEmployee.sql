-- Replace placeholder account emails (userNNN@yakult.local) with the employee's real email.
--
--   1. Personal Email  : the employee's primary email (Employee Management "Personal Email")
--   2. Branch Email    : only for SUPERVISOR level, and only when there is no usable personal email
--   Anything else stays as it is.
--
-- Why this is needed: accounts made by Bulk Create Account before the email lookup was added
-- got the placeholder address. The lookup only applies to accounts created after it.
--
-- Safe to re-run:
--   * only touches accounts whose email is still a placeholder (user<digits>@yakult.local),
--     so a real email, or one someone typed by hand, is never overwritten
--   * dbo.User.EmailAddress is unique, so an address already used by another account is skipped,
--     and when several accounts want the same address (branch emails are shared) only one gets it
--   * @DryRun = 1 shows what would change and changes nothing

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ==================== EDIT HERE ====================
DECLARE @DryRun          BIT = 1;   -- 1 = preview only, 0 = apply
DECLARE @BranchForAll    BIT = 0;   -- 1 = use the branch email for every level, not just Supervisor
-- ================== END EDIT HERE ==================

-- Accounts still on a placeholder email, with the employee they belong to.
SELECT u.UserId, u.Name AS AccountName, u.EmailAddress AS OldEmail, u.EmpId,
       e.Name AS EmployeeName, e.Position,
       CASE WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) = 'SUPERVISOR' THEN 1 ELSE 0 END AS IsSupervisor,
       e.ComId, e.BranchId, e.DeptId
INTO #Targets
FROM dbo.[User] u
INNER JOIN dbo.Employee e ON e.EmpId = u.EmpId
WHERE u.EmailAddress LIKE 'user%@yakult.local'
  AND REPLACE(REPLACE(u.EmailAddress, 'user', ''), '@yakult.local', '') <> ''
  AND REPLACE(REPLACE(u.EmailAddress, 'user', ''), '@yakult.local', '') NOT LIKE '%[^0-9]%';

-- Personal email (first primary, active one per employee).
SELECT ee.EmpId, ea.EmailAddress,
       ROW_NUMBER() OVER (PARTITION BY ee.EmpId ORDER BY ee.EmployeeEmailId) AS rn
INTO #Personal
FROM dbo.EmployeeEmail ee
INNER JOIN dbo.EmailAddress ea ON ea.EmailId = ee.EmailId
WHERE ee.IsPrimary = 1 AND ee.IsActive = 1 AND ea.IsActive = 1;

-- Branch email (company + department + branch account email).
SELECT t.EmpId, ba.EmailAddress,
       ROW_NUMBER() OVER (PARTITION BY t.EmpId ORDER BY da.Id) AS rn
INTO #Branch
FROM #Targets t
INNER JOIN dbo.Company    c  ON c.ComId    = t.ComId
INNER JOIN dbo.Branch     b  ON b.BranchId = t.BranchId
INNER JOIN dbo.Department d  ON d.DeptId   = t.DeptId
INNER JOIN dbo.DepartmentAccount da
        ON da.CompanyName = c.Name AND da.DepartmentName = d.Name AND da.BranchName = b.Name
INNER JOIN dbo.EmailAddress ba ON ba.EmailId = da.EmailAddressId;

CREATE TABLE #Plan (
    UserId   INT           NOT NULL PRIMARY KEY,
    NewEmail NVARCHAR(255) NOT NULL,
    Source   VARCHAR(10)   NOT NULL
);

-- Pass 1: personal emails. One account per address, and never one held by a different account.
INSERT INTO #Plan (UserId, NewEmail, Source)
SELECT x.UserId, x.EmailAddress, 'Personal'
FROM (
    SELECT t.UserId, p.EmailAddress,
           ROW_NUMBER() OVER (PARTITION BY LOWER(p.EmailAddress) ORDER BY t.UserId) AS pick
    FROM #Targets t
    INNER JOIN #Personal p ON p.EmpId = t.EmpId AND p.rn = 1
    WHERE LEN(p.EmailAddress) <= 255
      AND NOT EXISTS (SELECT 1 FROM dbo.[User] o WHERE o.EmailAddress = p.EmailAddress AND o.UserId <> t.UserId)
) x
WHERE x.pick = 1;

-- Pass 2: branch email for supervisors (or everyone if @BranchForAll = 1) still without one.
INSERT INTO #Plan (UserId, NewEmail, Source)
SELECT x.UserId, x.EmailAddress, 'Branch'
FROM (
    SELECT t.UserId, br.EmailAddress,
           ROW_NUMBER() OVER (PARTITION BY LOWER(br.EmailAddress) ORDER BY t.UserId) AS pick
    FROM #Targets t
    INNER JOIN #Branch br ON br.EmpId = t.EmpId AND br.rn = 1
    WHERE (t.IsSupervisor = 1 OR @BranchForAll = 1)
      AND NOT EXISTS (SELECT 1 FROM #Plan p WHERE p.UserId = t.UserId)
      AND NOT EXISTS (SELECT 1 FROM #Plan p WHERE p.NewEmail = br.EmailAddress)
      AND LEN(br.EmailAddress) <= 255
      AND NOT EXISTS (SELECT 1 FROM dbo.[User] o WHERE o.EmailAddress = br.EmailAddress AND o.UserId <> t.UserId)
) x
WHERE x.pick = 1;

-- What will happen, per account.
SELECT t.UserId, t.AccountName, t.EmployeeName, t.Position,
       t.OldEmail, p.NewEmail, ISNULL(p.Source, 'unchanged (no usable email)') AS Result
FROM #Targets t
LEFT JOIN #Plan p ON p.UserId = t.UserId
ORDER BY CASE WHEN p.UserId IS NULL THEN 1 ELSE 0 END, t.AccountName;

SELECT ISNULL(p.Source, 'unchanged') AS Result, COUNT(*) AS Accounts
FROM #Targets t
LEFT JOIN #Plan p ON p.UserId = t.UserId
GROUP BY ISNULL(p.Source, 'unchanged');

IF @DryRun = 1
BEGIN
    PRINT 'DRY RUN: nothing was changed. Set @DryRun = 0 to apply.';
END
ELSE
BEGIN
    BEGIN TRANSACTION;

    UPDATE u
    SET u.EmailAddress = p.NewEmail
    FROM dbo.[User] u
    INNER JOIN #Plan p ON p.UserId = u.UserId
    WHERE u.EmailAddress LIKE 'user%@yakult.local';   -- still a placeholder

    PRINT CONCAT(@@ROWCOUNT, ' account email(s) updated.');
    COMMIT TRANSACTION;
END

DROP TABLE #Plan;
DROP TABLE #Branch;
DROP TABLE #Personal;
DROP TABLE #Targets;
