-- ============================================================
-- Migration: Department — Hard-delete deactivated duplicates
--            and combined branch-department rows
-- ============================================================
-- BACKGROUND
--   Migration_Department_DeduplicateHOAndFactory.sql deactivated
--   the old HO-era duplicate departments and remapped all FK
--   references to the canonical factory-seeded rows.
--   Those deactivated rows can now be permanently removed.a
--
--   DeptIds 60-64 are "combined" rows that embed a branch name
--   inside the department name (e.g. "DS - Marilao Depot").
--   These were created before BDC existed to distinguish the same
--   department concept across different branches.
--   Since BDC now owns the branch-department relationship, these
--   combined rows are replaced by:
--     • One canonical "Direct Sales" department row
--     • BDC entries linking it to each branch
--     • Employees remapped to the canonical dept + correct branch
--
-- WHAT THIS SCRIPT DOES
--   Phase 1 — Hard-delete rows marked [DEACTIVATED] by the
--             previous deduplication migration.
--   Phase 2 — Find or create a canonical "Direct Sales" dept.
--   Phase 3 — Resolve each combined dept to its real branch
--             (by name pattern from dbo.Branch).
--   Phase 4 — Remap employees: DeptId → canonical, BranchId →
--             resolved branch.
--   Phase 5 — Create BDC entries (canonical dept → each branch).
--   Phase 6 — Delete combined branch-dept rows (60-64).
--
-- SAFE TO RE-RUN   — all steps are idempotent
-- ROLLBACK         — single transaction; auto-rolls back on error
-- PREREQUISITES    — Migration_Department_DeduplicateHOAndFactory.sql
-- ============================================================

-- ============================================================
-- HOW TO RUN THIS SCRIPT
--   BATCH 1 (Migration)   — highlight from SET NOCOUNT ON down
--                           to the GO after END CATCH, then execute.
--   BATCH 2 (Validation)  — run each validation block individually
--                           after the migration completes.
-- ============================================================

-- ============================================================
-- BATCH 1 OF 2 — Migration
-- Highlight from here to the GO after END CATCH, then execute.
-- ============================================================
SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

BEGIN TRY

-- Pre-flight: ensure the deduplication migration was already run
IF EXISTS (
    SELECT 1 FROM dbo.Department
    WHERE  Active = 1
      AND  Description IS NULL
)
    RAISERROR('PREREQUISITE: Active departments with NULL description still exist. Run Migration_Department_DeduplicateHOAndFactory.sql first.', 16, 1);

PRINT '=== Pre-flight passed. Starting cleanup migration. ===';
PRINT '';

DECLARE @SysUserId INT = 8;

-- ============================================================
-- PHASE 1 — Hard-delete deactivated duplicate departments
-- ============================================================
PRINT 'PHASE 1: Deleting deactivated duplicate departments...';

-- Safety: abort if any active FK still points at deactivated rows
IF EXISTS (
    SELECT 1 FROM dbo.BranchDepartmentCompany bdc
    INNER JOIN dbo.Department d ON d.DeptId = bdc.DepartmentID
    WHERE d.Active = 0
)
    RAISERROR('SAFETY: BranchDepartmentCompany still points at deactivated departments. Investigate before proceeding.', 16, 1);

IF EXISTS (
    SELECT 1 FROM dbo.Employee e
    INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
    WHERE d.Active = 0
)
    RAISERROR('SAFETY: Employee still points at deactivated departments. Investigate before proceeding.', 16, 1);

DELETE d
FROM   dbo.Department d
WHERE  d.Active = 0
  AND  d.Description LIKE N'[[]DEACTIVATED%';

PRINT CONCAT('  Deleted ', @@ROWCOUNT, ' deactivated duplicate department(s).');
PRINT '';

-- ============================================================
-- PHASE 2 — Find or create canonical "Direct Sales" department
-- ============================================================
PRINT 'PHASE 2: Resolving canonical Direct Sales department...';

DECLARE @CanonicalDeptId INT;

SELECT TOP 1 @CanonicalDeptId = DeptId
FROM   dbo.Department
WHERE  UPPER(LTRIM(RTRIM(Name))) LIKE N'%DIRECT SALES%'
  AND  Active = 1
  AND  DeptId NOT IN (60, 61, 62, 63, 64)
ORDER BY DeptId;

IF @CanonicalDeptId IS NULL
BEGIN
    INSERT INTO dbo.Department (Name, Description, CreatedBy, Active)
    VALUES (N'Direct Sales', N'Head Office / MANILA LIAISON Office', @SysUserId, 1);

    SET @CanonicalDeptId = SCOPE_IDENTITY();
    PRINT CONCAT('  Created canonical "Direct Sales" department (DeptId = ', @CanonicalDeptId, ').');
