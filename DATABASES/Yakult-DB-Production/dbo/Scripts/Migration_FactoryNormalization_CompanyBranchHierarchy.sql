-- ============================================================
-- Migration: Factory Normalization — Company / Branch Hierarchy
-- ============================================================
-- PURPOSE
--   Corrects the incorrect modelling of "Factory" as a Company.
--   In reality:
--     • YPI and YMC are the only Companies.
--     • "Factory" is a BranchType, not a Company.
--     • CALAMBA PLANT       → Branch, BranchType = 'Factory', Company = YPI
--     • YAKULT EL SALVADOR  → Branch, BranchType = 'Factory', Company = YMC
--   Factory-specific sections (SHIPPING SECTION, PRODUCTION SECTION,
--   etc.) are Departments linked to CALAMBA PLANT via
--   dbo.BranchDepartmentCompany.
--
-- PHASES
--   1  Detect and remove "Factory" as a Company row (if present).
--      Reassign affected Branches / Departments to correct companies.
--   2  Classify CALAMBA PLANT and YAKULT EL SALVADOR as Factory branches.
--   3  Seed factory-specific Department rows (idempotent).
--   4  Link factory Departments → CALAMBA PLANT → YPI via BDC table.
--   5  Link YAKULT EL SALVADOR → YMC via BDC table (no-dept row).
--   6  Validation queries.
--
-- PREREQUISITES (run these first, in order)
--   1. Migration_Branch_AddBranchType.sql
--   2. Migration_BranchDepartmentCompany_CreateAndPopulate.sql
--   3. Migration_BranchDepartmentCompany_NullableDept.sql
--
-- SAFE TO RE-RUN   — all steps are idempotent
-- ROLLBACK         — all DDL + DML wrapped in a single transaction
-- NO DATA LOSS     — no columns dropped, no hard deletes of legitimate data
-- TEST ON BACKUP   — always run against a restored backup first
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

-- ── Pre-flight checks (inside TRY — RAISERROR here rolls back via CATCH) ──────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.Branch')
      AND  name      = N'BranchType'
)
    RAISERROR('PREREQUISITE MISSING: BranchType column not found on dbo.Branch. Run Migration_Branch_AddBranchType.sql before this script.', 16, 1);

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE  object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
      AND  type = 'U'
)
    RAISERROR('PREREQUISITE MISSING: dbo.BranchDepartmentCompany does not exist. Run Migration_BranchDepartmentCompany_CreateAndPopulate.sql before this script.', 16, 1);

PRINT '=== Pre-flight checks passed. Starting migration. ===';
PRINT '';

-- ============================================================
-- ALL VARIABLE DECLARATIONS — must be at the top of the batch
-- so that no DECLARE appears after any conditional branching.
-- SQL Server does not allow jumping over DECLARE statements.
-- ============================================================

DECLARE @FactoryComId       INT  = NULL;
DECLARE @YpiComId           INT  = NULL;
DECLARE @YmcComId           INT  = NULL;
DECLARE @CalambaBranchId    INT  = NULL;
DECLARE @ElSalvadorBranchId INT  = NULL;
DECLARE @SysUserId          INT  = 8;      -- system/admin user for audit columns

-- @FactorySections is used in both Phase 3 (INSERT dept) and
-- Phase 4 (INSERT BDC rows). Declare and populate here so both
-- phases can reference it regardless of which IFs are taken.
DECLARE @FactorySections TABLE (SectionName NVARCHAR(150));

INSERT INTO @FactorySections (SectionName) VALUES
    (N'SHIPPING SECTION'),
    (N'UTILITY CONTROL SECTION'),
    (N'BOTTLING SECTION'),
    (N'BOTTLEMAKING SECTION'),
    (N'DISS. & PAST. SECTION'),
    (N'GEN. AFFAIRS SECTION'),
    (N'PRODUCTION SECTION'),
    (N'PURCH. & MATERIALS PROP.'),
    (N'QUALITY CONTROL SECTION'),
    (N'ENG''G. & MAINT. SECTION');

