-- ============================================================
-- Migration: Normalize Company / Department / Branch Hierarchy
-- ============================================================
-- PURPOSE
--   Completes the normalization of the Company → Department →
--   Branch relationship hierarchy by:
--
--   1. Backfilling dbo.BranchDepartmentCompany (BDC) from:
--        a. Department.ComId + Department.BrId (dept-driven bindings)
--        b. Branch.ComId WHERE Branch.DeptId IS NULL (company-only rows)
--
--   2. Marking the following columns as DEPRECATED via SQL Server
--      extended properties (MS_Description):
--        • Branch.ComId    — superseded by BDC.CompanyID
--        • Branch.DeptId   — superseded by BDC.DepartmentID
--        • Department.ComId — superseded by BDC.CompanyID
--        • Department.BrId  — superseded by BDC.BranchID
--
-- WHAT THIS SCRIPT DOES NOT DO
--   • Does NOT drop any columns (existing C# code still reads them).
--   • Does NOT drop FK constraints (integrity still enforced while
--     the app transitions to BDC-only reads).
--   • Does NOT modify any existing BDC rows.
--
-- FUTURE CLEANUP (Phase 2 — do after all C# joins are migrated)
--   Once no query in the application reads Branch.ComId, Branch.DeptId,
--   Department.ComId, or Department.BrId, run the Phase 2 cleanup:
--     ALTER TABLE dbo.Branch     DROP CONSTRAINT FK_Branch_Company;
--     ALTER TABLE dbo.Branch     DROP CONSTRAINT FK_Branch_Department;
--     ALTER TABLE dbo.Department DROP CONSTRAINT FK_Department_Company;
--     ALTER TABLE dbo.Department DROP CONSTRAINT FK_Department_Branch;
--     DROP INDEX IX_Branch_ComId      ON dbo.Branch;
--     DROP INDEX IX_Branch_DeptId     ON dbo.Branch;
--     DROP INDEX IX_Department_ComId  ON dbo.Department;
--     DROP INDEX IX_Department_BrId   ON dbo.Department;
--     ALTER TABLE dbo.Branch     DROP COLUMN ComId, DeptId;
--     ALTER TABLE dbo.Department DROP COLUMN ComId, BrId;
--
-- PREREQUISITES
--   Run these before this script:
--     1. Migration_BranchDepartmentCompany_CreateAndPopulate.sql
--     2. Migration_BranchDepartmentCompany_NullableDept.sql
--     3. Migration_FactoryNormalization_CompanyBranchHierarchy.sql
--
-- SAFE TO RE-RUN   — all inserts guarded by NOT EXISTS
-- ROLLBACK         — wrapped in a single transaction
-- NO DATA LOSS     — no DROP, no DELETE on legitimate data
-- TEST ON BACKUP   — always run against a restored backup first
-- ============================================================

SET NOCOUNT ON;

-- ── Pre-flight checks ─────────────────────────────────────────

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE  object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany') AND type = 'U'
)
BEGIN
    RAISERROR(
        'PREREQUISITE MISSING: dbo.BranchDepartmentCompany does not exist. Run Migration_BranchDepartmentCompany_CreateAndPopulate.sql first.',
        16, 1
    );
    RETURN;
END

-- Verify DepartmentID is nullable (NullableDept migration was applied)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
      AND  name      = N'DepartmentID'
      AND  is_nullable = 1
)
BEGIN
    RAISERROR(
        'PREREQUISITE MISSING: BDC.DepartmentID is not nullable. Run Migration_BranchDepartmentCompany_NullableDept.sql first.',
        16, 1
    );
    RETURN;
END

-- ─────────────────────────────────────────────────────────────
DECLARE @PropValue NVARCHAR(500);