END
ELSE
    PRINT CONCAT('  Found existing canonical "Direct Sales" department (DeptId = ', @CanonicalDeptId, ').');

PRINT '';

-- ============================================================
-- PHASE 3 — Resolve each combined dept to its real branch
--   Each combined dept name encodes a branch:
--     60  Direct Sales - HO       → Head Office branch
--     61  DS - Marilao Depot      → Marilao Depot branch
--     62  DS - Victoria Depot     → Victoria Depot branch
--     63  DS - Cavite Depot       → Cavite Depot branch
--     64  DS - Antipolo Depot     → Antipolo Depot branch
-- ============================================================
PRINT 'PHASE 3: Resolving branch IDs for combined departments...';

DECLARE @Mappings TABLE (
    CombinedDeptId INT          NOT NULL,
    Label          NVARCHAR(50) NOT NULL,
    BranchPattern  NVARCHAR(100) NOT NULL,
    BranchId       INT          NULL,
    CompanyId      INT          NULL
);

INSERT INTO @Mappings (CombinedDeptId, Label, BranchPattern) VALUES
    (60, N'Direct Sales - HO',    N'%MANILA LIAISON%'),
    (61, N'DS - Marilao Depot',   N'%MARILAO%'),
    (62, N'DS - Victoria Depot',  N'%VICTORIA%'),
    (63, N'DS - Cavite Depot',    N'%CAVITE%'),
    (64, N'DS - Antipolo Depot',  N'%ANTIPOLO%');

-- Resolve BranchId from Branch, then CompanyId from an existing BDC entry
-- for that branch (Branch no longer carries ComId directly).
UPDATE m
SET    m.BranchId  = b.BranchId,
       m.CompanyId = c.CompanyID
FROM   @Mappings m
CROSS APPLY (
    SELECT TOP 1 BranchId
    FROM   dbo.Branch
    WHERE  UPPER(LTRIM(RTRIM(Name))) LIKE UPPER(m.BranchPattern)
      AND  Active = 1
    ORDER BY BranchId
) b
OUTER APPLY (
    SELECT TOP 1 CompanyID
    FROM   dbo.BranchDepartmentCompany
    WHERE  BranchID = b.BranchId
    ORDER BY BranchDeptCompanyID
) c;

-- Print resolved mappings for review
SELECT
    CombinedDeptId,
    Label,
    BranchPattern,
    BranchId,
    CompanyId,
    CASE WHEN BranchId IS NULL THEN 'WARNING: no branch match found' ELSE 'OK' END AS Status
FROM @Mappings
ORDER BY CombinedDeptId;

IF EXISTS (SELECT 1 FROM @Mappings WHERE BranchId IS NULL)
BEGIN
    PRINT '  WARNING: One or more combined depts could not be matched to a branch.';
    PRINT '  Those employees will have DeptId updated but BranchId left unchanged.';
END

PRINT '';

-- ============================================================
-- PHASE 4 — Remap employees from combined depts
--   DeptId  → canonical Direct Sales dept
--   BranchId → resolved branch (only when a match was found)
-- ============================================================
PRINT 'PHASE 4: Remapping employees...';

-- Update employees where a branch was resolved
UPDATE e
SET    e.DeptId       = @CanonicalDeptId,
       e.BranchId     = m.BranchId,
       e.DateModified = SYSUTCDATETIME(),
       e.ModifiedBy   = @SysUserId
FROM   dbo.Employee e
INNER JOIN @Mappings m ON m.CombinedDeptId = e.DeptId
WHERE  m.BranchId IS NOT NULL;

PRINT CONCAT('  Employees remapped (dept + branch): ', @@ROWCOUNT);

-- Update employees where no branch was resolved — dept only
UPDATE e
SET    e.DeptId       = @CanonicalDeptId,
       e.DateModified = SYSUTCDATETIME(),
       e.ModifiedBy   = @SysUserId
FROM   dbo.Employee e
INNER JOIN @Mappings m ON m.CombinedDeptId = e.DeptId
WHERE  m.BranchId IS NULL;

PRINT CONCAT('  Employees remapped (dept only, branch unresolved): ', @@ROWCOUNT);
PRINT '';

-- ============================================================
-- PHASE 5 — Create BDC entries: canonical dept → each branch
-- ============================================================
PRINT 'PHASE 5: Creating BDC entries for canonical Direct Sales dept...';

INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID, CreatedDate)
SELECT
    m.BranchId,
    @CanonicalDeptId,
    m.CompanyId,
    SYSUTCDATETIME()