-- Look up all IDs now; every phase reads from these variables.
SELECT @FactoryComId = ComId
FROM   dbo.Company
WHERE  UPPER(LTRIM(RTRIM(Name))) IN (N'FACTORY', N'FACTORIES');

SELECT @YpiComId = ComId FROM dbo.Company WHERE UPPER(LTRIM(RTRIM(Name))) = N'YPI';
SELECT @YmcComId = ComId FROM dbo.Company WHERE UPPER(LTRIM(RTRIM(Name))) = N'YMC';

SELECT @CalambaBranchId = BranchId
FROM   dbo.Branch
WHERE  UPPER(LTRIM(RTRIM(Name))) = N'CALAMBA PLANT'
  AND  Active = 1;

SELECT @ElSalvadorBranchId = BranchId
FROM   dbo.Branch
WHERE  UPPER(LTRIM(RTRIM(Name))) LIKE N'%EL SALVADOR%'
  AND  Active = 1;

-- ============================================================
-- PHASE 1 — Detect and Remove "Factory" as a Company
-- ============================================================
-- The CSV header (YPI-YMC-FACTORY.csv) groups "FACTORY" alongside
-- YPI and YMC as if it were a company.  This is incorrect.
-- If a Company row with Name = 'Factory' / 'FACTORY' exists in
-- dbo.Company, we reassign its children and then delete it.
-- ============================================================

IF @FactoryComId IS NOT NULL
BEGIN
    PRINT CONCAT('PHASE 1: Factory company detected (ComId = ', @FactoryComId, '). Reassigning children and removing the row.');

    -- Note: dbo.Branch has no ComId column — branch-company membership is
    -- managed exclusively through dbo.BranchDepartmentCompany (Phase 1D/1E).

    -- Note: dbo.Department has no ComId column — company linkage for departments
    -- is managed exclusively through dbo.BranchDepartmentCompany (Phase 1E).

    -- ── 1E: Reassign BranchDepartmentCompany rows ──────────────────
    IF @YpiComId IS NOT NULL
    BEGIN
        UPDATE dbo.BranchDepartmentCompany
        SET    CompanyID   = @YpiComId,
               UpdatedDate = SYSUTCDATETIME()
        WHERE  CompanyID   = @FactoryComId;

        PRINT CONCAT('  Reassigned ', @@ROWCOUNT, ' BranchDepartmentCompany row(s) from Factory to YPI.');
    END

    -- ── 1F: Delete Factory company only if all references cleared ──
    IF NOT EXISTS (SELECT 1 FROM dbo.BranchDepartmentCompany WHERE CompanyID = @FactoryComId)
    BEGIN
        DELETE FROM dbo.Company WHERE ComId = @FactoryComId;
        PRINT CONCAT('  Deleted Factory company row (ComId=', @FactoryComId, ').');
    END
    ELSE
        PRINT CONCAT('  WARNING: Factory company (ComId=', @FactoryComId, ') still has dependent rows. The Company row was NOT deleted. Investigate before re-running.');
END
ELSE
    PRINT 'PHASE 1: No "Factory" company row found — skipped.';

PRINT '';

-- ============================================================
-- PHASE 2 — Classify CALAMBA PLANT and YAKULT EL SALVADOR
--            as BranchType = 'Factory'
-- ============================================================
-- Sets both the legacy flag columns and the new BranchType column.
-- Only updates rows that are not already correctly tagged.
-- ============================================================

PRINT 'PHASE 2: Classifying factory branches...';

UPDATE dbo.Branch
SET    IsFactory     = 1,
       IsDepot       = 0,
       IsCenter      = 0,
       IsDistributor = 0,
       BranchType    = N'Factory',
       DateModified  = SYSUTCDATETIME(),
       ModifiedBy    = @SysUserId
