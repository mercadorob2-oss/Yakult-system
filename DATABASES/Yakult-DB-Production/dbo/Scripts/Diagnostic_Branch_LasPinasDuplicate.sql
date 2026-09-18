-- ============================================================
-- Diagnostic: Branch — Las Pinas / LAS PIÑAS CENTER duplicate check
-- ============================================================
-- PURPOSE
--   The ñ-merge migration handled Dasmarinas, Binan, and Paranaque
--   but did NOT include Las Pinas / LAS PIÑAS CENTER.
--   This script shows the current duplicate state so you can
--   verify the problem before running the merge migration.
--
-- HOW TO RUN
--   Run each block individually (highlight + execute).
-- ============================================================

-- ── D1: Show both Las Pinas branch records ─────────────────────────
-- Expected: 2 rows — one without ñ and one with ñ
PRINT '--- D1: Las Pinas branch records ---';
SELECT
    BranchId,
    Name,
    BranchType,
    Acronym,
    Active
FROM dbo.Branch
WHERE Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
ORDER BY BranchId;
GO

-- ── D2: BDC (department links) per Las Pinas branch ────────────────
-- Expected: duplicated department rows across the two branches
PRINT '--- D2: BDC department links for both Las Pinas branches ---';
SELECT
    b.BranchId,
    b.Name          AS BranchName,
    d.DeptId,
    d.Name          AS DepartmentName,
    d.Active        AS DeptActive,
    c.Name          AS CompanyName
FROM       dbo.Branch                  b
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.BranchID     = b.BranchId
INNER JOIN dbo.Department              d   ON d.DeptId         = bdc.DepartmentID
INNER JOIN dbo.Company                 c   ON c.ComId          = bdc.CompanyID
WHERE b.Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
ORDER BY d.Name, b.BranchId;
GO

-- ── D3: Employees assigned to Las Pinas branches ───────────────────
PRINT '--- D3: Employees on Las Pinas branches ---';
SELECT
    e.EmpId,
    e.Name       AS EmployeeName,
    e.Position,
    e.BranchId,
    b.Name       AS BranchName,
    d.Name       AS DepartmentName
FROM       dbo.Employee   e
INNER JOIN dbo.Branch     b ON b.BranchId = e.BranchId
LEFT  JOIN dbo.Department d ON d.DeptId   = e.DeptId
WHERE b.Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
ORDER BY b.BranchId, e.Name;
GO

-- ── D4: Active departments visible to BOTH branches (the duplicate effect)
-- These are department names that appear in BDC for BOTH Las Pinas branch IDs,
-- causing them to show up twice in department dropdowns.
PRINT '--- D4: Duplicate department names across both Las Pinas branches ---';
SELECT
    d.Name          AS DepartmentName,
    COUNT(DISTINCT bdc.BranchID) AS BranchCount,
    STRING_AGG(CAST(bdc.BranchID AS NVARCHAR(10)), ', ') AS BranchIds
FROM       dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Department              d ON d.DeptId = bdc.DepartmentID
INNER JOIN dbo.Branch                  b ON b.BranchId = bdc.BranchID
WHERE b.Name LIKE N'%Las Pi%as%' COLLATE Latin1_General_CI_AI
  AND d.Active = 1
GROUP BY d.Name
HAVING COUNT(DISTINCT bdc.BranchID) > 1
ORDER BY d.Name;
GO
