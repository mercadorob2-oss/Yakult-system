-- ============================================================================
-- Data fix: a Department was renamed, so name-keyed rows no longer match it.
--
-- dbo.DepartmentAccount and dbo.DepartmentEmail store the department by NAME.
-- When 'Personnel Management Department' was renamed to 'Human Resources
-- Department', their rows kept the old name, so:
--   - Migration_DepartmentAccount_AddScopeIds.sql could not fill DeptId for them;
--   - the Department Accounts page shows the HR combinations with no email/account.
--
-- This script moves those rows to the new name and fills DeptId. If a row for the
-- new name already exists (UQ on the name combination), the old row's email is
-- copied onto it where it has none, and the old row is removed, unless the old row
-- holds login credentials, which is reported instead so nothing is lost.
--
-- Reusable: change @OldName / @NewName for any future rename. Idempotent.
-- Run AFTER Migration_DepartmentAccount_AddScopeIds.sql.
-- ============================================================================

SET XACT_ABORT ON;

DECLARE @OldName NVARCHAR(200) = N'Personnel Management Department';
DECLARE @NewName NVARCHAR(200) = N'Human Resources Department';
DECLARE @NewDeptId INT;

IF (SELECT COUNT(*) FROM dbo.Department WHERE Name = @NewName) <> 1
BEGIN
    RAISERROR('Expected exactly one dbo.Department named ''%s''. Nothing changed.', 16, 1, @NewName);
    RETURN;
END

SELECT @NewDeptId = DeptId FROM dbo.Department WHERE Name = @NewName;

BEGIN TRANSACTION;

-- ── DepartmentAccount ───────────────────────────────────────────────────────

-- Rows whose new-name combination already exists: report the ones holding credentials.
SELECT 'CONFLICT: old row has login credentials and a row for the new name already exists; resolve manually' AS Issue,
       o.Id AS OldRowId, o.Username AS OldUsername, n.Id AS NewRowId, n.Username AS NewUsername,
       o.CompanyName, o.BranchName
FROM   dbo.DepartmentAccount o
JOIN   dbo.DepartmentAccount n
       ON n.CompanyName = o.CompanyName AND n.BranchName = o.BranchName AND n.DepartmentName = @NewName
WHERE  o.DepartmentName = @OldName
  AND  o.Username IS NOT NULL;

-- Merge credential-less old rows into the existing new-name row (keep its email, else take the old one).
UPDATE n
SET    n.EmailAddressId = ISNULL(n.EmailAddressId, o.EmailAddressId),
       n.DeptId         = @NewDeptId
FROM   dbo.DepartmentAccount o
JOIN   dbo.DepartmentAccount n
       ON n.CompanyName = o.CompanyName AND n.BranchName = o.BranchName AND n.DepartmentName = @NewName
WHERE  o.DepartmentName = @OldName
  AND  o.Username IS NULL;

DELETE o
FROM   dbo.DepartmentAccount o
WHERE  o.DepartmentName = @OldName
  AND  o.Username IS NULL
  AND  EXISTS (SELECT 1 FROM dbo.DepartmentAccount n
               WHERE n.CompanyName = o.CompanyName AND n.BranchName = o.BranchName
                 AND n.DepartmentName = @NewName);

-- No new-name row yet: rename in place.
UPDATE o
SET    o.DepartmentName = @NewName,
       o.DeptId         = @NewDeptId
FROM   dbo.DepartmentAccount o
WHERE  o.DepartmentName = @OldName
  AND  NOT EXISTS (SELECT 1 FROM dbo.DepartmentAccount n
                   WHERE n.CompanyName = o.CompanyName AND n.BranchName = o.BranchName
                     AND n.DepartmentName = @NewName);

-- Any new-name rows still missing the ID.
UPDATE dbo.DepartmentAccount
SET    DeptId = @NewDeptId
WHERE  DepartmentName = @NewName AND DeptId IS NULL;

-- ── DepartmentEmail (keyed by Company + Department name) ────────────────────

UPDATE n
SET    n.EmailAddressId = ISNULL(n.EmailAddressId, o.EmailAddressId)
FROM   dbo.DepartmentEmail o
JOIN   dbo.DepartmentEmail n ON n.CompanyName = o.CompanyName AND n.DepartmentName = @NewName
WHERE  o.DepartmentName = @OldName;