WHERE  UPPER(LTRIM(RTRIM(Name))) = N'CALAMBA PLANT'
  AND (IsFactory IS NULL OR IsFactory = 0 OR BranchType IS NULL OR BranchType <> N'Factory');

PRINT CONCAT('  Tagged ', @@ROWCOUNT, ' CALAMBA PLANT row(s) as Factory.');

UPDATE dbo.Branch
SET    IsFactory     = 1,
       IsDepot       = 0,
       IsCenter      = 0,
       IsDistributor = 0,
       BranchType    = N'Factory',
       DateModified  = SYSUTCDATETIME(),
       ModifiedBy    = @SysUserId
WHERE  UPPER(LTRIM(RTRIM(Name))) LIKE N'%EL SALVADOR%'
  AND (IsFactory IS NULL OR IsFactory = 0 OR BranchType IS NULL OR BranchType <> N'Factory');

PRINT CONCAT('  Tagged ', @@ROWCOUNT, ' YAKULT EL SALVADOR row(s) as Factory.');
PRINT '';

-- Re-resolve @CalambaBranchId in case Phase 1 just created or renamed it
-- (Phase 2 may have also just updated the row; BranchId is stable, so this
-- is a no-op refresh — the value will be the same or will now be found.)
IF @CalambaBranchId IS NULL
BEGIN
    SELECT @CalambaBranchId = BranchId
    FROM   dbo.Branch
    WHERE  UPPER(LTRIM(RTRIM(Name))) = N'CALAMBA PLANT'
      AND  Active = 1;
END

IF @ElSalvadorBranchId IS NULL
BEGIN
    SELECT @ElSalvadorBranchId = BranchId
    FROM   dbo.Branch
    WHERE  UPPER(LTRIM(RTRIM(Name))) LIKE N'%EL SALVADOR%'
      AND  Active = 1;
END

-- ============================================================
-- PHASE 3 — Seed Factory-Specific Departments (CALAMBA PLANT)
-- ============================================================
-- Source: YPI-YMC-FACTORY.csv (right-side section, BranchType: Factory)
-- These sections operate within CALAMBA PLANT under YPI.
-- Rows are inserted only when no matching Name already exists.
-- ============================================================

PRINT 'PHASE 3: Seeding factory departments...';

IF @YpiComId IS NOT NULL
BEGIN
    -- Stamp the canonical description on any existing rows that match by name
    -- but were seeded without a description (e.g. from an earlier migration run).
    UPDATE d
    SET    d.Description  = N'Factory section — CALAMBA PLANT (seeded by Migration_FactoryNormalization)',
           d.DateModified = SYSUTCDATETIME(),
           d.ModifiedBy   = @SysUserId
    FROM   dbo.Department d
    INNER JOIN @FactorySections fs
            ON UPPER(LTRIM(RTRIM(d.Name))) = UPPER(LTRIM(RTRIM(fs.SectionName)))
    WHERE  d.Description IS NULL
      AND  d.Active = 1;

    PRINT CONCAT('  Stamped description on ', @@ROWCOUNT, ' existing factory department(s).');

    -- Insert any that don't exist at all yet
    INSERT INTO dbo.Department (Name, Description, CreatedBy, Active)
    SELECT
        fs.SectionName,
        N'Factory section — CALAMBA PLANT (seeded by Migration_FactoryNormalization)',
        @SysUserId,
        1
    FROM @FactorySections fs
    WHERE NOT EXISTS (
        SELECT 1
        FROM   dbo.Department d
        WHERE  UPPER(LTRIM(RTRIM(d.Name))) = UPPER(LTRIM(RTRIM(fs.SectionName)))
    );

    PRINT CONCAT('  Inserted ', @@ROWCOUNT, ' new factory department(s).');
END
ELSE
    PRINT '  WARNING: YPI company not found. Factory department seeding skipped.';

PRINT '';