FROM @Mappings m
WHERE m.BranchId  IS NOT NULL
  AND m.CompanyId IS NOT NULL
  AND NOT EXISTS (
      SELECT 1
      FROM   dbo.BranchDepartmentCompany bdc
      WHERE  bdc.BranchID     = m.BranchId
        AND  bdc.DepartmentID = @CanonicalDeptId
        AND  bdc.CompanyID    = m.CompanyId
  );

PRINT CONCAT('  BDC entries created: ', @@ROWCOUNT);
PRINT '';

-- ============================================================
-- PHASE 6 — Delete BDC entries for combined depts, then delete
--            the combined department rows themselves
-- ============================================================
PRINT 'PHASE 6: Deleting combined branch-department rows...';

DELETE bdc
FROM   dbo.BranchDepartmentCompany bdc
WHERE  bdc.DepartmentID IN (60, 61, 62, 63, 64);

PRINT CONCAT('  BDC entries for combined depts removed: ', @@ROWCOUNT);

DELETE ds
FROM   dbo.DepartmentSupervisor ds
WHERE  ds.DeptId IN (60, 61, 62, 63, 64);

PRINT CONCAT('  DepartmentSupervisor rows removed: ', @@ROWCOUNT);

DELETE FROM dbo.Department
WHERE  DeptId IN (60, 61, 62, 63, 64);

PRINT CONCAT('  Combined department rows deleted: ', @@ROWCOUNT);
PRINT '';

COMMIT TRANSACTION;
PRINT '=== Cleanup migration completed successfully. ===';

END TRY
BEGIN CATCH

    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg      NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT            = ERROR_SEVERITY();
    DECLARE @ErrState    INT            = ERROR_STATE();
    DECLARE @ErrLine     INT            = ERROR_LINE();

    PRINT CONCAT('=== Migration FAILED at line ', @ErrLine, ': ', @ErrMsg, ' ===');
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);

END CATCH;
GO

-- ============================================================
-- BATCH 2 OF 2 — Validation Queries
-- Run each block below individually after the migration completes.
-- ============================================================

-- V1: No deactivated or combined rows remain
-- Highlight this block and execute.
PRINT '--- Validation V1: No leftover deactivated/combined rows ---';
SELECT DeptId, Name, Description, Active
FROM   dbo.Department
WHERE  Active = 0
    OR DeptId IN (60, 61, 62, 63, 64);
-- Expected: 0 rows
GO

-- V2: Canonical Direct Sales dept exists with BDC entries
-- Highlight this block and execute.
PRINT '--- Validation V2: Direct Sales BDC linkages ---';
SELECT
    d.DeptId,
    d.Name          AS Department,
    b.Name          AS Branch,
    c.Name          AS Company,
    bdc.CreatedDate
FROM       dbo.Department              d
INNER JOIN dbo.BranchDepartmentCompany bdc ON bdc.DepartmentID = d.DeptId
INNER JOIN dbo.Branch                  b   ON b.BranchId       = bdc.BranchID
INNER JOIN dbo.Company                 c   ON c.ComId          = bdc.CompanyID
WHERE      UPPER(d.Name) LIKE N'%DIRECT SALES%'
ORDER BY   b.Name;
-- Expected: rows for HO, Marilao, Victoria, Cavite, Antipolo
GO

-- V3: No BDC orphans pointing at deleted dept IDs
-- Highlight this block and execute.
PRINT '--- Validation V3: BDC orphan check ---';
SELECT bdc.BranchDeptCompanyID, bdc.DepartmentID
FROM   dbo.BranchDepartmentCompany bdc
WHERE  bdc.DepartmentID IS NOT NULL
  AND  NOT EXISTS (
      SELECT 1 FROM dbo.Department d WHERE d.DeptId = bdc.DepartmentID
  );
-- Expected: 0 rows
GO

-- V4: Employees now on canonical Direct Sales dept
-- Highlight this block and execute.
PRINT '--- Validation V4: Employees on Direct Sales dept ---';
SELECT
    e.EmpId,
    e.Name        AS EmployeeName,
    e.Position,
    d.Name        AS Department,
    b.Name        AS Branch
FROM       dbo.Employee   e
INNER JOIN dbo.Department d ON d.DeptId   = e.DeptId
LEFT  JOIN dbo.Branch     b ON b.BranchId = e.BranchId
WHERE      UPPER(d.Name) LIKE N'%DIRECT SALES%'
ORDER BY   b.Name, e.Name;
GO

-- V5: Final active department list
-- Highlight this block and execute.
PRINT '--- Validation V5: All active departments ---';
SELECT DeptId, Name, Description
FROM   dbo.Department
WHERE  Active = 1
ORDER BY Name;
GO