BEGIN TRANSACTION;
BEGIN TRY

    DECLARE @Inserted INT;

    -- ──────────────────────────────────────────────────────────
    -- PHASE 1: Backfill from Department.ComId + Department.BrId
    -- ──────────────────────────────────────────────────────────
    -- Some departments were assigned directly to a company and
    -- branch via Department.ComId and Department.BrId. These
    -- bindings must exist in BDC.
    -- ──────────────────────────────────────────────────────────

    INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
    SELECT
        d.BrId   AS BranchID,
        d.DeptId AS DepartmentID,
        d.ComId  AS CompanyID
    FROM dbo.Department d
    WHERE d.BrId  IS NOT NULL
      AND d.ComId IS NOT NULL
      -- Only migrate to a branch/company that actually exist
      AND EXISTS (SELECT 1 FROM dbo.Branch  b WHERE b.BranchId = d.BrId)
      AND EXISTS (SELECT 1 FROM dbo.Company c WHERE c.ComId    = d.ComId)
      -- Idempotency: skip rows already in BDC
      AND NOT EXISTS (
          SELECT 1
          FROM   dbo.BranchDepartmentCompany bdc
          WHERE  bdc.BranchID     = d.BrId
            AND  bdc.DepartmentID = d.DeptId
            AND  bdc.CompanyID    = d.ComId
      );

    SET @Inserted = @@ROWCOUNT;
    PRINT CONCAT('[Phase 1] Department.BrId+ComId  → ', @Inserted, ' new BDC row(s) inserted.');

    -- ──────────────────────────────────────────────────────────
    -- PHASE 2: Backfill company-only branches (DeptId IS NULL)
    -- ──────────────────────────────────────────────────────────
    -- Branches that have a Company set but no Department (e.g.
    -- YAKULT EL SALVADOR → YMC). These map to a BDC row with
    -- DepartmentID = NULL.
    -- ──────────────────────────────────────────────────────────

    INSERT INTO dbo.BranchDepartmentCompany (BranchID, DepartmentID, CompanyID)
    SELECT
        b.BranchId AS BranchID,
        NULL       AS DepartmentID,
        b.ComId    AS CompanyID
    FROM dbo.Branch b
    WHERE b.ComId  IS NOT NULL
      AND b.DeptId IS NULL       -- company-only; no department attachment
      AND b.Active = 1
      -- Only migrate to a company that exists
      AND EXISTS (SELECT 1 FROM dbo.Company c WHERE c.ComId = b.ComId)
      -- Idempotency: skip if a company-only row already exists for this branch+company
      AND NOT EXISTS (
          SELECT 1
          FROM   dbo.BranchDepartmentCompany bdc
          WHERE  bdc.BranchID     = b.BranchId
            AND  bdc.CompanyID    = b.ComId
            AND  bdc.DepartmentID IS NULL
      );

    SET @Inserted = @@ROWCOUNT;
    PRINT CONCAT('[Phase 2] Branch (no dept) company-only → ', @Inserted, ' new BDC row(s) inserted.');

    -- ──────────────────────────────────────────────────────────
    -- PHASE 3: Mark legacy columns as deprecated
    --
    -- Uses SQL Server Extended Properties (MS_Description) so
    -- tools like SSMS show the deprecation note on hover.
    -- Each column is handled with an IF EXISTS guard so the
    -- script is safe to re-run (updates instead of re-adds).
    -- ──────────────────────────────────────────────────────────

    -- Helper: add or update extended property on a column
    -- (SQL Server has no UPSERT for extended properties,
    --  so we check existence and branch accordingly.)

    -- ── Branch.ComId ─────────────────────────────────────────
    SET @PropValue = N'DEPRECATED — superseded by dbo.BranchDepartmentCompany.CompanyID. Do not use in new joins. Kept for transitional C# compatibility.';
    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            'MS_Description', 'SCHEMA', 'dbo', 'TABLE', 'Branch', 'COLUMN', 'ComId')
    )
        EXEC sys.sp_updateextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'ComId';
    ELSE
        EXEC sys.sp_addextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'ComId';

    PRINT '[Phase 3] Branch.ComId    — deprecated property set.';

    -- ── Branch.DeptId ─────────────────────────────────────────
    SET @PropValue = N'DEPRECATED — superseded by dbo.BranchDepartmentCompany.DepartmentID. Do not use in new joins. Kept for transitional C# compatibility.';
    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            'MS_Description', 'SCHEMA', 'dbo', 'TABLE', 'Branch', 'COLUMN', 'DeptId')
    )
        EXEC sys.sp_updateextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'DeptId';
    ELSE
        EXEC sys.sp_addextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'DeptId';

    PRINT '[Phase 3] Branch.DeptId   — deprecated property set.';

    -- ── Department.ComId ──────────────────────────────────────
    SET @PropValue = N'DEPRECATED — superseded by dbo.BranchDepartmentCompany.CompanyID. Do not use in new joins. Kept for transitional C# compatibility.';
    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            'MS_Description', 'SCHEMA', 'dbo', 'TABLE', 'Department', 'COLUMN', 'ComId')
    )
        EXEC sys.sp_updateextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'ComId';
    ELSE
        EXEC sys.sp_addextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'ComId';

    PRINT '[Phase 3] Department.ComId — deprecated property set.';

    -- ── Department.BrId ───────────────────────────────────────
    SET @PropValue = N'DEPRECATED — superseded by dbo.BranchDepartmentCompany.BranchID. Do not use in new joins. Kept for transitional C# compatibility.';
    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            'MS_Description', 'SCHEMA', 'dbo', 'TABLE', 'Department', 'COLUMN', 'BrId')
    )
        EXEC sys.sp_updateextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'BrId';
    ELSE
        EXEC sys.sp_addextendedproperty
            @name       = N'MS_Description',
            @value      = @PropValue,
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'BrId';

    PRINT '[Phase 3] Department.BrId  — deprecated property set.';

    -- ──────────────────────────────────────────────────────────

    COMMIT TRANSACTION;
    PRINT '=== Migration completed successfully. Run validation queries below. ===';

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
-- VALIDATION QUERIES
-- Run these after the migration to confirm data integrity.
-- ============================================================

