-- ============================================================
-- Migration: Department — Deduplicate HO-era vs Factory-seeded rows
-- ============================================================
-- BACKGROUND
--   Before BranchDepartmentCompany existed, each branch kept its
--   department link via dbo.Branch.DeptId.  This caused the same
--   section name (e.g. "SHIPPING SECTION") to appear as separate
--   Department rows for every branch that had one.
--
--   Migration_FactoryNormalization_CompanyBranchHierarchy then
--   seeded a fresh, properly-described set of factory sections for
--   CALAMBA PLANT under YPI, leaving the old HO-era rows as
--   unnamed duplicates (Description IS NULL).
--
--   Now that BranchDepartmentCompany handles branch → department
--   resolution, there is no need to keep a separate department row
--   per branch.  One canonical row (the factory-seeded one) can be
--   linked to multiple branches via BDC.
--
-- WHAT THIS SCRIPT DOES
--   1. Identifies duplicate pairs:
--        OLD  = same Name, Description IS NULL   (HO-era row)
--        NEW  = same Name, Description LIKE 'Factory section%'  (canonical row)
--   2. Re-maps FK references in:
--        dbo.BranchDepartmentCompany  (DepartmentID)
--        dbo.Employee                 (DeptId)
--        dbo.DepartmentSupervisor     (DeptId)
--   3. Deactivates the old duplicate rows  (Active = 0).
--   4. Describes the HO-only departments (not duplicated) as
--      "Head Office / MANILA LIAISON Office".
--
-- SAFE TO RE-RUN   — all steps are idempotent
-- ROLLBACK         — single transaction; auto-rolls back on error
-- PREREQUISITES    — Migration_FactoryNormalization_CompanyBranchHierarchy.sql
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

-- ── Pre-flight (inside TRY — RAISERROR here rolls back via CATCH) ──────────
IF NOT EXISTS (
    SELECT 1 FROM dbo.Department
    WHERE  Description LIKE N'Factory section%'
)
    RAISERROR('PREREQUISITE MISSING: No factory-seeded Department rows found. Run Migration_FactoryNormalization_CompanyBranchHierarchy.sql first.', 16, 1);

PRINT '=== Pre-flight passed. Starting deduplication migration. ===';
PRINT '';

DECLARE @SysUserId INT = 8;

-- ============================================================
-- STEP 1 — Build the duplicate-pairs map
--   old_id  = HO-era row  (Description IS NULL)
--   new_id  = factory-seeded canonical row (Description LIKE 'Factory section%')
-- ============================================================
DECLARE @DupePairs TABLE (
    OldDeptId INT NOT NULL,
    NewDeptId INT NOT NULL,
    DeptName  NVARCHAR(150) NOT NULL
);

INSERT INTO @DupePairs (OldDeptId, NewDeptId, DeptName)
SELECT
    old_d.DeptId    AS OldDeptId,
    new_d.DeptId    AS NewDeptId,
    old_d.Name      AS DeptName
FROM       dbo.Department old_d
INNER JOIN dbo.Department new_d
        ON UPPER(LTRIM(RTRIM(new_d.Name))) = UPPER(LTRIM(RTRIM(old_d.Name)))
       AND new_d.Description LIKE N'Factory section%'
       AND new_d.Active = 1
WHERE  old_d.Description IS NULL
  AND  old_d.Active = 1
  AND  old_d.DeptId <> new_d.DeptId;

DECLARE @PairCount INT = (SELECT COUNT(*) FROM @DupePairs);
PRINT CONCAT('Found ', @PairCount, ' duplicate department pair(s) to consolidate.');

