-- ============================================================
-- Seed: DepartmentAccount — one lookup row per unique
--       Company / Department / Branch combination.
--
-- Safe to re-run: skips combinations that already exist.
--
-- Account columns (Username, PasswordHash, PasswordSalt,
-- IsActive, DateCreated, UserId, EmailAddressId) are left
-- NULL — use the admin page to create accounts per row.
-- ============================================================

INSERT INTO dbo.DepartmentAccount (CompanyName, DepartmentName, BranchName)
SELECT DISTINCT
    c.Name AS CompanyName,
    d.Name AS DepartmentName,
    b.Name AS BranchName
FROM dbo.Employee e
INNER JOIN dbo.Company    c ON e.ComId    = c.ComId
INNER JOIN dbo.Department d ON e.DeptId   = d.DeptId
INNER JOIN dbo.Branch     b ON e.BranchId = b.BranchId
WHERE e.Active = 1
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.DepartmentAccount da
      WHERE da.CompanyName    = c.Name
        AND da.DepartmentName = d.Name
        AND da.BranchName     = b.Name
  )
ORDER BY c.Name, d.Name, b.Name;
GO

-- Preview all rows after seed
SELECT
    CompanyName,
    DepartmentName,
    BranchName,
    Username,
    CASE
        WHEN Username IS NULL     THEN '—'
        WHEN PasswordHash IS NULL THEN 'Needs Password'
        WHEN IsActive = 1         THEN 'Active'
        ELSE                           'Inactive'
    END AS AccountStatus
FROM dbo.DepartmentAccount
ORDER BY CompanyName, DepartmentName, BranchName;
GO
