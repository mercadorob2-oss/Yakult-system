-- ============================================================================
-- Data fix: create the 'Booking Section' department and move these employees
-- (matched by dbo.Employee.EmployeeNumber) into it.
--
-- Also registers the new department where the app looks for it:
--   - dbo.BranchDepartmentCompany: one row per (Company, Branch) of the moved
--     employees, so it shows in the Branch / Department pickers;
--   - dbo.DepartmentAccount: a credential-less placeholder row per (Company,
--     Branch), same as AddDepartmentDialog does, so it shows on the
--     Department Accounts page.
--
-- Idempotent: safe to re-run. Review the result sets before relying on it:
--   1. employee numbers not found (nothing is changed for them);
--   2. employee numbers matching more than one employee (all matches are moved;
--      check they are the intended people);
--   3. the employees after the change.
-- ============================================================================

SET XACT_ABORT ON;
SET NOCOUNT ON;

DECLARE @DeptName NVARCHAR(150) = N'Booking Section';
DECLARE @DeptId   INT;
DECLARE @ByUserId INT;

DECLARE @EmpNos TABLE (EmployeeNumber VARCHAR(20) PRIMARY KEY);
INSERT INTO @EmpNos (EmployeeNumber) VALUES
    ('173X'), ('1987'), ('1192'), ('1011'), ('1903'), ('1905'), ('1343'), ('5251'),
    ('9155'), ('9158'), ('9161'), ('1044'), ('1204'), ('5062'), ('5055'), ('5231'),
    ('5257'), ('5348'), ('5568'),
    ('1919'), ('1084'), ('1616'), ('5016'), ('5296'), ('5579'), ('5669'), ('5707'),
    ('5745'), ('5749'), ('5763'), ('5835'), ('5846'), ('5861'), ('5922'), ('5936'),
    ('5978'), ('9035'), ('9145'), ('9204');

-- dbo.Department.CreatedBy is NOT NULL: attribute the change to a developer account.
SELECT @ByUserId = MIN(UserId) FROM dbo.[User] WHERE IsDeveloper = 1;
IF @ByUserId IS NULL
BEGIN
    RAISERROR('No developer user found for CreatedBy. Nothing changed.', 16, 1);
    RETURN;
END

-- ── 1. Employee numbers not found ───────────────────────────────────────────
SELECT 'NOT FOUND' AS Issue, n.EmployeeNumber
FROM   @EmpNos n
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Employee e
                   WHERE LTRIM(RTRIM(e.EmployeeNumber)) = n.EmployeeNumber)
ORDER BY n.EmployeeNumber;

-- ── 2. Employee numbers matching more than one employee ─────────────────────
SELECT 'MULTIPLE MATCHES' AS Issue, e.EmployeeNumber, e.EmpId, e.Name, e.Active,
       c.Name AS Company, b.Name AS Branch
FROM   dbo.Employee e
JOIN   @EmpNos n ON n.EmployeeNumber = LTRIM(RTRIM(e.EmployeeNumber))
LEFT JOIN dbo.Company c ON c.ComId = e.ComId
LEFT JOIN dbo.Branch  b ON b.BranchId = e.BranchId
WHERE  n.EmployeeNumber IN (SELECT LTRIM(RTRIM(EmployeeNumber)) FROM dbo.Employee
                            GROUP BY LTRIM(RTRIM(EmployeeNumber)) HAVING COUNT(*) > 1)
ORDER BY e.EmployeeNumber, e.EmpId;

BEGIN TRANSACTION;

-- ── Department ──────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.Department WHERE Name = @DeptName)
    INSERT INTO dbo.Department (Name, Description, DateCreated, CreatedBy, Active)
    VALUES (@DeptName, NULL, SYSUTCDATETIME(), @ByUserId, 1);

IF (SELECT COUNT(*) FROM dbo.Department WHERE Name = @DeptName) <> 1
BEGIN
    ROLLBACK TRANSACTION;
    RAISERROR('Expected exactly one dbo.Department named ''%s''. Nothing changed.', 16, 1, @DeptName);
    RETURN;
END

SELECT @DeptId = DeptId FROM dbo.Department WHERE Name = @DeptName;

-- Reactivate it if it was deactivated earlier.
UPDATE dbo.Department SET Active = 1, ModifiedBy = @ByUserId
WHERE  DeptId = @DeptId AND Active = 0;

-- ── Employees ───────────────────────────────────────────────────────────────
UPDATE e
SET    e.DeptId       = @DeptId,
       e.ModifiedBy   = @ByUserId,
       e.DateModified = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time'
FROM   dbo.Employee e
JOIN   @EmpNos n ON n.EmployeeNumber = LTRIM(RTRIM(e.EmployeeNumber))
WHERE  ISNULL(e.DeptId, 0) <> @DeptId;

PRINT CONCAT('Employees moved to ', @DeptName, ': ', @@ROWCOUNT);

-- ── Branch / Department / Company links ─────────────────────────────────────
INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
SELECT DISTINCT e.BranchId, @DeptId, e.ComId
FROM   dbo.Employee e
JOIN   @EmpNos n ON n.EmployeeNumber = LTRIM(RTRIM(e.EmployeeNumber))
WHERE  NOT EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany bdc
                   WHERE bdc.BranchID = e.BranchId AND bdc.CompanyID = e.ComId
                     AND bdc.DepartmentID = @DeptId);

-- ── Department Accounts placeholders ────────────────────────────────────────
INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName, ComId, DeptId, BranchId)
SELECT DISTINCT c.Name, @DeptName, b.Name, c.ComId, @DeptId, b.BranchId
FROM   dbo.Employee e
JOIN   @EmpNos n ON n.EmployeeNumber = LTRIM(RTRIM(e.EmployeeNumber))
JOIN   dbo.Company c ON c.ComId = e.ComId
JOIN   dbo.Branch  b ON b.BranchId = e.BranchId
WHERE  NOT EXISTS (SELECT 1 FROM dbo.DepartmentAccount da
                   WHERE da.CompanyName = c.Name AND da.DepartmentName = @DeptName
                     AND da.BranchName = b.Name);

COMMIT TRANSACTION;
GO

-- ── 3. Review ───────────────────────────────────────────────────────────────
SELECT e.EmployeeNumber, e.Name, e.Position, c.Name AS Company, b.Name AS Branch,
       d.Name AS Department, e.Active
FROM   dbo.Employee e
JOIN   dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.Company c ON c.ComId = e.ComId
LEFT JOIN dbo.Branch  b ON b.BranchId = e.BranchId
WHERE  d.Name = N'Booking Section'
ORDER BY e.EmployeeNumber;
GO