IF @PairCount > 0
BEGIN
    -- Print what will be merged (visible in SSMS Messages tab)
    SELECT OldDeptId, NewDeptId, DeptName FROM @DupePairs ORDER BY DeptName;

    -- --------------------------------------------------------
    -- STEP 2A — Re-map BranchDepartmentCompany
    --   Case A: a BDC row for (BranchID, CompanyID, NewDeptId) already
    --           exists — the canonical row is already there, so just
    --           delete the old duplicate row.
    --   Case B: no BDC row exists for the new dept yet — update in place.
    -- --------------------------------------------------------
    DELETE bdc
    FROM   dbo.BranchDepartmentCompany bdc
    INNER JOIN @DupePairs p ON p.OldDeptId = bdc.DepartmentID
    WHERE  EXISTS (
        SELECT 1
        FROM   dbo.BranchDepartmentCompany existing
        WHERE  existing.BranchID     = bdc.BranchID
          AND  existing.CompanyID    = bdc.CompanyID
          AND  existing.DepartmentID = p.NewDeptId
    );

    PRINT CONCAT('  BDC rows deleted (canonical already present): ', @@ROWCOUNT);

    UPDATE bdc
    SET    bdc.DepartmentID = p.NewDeptId,
           bdc.UpdatedDate  = SYSUTCDATETIME()
    FROM   dbo.BranchDepartmentCompany bdc
    INNER JOIN @DupePairs p ON p.OldDeptId = bdc.DepartmentID;

    PRINT CONCAT('  BDC rows re-mapped (no conflict)            : ', @@ROWCOUNT);

    -- --------------------------------------------------------
    -- STEP 2B — Re-map Employee.DeptId
    -- --------------------------------------------------------
    UPDATE e
    SET    e.DeptId       = p.NewDeptId,
           e.DateModified = SYSUTCDATETIME(),
           e.ModifiedBy   = @SysUserId
    FROM   dbo.Employee e
    INNER JOIN @DupePairs p ON p.OldDeptId = e.DeptId;

    PRINT CONCAT('  Employee rows re-mapped    : ', @@ROWCOUNT);

    -- --------------------------------------------------------
    -- STEP 2C — Re-map DepartmentSupervisor.DeptId
    --   The table has a UQ on (DeptId, SupervisorUserId).
    --   Skip if the supervisor already exists for the new dept
    --   to avoid a duplicate-key error.
    -- --------------------------------------------------------
    UPDATE ds
    SET    ds.DeptId = p.NewDeptId
    FROM   dbo.DepartmentSupervisor ds
    INNER JOIN @DupePairs p ON p.OldDeptId = ds.DeptId
    WHERE  NOT EXISTS (
        SELECT 1
        FROM   dbo.DepartmentSupervisor existing
        WHERE  existing.DeptId           = p.NewDeptId
          AND  existing.SupervisorUserId = ds.SupervisorUserId
    );

    PRINT CONCAT('  DeptSupervisor rows re-mapped: ', @@ROWCOUNT);

    -- --------------------------------------------------------
    -- STEP 3 — Deactivate old duplicate departments
    -- --------------------------------------------------------
    UPDATE d
    SET    d.Active       = 0,
           d.Description  = CONCAT(
               N'[DEACTIVATED — merged into DeptId ',
               CAST(p.NewDeptId AS NVARCHAR(10)),
               N' by Migration_Department_DeduplicateHOAndFactory]'
           ),
           d.DateModified = SYSUTCDATETIME(),
           d.ModifiedBy   = @SysUserId
    FROM   dbo.Department d
    INNER JOIN @DupePairs p ON p.OldDeptId = d.DeptId;

    PRINT CONCAT('  Old duplicate depts deactivated: ', @@ROWCOUNT);
END
ELSE
    PRINT '  No duplicate pairs found — Steps 2-3 skipped (already clean).';

PRINT '';

-- ============================================================
-- STEP 4 — Describe HO-only departments
--   These are NULL-description, active departments whose Name
--   does NOT match any factory-seeded department name.
--   They belong to Head Office / MANILA LIAISON Office.
-- ============================================================
PRINT 'STEP 4: Updating HO-only department descriptions...';

UPDATE d
SET    d.Description  = N'Head Office / MANILA LIAISON Office',
       d.DateModified = SYSUTCDATETIME(),
       d.ModifiedBy   = @SysUserId
FROM   dbo.Department d
WHERE  d.Description IS NULL
  AND  d.Active = 1
  AND  NOT EXISTS (
      SELECT 1 FROM dbo.Department other
      WHERE  UPPER(LTRIM(RTRIM(other.Name))) = UPPER(LTRIM(RTRIM(d.Name)))
        AND  other.Description LIKE N'Factory section%'
        AND  other.Active = 1
        AND  other.DeptId <> d.DeptId
  );

PRINT CONCAT('  HO-only departments described: ', @@ROWCOUNT);
PRINT '';

COMMIT TRANSACTION;
PRINT '=== Migration completed successfully. ===';

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

-- V1: No active duplicate department names
-- Highlight this block and execute.
PRINT '--- Validation V1: Active duplicate names ---';
SELECT Name, COUNT(*) AS Cnt
FROM   dbo.Department
WHERE  Active = 1
GROUP BY Name
HAVING COUNT(*) > 1;
-- Expected: 0 rows
GO

-- V2: All factory-seeded departments still active with correct description
-- Highlight this block and execute.
PRINT '--- Validation V2: Factory-seeded departments ---';
SELECT DeptId, Name, Description, Active
FROM   dbo.Department
WHERE  Description LIKE N'Factory section%'
ORDER BY Name;
-- Expected: 10 rows, all Active = 1
GO

-- V3: HO-only departments described
-- Highlight this block and execute.
PRINT '--- Validation V3: HO-only departments ---';
SELECT DeptId, Name, Description, Active
FROM   dbo.Department
WHERE  Description = N'Head Office / MANILA LIAISON Office'
  AND  Active = 1
ORDER BY Name;
GO

-- V4: No BDC rows pointing at deactivated department IDs
-- Highlight this block and execute.
PRINT '--- Validation V4: BDC orphan check ---';
SELECT bdc.BranchDeptCompanyID, bdc.DepartmentID, d.Name, d.Active
FROM   dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Department d ON d.DeptId = bdc.DepartmentID
WHERE  d.Active = 0;
-- Expected: 0 rows
GO

-- V5: No employees pointing at deactivated department IDs
-- Highlight this block and execute.
PRINT '--- Validation V5: Employee orphan check ---';
SELECT e.EmpId, e.DeptId, d.Name, d.Active
FROM   dbo.Employee e
INNER JOIN dbo.Department d ON d.DeptId = e.DeptId
WHERE  d.Active = 0;
-- Expected: 0 rows
GO