-- ============================================================
-- PHASE 4 — Link Factory Departments → CALAMBA PLANT → YPI
--            via dbo.BranchDepartmentCompany
-- ============================================================

PRINT 'PHASE 4: Linking factory sections to CALAMBA PLANT in BranchDepartmentCompany...';

IF @CalambaBranchId IS NULL
    PRINT '  WARNING: CALAMBA PLANT branch not found or inactive. Phase 4 skipped.'
ELSE IF @YpiComId IS NULL
    PRINT '  WARNING: YPI company not found. Phase 4 skipped.'
ELSE
BEGIN
    INSERT INTO dbo.BranchDepartmentCompany
        (BranchID, DepartmentID, CompanyID, CreatedDate)
    SELECT
        @CalambaBranchId,
        d.DeptId,
        @YpiComId,
        SYSUTCDATETIME()
    FROM       @FactorySections fs
    INNER JOIN dbo.Department   d
            ON UPPER(LTRIM(RTRIM(d.Name))) = UPPER(LTRIM(RTRIM(fs.SectionName)))
           AND d.Active = 1
    WHERE NOT EXISTS (
        SELECT 1
        FROM   dbo.BranchDepartmentCompany bdc
        WHERE  bdc.BranchID     = @CalambaBranchId
          AND  bdc.DepartmentID = d.DeptId
          AND  bdc.CompanyID    = @YpiComId
    );

    PRINT CONCAT('  Linked ', @@ROWCOUNT, ' factory section(s) to CALAMBA PLANT.');
END

PRINT '';

-- ============================================================
-- PHASE 5 — Link YAKULT EL SALVADOR → YMC (company-only BDC row)
-- ============================================================
-- DepartmentID = NULL is intentional here (no dept assignment).
-- The UQ_BDC_NoDept filtered unique index handles this correctly.
-- ============================================================

PRINT 'PHASE 5: Linking YAKULT EL SALVADOR to YMC in BranchDepartmentCompany...';

IF @ElSalvadorBranchId IS NULL
    PRINT '  WARNING: YAKULT EL SALVADOR branch not found or inactive. Phase 5 skipped.'
ELSE IF @YmcComId IS NULL
    PRINT '  WARNING: YMC company not found. Phase 5 skipped.'
ELSE
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM   dbo.BranchDepartmentCompany
        WHERE  BranchID     = @ElSalvadorBranchId
          AND  CompanyID    = @YmcComId
          AND  DepartmentID IS NULL
    )
    BEGIN
        INSERT INTO dbo.BranchDepartmentCompany
            (BranchID, DepartmentID, CompanyID, CreatedDate)
        VALUES
            (@ElSalvadorBranchId, NULL, @YmcComId, SYSUTCDATETIME());

        PRINT '  Linked YAKULT EL SALVADOR to YMC (no-department row).';
    END
    ELSE
        PRINT '  YAKULT EL SALVADOR to YMC already in BranchDepartmentCompany — skipped.';
END

PRINT '';

-- ── Commit ────────────────────────────────────────────────────
    COMMIT TRANSACTION;
    PRINT '=== Factory normalization migration completed successfully. ===';
    PRINT '';

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

-- ── 6A: Confirm no "Factory" company row remains ─────────────
-- Highlight this block and execute.
PRINT '--- Validation 6A: No Factory company ---';
SELECT ComId, Name, Active
FROM   dbo.Company
WHERE  UPPER(LTRIM(RTRIM(Name))) IN (N'FACTORY', N'FACTORIES');
-- Expected: 0 rows
GO

-- ── 6B: All factory branches correctly classified ────────────
-- Highlight this block and execute.
PRINT '--- Validation 6B: Factory branch classifications ---';
SELECT
    b.BranchId,
    b.Name        AS BranchName,
    b.BranchType,
    b.IsFactory,
    b.Active