-- V1: All active Branches with a Company should have at least
--     one BDC row (with or without a Department).
SELECT
    b.BranchId,
    b.Name      AS BranchName,
    b.BranchType,
    c.Name      AS LegacyCompany,
    COUNT(bdc.BranchDeptCompanyID) AS BdcRowCount
FROM       dbo.Branch  b
LEFT JOIN  dbo.Company c   ON c.ComId    = b.ComId
LEFT JOIN  dbo.BranchDepartmentCompany bdc ON bdc.BranchID = b.BranchId
WHERE b.Active = 1
  AND b.ComId IS NOT NULL
GROUP BY b.BranchId, b.Name, b.BranchType, c.Name
HAVING COUNT(bdc.BranchDeptCompanyID) = 0
ORDER BY b.Name;
-- Expected: 0 rows (every company-assigned branch has a BDC entry)

-- ─────────────────────────────────────────────────────────────

-- V2: Departments with BrId set should have a matching BDC row.
SELECT
    d.DeptId,
    d.Name   AS DepartmentName,
    d.BrId   AS LegacyBranchId,
    d.ComId  AS LegacyCompanyId,
    bdc.BranchDeptCompanyID
FROM       dbo.Department d
LEFT JOIN  dbo.BranchDepartmentCompany bdc
               ON  bdc.BranchID     = d.BrId
               AND bdc.DepartmentID = d.DeptId
               AND bdc.CompanyID    = d.ComId
WHERE d.BrId  IS NOT NULL
  AND d.ComId IS NOT NULL
  AND bdc.BranchDeptCompanyID IS NULL
ORDER BY d.Name;
-- Expected: 0 rows (all dept-level bindings are in BDC)

-- ─────────────────────────────────────────────────────────────

-- V3: No duplicate BDC rows with a department.
SELECT BranchID, CompanyID, DepartmentID, COUNT(*) AS Cnt
FROM   dbo.BranchDepartmentCompany
WHERE  DepartmentID IS NOT NULL
GROUP BY BranchID, CompanyID, DepartmentID
HAVING COUNT(*) > 1;
-- Expected: 0 rows

-- V4: No duplicate BDC rows without a department (company-only).
SELECT BranchID, CompanyID, COUNT(*) AS Cnt
FROM   dbo.BranchDepartmentCompany
WHERE  DepartmentID IS NULL
GROUP BY BranchID, CompanyID
HAVING COUNT(*) > 1;
-- Expected: 0 rows

-- ─────────────────────────────────────────────────────────────

-- V5: BDC referential integrity — no orphaned BranchID.
SELECT bdc.BranchDeptCompanyID, bdc.BranchID
FROM   dbo.BranchDepartmentCompany bdc
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Branch b WHERE b.BranchId = bdc.BranchID);
-- Expected: 0 rows

-- V6: BDC referential integrity — no orphaned DepartmentID.
SELECT bdc.BranchDeptCompanyID, bdc.DepartmentID
FROM   dbo.BranchDepartmentCompany bdc
WHERE  bdc.DepartmentID IS NOT NULL
  AND  NOT EXISTS (SELECT 1 FROM dbo.Department d WHERE d.DeptId = bdc.DepartmentID);
-- Expected: 0 rows

-- V7: BDC referential integrity — no orphaned CompanyID.
SELECT bdc.BranchDeptCompanyID, bdc.CompanyID
FROM   dbo.BranchDepartmentCompany bdc
WHERE  NOT EXISTS (SELECT 1 FROM dbo.Company c WHERE c.ComId = bdc.CompanyID);
-- Expected: 0 rows

-- ─────────────────────────────────────────────────────────────

-- V8: Full BDC view — current state of all bindings.
SELECT
    bdc.BranchDeptCompanyID,
    co.Name  AS Company,
    b.Name   AS Branch,
    b.BranchType,
    ISNULL(d.Name, N'(No Department)') AS Department,
    bdc.CreatedDate
FROM       dbo.BranchDepartmentCompany bdc
JOIN       dbo.Company    co ON co.ComId    = bdc.CompanyID
JOIN       dbo.Branch     b  ON b.BranchId  = bdc.BranchID
LEFT JOIN  dbo.Department d  ON d.DeptId    = bdc.DepartmentID
ORDER BY   co.Name, b.Name, d.Name;

-- ─────────────────────────────────────────────────────────────

-- V9: Deprecated column — confirm extended properties are set.
SELECT
    t.name  AS TableName,
    c.name  AS ColumnName,
    ep.value AS DeprecationNote
FROM sys.extended_properties ep
JOIN sys.columns c ON ep.major_id = c.object_id AND ep.minor_id = c.column_id
JOIN sys.tables  t ON c.object_id = t.object_id
WHERE ep.name = 'MS_Description'
  AND t.name  IN ('Branch', 'Department')
  AND c.name  IN ('ComId', 'DeptId', 'BrId')
ORDER BY t.name, c.name;
-- Expected: 4 rows (Branch.ComId, Branch.DeptId, Department.ComId, Department.BrId)