DELETE o
FROM   dbo.DepartmentEmail o
WHERE  o.DepartmentName = @OldName
  AND  EXISTS (SELECT 1 FROM dbo.DepartmentEmail n
               WHERE n.CompanyName = o.CompanyName AND n.DepartmentName = @NewName);

UPDATE dbo.DepartmentEmail
SET    DepartmentName = @NewName
WHERE  DepartmentName = @OldName;

COMMIT TRANSACTION;
GO

-- ── Human Resources Department email ────────────────────────────────────────
-- The department's email is now hrd@YAKULT.com.ph (was pmd@YAKULT.com.ph from the
-- Personnel Management days). It is the DEPT email (dbo.DepartmentEmail, one per
-- Company + Department, shared by all branches), for both YPI and YMC. No branch
-- email override: clear the one an earlier version of this script put on
-- YPI / HR / MANILA LIAISON OFFICE. Id 353 (YPI / blank department / MANILA LIAISON
-- OFFICE) was a stray row with only a dummy email and is removed. Idempotent.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

DECLARE @HrEmail NVARCHAR(255) = N'hrd@YAKULT.com.ph';
DECLARE @HrDept  NVARCHAR(200) = N'Human Resources Department';
DECLARE @HrEmailId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.EmailAddress WHERE EmailAddress = @HrEmail)
    INSERT INTO dbo.EmailAddress (EmailAddress, DisplayName, IsActive) VALUES (@HrEmail, @HrEmail, 1);
SELECT @HrEmailId = EmailId FROM dbo.EmailAddress WHERE EmailAddress = @HrEmail;

-- Dept email for YPI and YMC (update the existing row, or add one).
UPDATE dbo.DepartmentEmail
SET    EmailAddressId = @HrEmailId
WHERE  DepartmentName = @HrDept AND CompanyName IN (N'YPI', N'YMC');

INSERT INTO dbo.DepartmentEmail (CompanyName, DepartmentName, EmailAddressId)
SELECT co.CompanyName, @HrDept, @HrEmailId
FROM   (VALUES (N'YPI'), (N'YMC')) co(CompanyName)
WHERE  NOT EXISTS (SELECT 1 FROM dbo.DepartmentEmail de
                   WHERE de.CompanyName = co.CompanyName AND de.DepartmentName = @HrDept);

-- Undo the branch-email binding, so the branch uses the dept email.
UPDATE dbo.DepartmentAccount
SET    EmailAddressId = NULL
WHERE  CompanyName = N'YPI' AND DepartmentName = @HrDept
  AND  BranchName = N'MANILA LIAISON OFFICE' AND EmailAddressId = @HrEmailId;

DELETE FROM dbo.DepartmentAccount
WHERE  Id = 353 AND Username IS NULL AND DepartmentName = N'';

COMMIT TRANSACTION;
GO

-- The old address is left in dbo.EmailAddress; remove it from Email Configuration
-- if nothing else uses it:
SELECT 'Still bound to pmd@' AS Note, 'DepartmentEmail' AS [Table], de.Id, de.CompanyName, de.DepartmentName, NULL AS BranchName
FROM   dbo.DepartmentEmail de JOIN dbo.EmailAddress ea ON ea.EmailId = de.EmailAddressId
WHERE  ea.EmailAddress = N'pmd@YAKULT.com.ph'
UNION ALL
SELECT 'Still bound to pmd@', 'DepartmentAccount', da.Id, da.CompanyName, da.DepartmentName, da.BranchName
FROM   dbo.DepartmentAccount da JOIN dbo.EmailAddress ea ON ea.EmailId = da.EmailAddressId
WHERE  ea.EmailAddress = N'pmd@YAKULT.com.ph';
GO

-- ── Review ──────────────────────────────────────────────────────────────────
-- Should return no rows.
SELECT Id, CompanyName, DepartmentName, BranchName, Username, EmailAddressId, ComId, DeptId, BranchId
FROM   dbo.DepartmentAccount
WHERE  ComId IS NULL OR DeptId IS NULL OR BranchId IS NULL;
GO