FROM       dbo.Branch  b
WHERE      b.BranchType = N'Factory'
ORDER BY   b.Name;
-- Expected: CALAMBA PLANT and YAKULT EL SALVADOR, both BranchType = 'Factory'
-- (Company linkage is in BranchDepartmentCompany — see 6D/6E)
GO

-- ── 6C: Factory section departments exist under YPI ──────────
-- Highlight this block and execute.
PRINT '--- Validation 6C: Factory departments ---';
SELECT
    d.DeptId,
    d.Name        AS DepartmentName,
    d.Active
FROM       dbo.Department d
WHERE      d.Name IN (
               N'SHIPPING SECTION',
               N'UTILITY CONTROL SECTION',
               N'BOTTLING SECTION',
               N'BOTTLEMAKING SECTION',
               N'DISS. & PAST. SECTION',
               N'GEN. AFFAIRS SECTION',
               N'PRODUCTION SECTION',
               N'PURCH. & MATERIALS PROP.',
               N'QUALITY CONTROL SECTION',
               N'ENG''G. & MAINT. SECTION'
           )
ORDER BY   d.Name;
-- Expected: 10 rows (company linkage is via BranchDepartmentCompany — see 6D)
GO

-- ── 6D: BDC linkages for CALAMBA PLANT factory sections ──────
-- Highlight this block and execute.
PRINT '--- Validation 6D: BranchDepartmentCompany — CALAMBA PLANT ---';
SELECT
    bdc.BranchDeptCompanyID,
    b.Name        AS BranchName,
    b.BranchType,
    d.Name        AS DepartmentName,
    c.Name        AS CompanyName,
    bdc.BranchEmail,
    bdc.CreatedDate
FROM       dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Branch                  b ON b.BranchId = bdc.BranchID
INNER JOIN dbo.Department              d ON d.DeptId   = bdc.DepartmentID
INNER JOIN dbo.Company                 c ON c.ComId    = bdc.CompanyID
WHERE      UPPER(LTRIM(RTRIM(b.Name))) = N'CALAMBA PLANT'
ORDER BY   d.Name;
-- Expected: 10 rows (one per factory section)
GO

-- ── 6E: BDC linkage for YAKULT EL SALVADOR ───────────────────
-- Highlight this block and execute.
PRINT '--- Validation 6E: BranchDepartmentCompany — YAKULT EL SALVADOR ---';
SELECT
    bdc.BranchDeptCompanyID,
    b.Name        AS BranchName,
    b.BranchType,
    bdc.DepartmentID,   -- should be NULL
    c.Name        AS CompanyName,
    bdc.CreatedDate
FROM       dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Branch                  b ON b.BranchId = bdc.BranchID
INNER JOIN dbo.Company                 c ON c.ComId    = bdc.CompanyID
WHERE      UPPER(LTRIM(RTRIM(b.Name))) LIKE N'%EL SALVADOR%';
-- Expected: 1 row, DepartmentID = NULL, CompanyName = 'YMC'
GO

-- ── 6F: Full hierarchy — Company → BranchType → Branch → Department
-- Highlight this block and execute.
PRINT '--- Validation 6F: Full hierarchy ---';
SELECT
    c.Name        AS Company,
    b.BranchType,
    b.Name        AS Branch,
    ISNULL(d.Name, '(no department)') AS Department,
    bdc.BranchEmail
FROM       dbo.BranchDepartmentCompany bdc
INNER JOIN dbo.Branch                  b ON b.BranchId = bdc.BranchID
INNER JOIN dbo.Company                 c ON c.ComId    = bdc.CompanyID
LEFT  JOIN dbo.Department              d ON d.DeptId   = bdc.DepartmentID
WHERE      b.Active = 1
ORDER BY   c.Name, b.BranchType, b.Name, d.Name;
GO

-- ── 6G: BranchType distribution ──────────────────────────────
-- Highlight this block and execute.
PRINT '--- Validation 6G: Branch count by BranchType ---';
SELECT
    ISNULL(BranchType, '(null)') AS BranchType,
    COUNT(*)                      AS BranchCount
FROM   dbo.Branch
WHERE  Active = 1
GROUP BY BranchType
ORDER BY BranchCount DESC;
GO

-- ── 6H: Orphaned company references ──────────────────────────
-- Highlight this block and execute.
PRINT '--- Validation 6H: Orphaned BranchDepartmentCompany company references ---';
SELECT bdc.BranchDeptCompanyID, bdc.CompanyID AS OrphanComId
FROM   dbo.BranchDepartmentCompany bdc
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Company WHERE ComId = bdc.CompanyID);
-- Expected: 0 rows
GO

-- ============================================================
-- BATCH 3 OF 3 — Duplicate Cleanup
-- Run only if the migration was executed more than once and
-- Validation 6C / 6D show duplicate rows.
-- Highlight this entire block and execute as a unit.
-- ============================================================
BEGIN TRANSACTION;
BEGIN TRY

-- ── Step 1: Identify duplicate Department rows ────────────────
-- For each factory section name that appears more than once,
-- keep the row with the lowest DeptId (the original) and
-- collect the higher-Id duplicates for deletion.
DECLARE @DupDeptIds TABLE (DeptId INT);

INSERT INTO @DupDeptIds (DeptId)
SELECT d.DeptId
FROM   dbo.Department d
WHERE  UPPER(LTRIM(RTRIM(d.Name))) IN (
           N'SHIPPING SECTION',
           N'UTILITY CONTROL SECTION',
           N'BOTTLING SECTION',
           N'BOTTLEMAKING SECTION',
           N'DISS. & PAST. SECTION',
           N'GEN. AFFAIRS SECTION',
           N'PRODUCTION SECTION',
           N'PURCH. & MATERIALS PROP.',
           N'QUALITY CONTROL SECTION',
           N'ENG''G. & MAINT. SECTION'
       )
  AND  EXISTS (
           SELECT 1 FROM dbo.Department d2
           WHERE  UPPER(LTRIM(RTRIM(d2.Name))) = UPPER(LTRIM(RTRIM(d.Name)))
             AND  d2.DeptId < d.DeptId
       );

DECLARE @DupCount INT = (SELECT COUNT(*) FROM @DupDeptIds);
PRINT CONCAT('Duplicate Department rows identified: ', @DupCount);

-- ── Step 2: Delete BDC rows that reference those duplicate depts ─
DELETE FROM dbo.BranchDepartmentCompany
WHERE  DepartmentID IN (SELECT DeptId FROM @DupDeptIds);

PRINT CONCAT('Duplicate BDC rows removed: ', @@ROWCOUNT);

-- ── Step 3: Delete the stray EL SALVADOR + non-null dept BDC row ─
-- BDC row 179: YAKULT EL SALVADOR linked to a factory section dept
-- under YMC — this is incorrect and should not exist.
DELETE FROM dbo.BranchDepartmentCompany
WHERE  BranchDeptCompanyID = 179;

PRINT CONCAT('Stray EL SALVADOR BDC row removed: ', @@ROWCOUNT);

-- ── Step 4: Delete the duplicate Department rows ──────────────
DELETE FROM dbo.Department
WHERE  DeptId IN (SELECT DeptId FROM @DupDeptIds);

PRINT CONCAT('Duplicate Department rows removed: ', @@ROWCOUNT);

COMMIT TRANSACTION;
PRINT '=== Cleanup completed successfully. Re-run Batch 2 validations to confirm. ===';

END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrMsg      NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrSeverity INT            = ERROR_SEVERITY();
    DECLARE @ErrState    INT            = ERROR_STATE();

    PRINT CONCAT('=== Cleanup FAILED: ', @ErrMsg, ' ===');
    RAISERROR(@ErrMsg, @ErrSeverity, @ErrState);
END CATCH;
GO
