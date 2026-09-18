-- ============================================================
-- Migration: BranchDepartmentCompany — Nullable DepartmentID
-- ============================================================
-- PURPOSE
--   Phase 2 of the BranchDepartmentCompany normalization.
--   Phase 1 (Migration_BranchDepartmentCompany_CreateAndPopulate.sql)
--   already created the table and backfilled rows where Branch.DeptId
--   IS NOT NULL.  This script handles everything else:
--
--   1. Makes DepartmentID nullable       (YMC case — no department)
--   2. Replaces the non-filtered UNIQUE constraint with two filtered
--      unique indexes that handle NULL correctly
--   3. Backfills Branch+Company rows that have no Department
--   4. Marks deprecated columns with extended properties
--
-- SAFE TO RE-RUN  — every step is guarded with IF NOT EXISTS /
--                   IF EXISTS / column-nullability checks
-- ROLLBACK        — all DDL + DML wrapped in a single transaction
-- NO DATA LOSS    — zero drops of old columns (Branch.DeptId,
--                   Department.BrId); only schema additions
-- ============================================================

SET NOCOUNT ON;

BEGIN TRANSACTION;

BEGIN TRY

-- ============================================================
-- PHASE 1 — Make DepartmentID nullable
-- ============================================================
-- SQL Server requires:
--   a) Drop the FK that references the column
--   b) Drop any UNIQUE constraint on the column
--   c) ALTER the column nullability
--   d) Re-add FK (nullable FKs are valid — NULL bypasses the check)
--   e) Re-add constraints as filtered unique indexes (see Phase 2)
-- ============================================================

    -- ── 1a. Drop FK on DepartmentID (required before ALTER COLUMN) ──
    IF EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE  name          = N'FK_BranchDeptCompany_Department'
          AND  parent_object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
    )
    BEGIN
        ALTER TABLE [dbo].[BranchDepartmentCompany]
            DROP CONSTRAINT [FK_BranchDeptCompany_Department];
        PRINT 'FK_BranchDeptCompany_Department dropped.';
    END
    ELSE
        PRINT 'FK_BranchDeptCompany_Department not found — skipped.';

    -- ── 1b. Drop the non-filtered UNIQUE constraint ─────────────────
    --   The old constraint UQ_BranchDeptCompany_Binding covers
    --   (BranchID, DepartmentID, CompanyID) without a WHERE clause.
    --   A non-filtered unique index treats all NULLs as equal, so two
    --   rows with DepartmentID = NULL would violate it.  We replace it
    --   with two filtered indexes in Phase 2.
    IF EXISTS (
        SELECT 1 FROM sys.objects
        WHERE  name   = N'UQ_BranchDeptCompany_Binding'
          AND  type   = 'UQ'
          AND  parent_object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
    )
    BEGIN
        ALTER TABLE [dbo].[BranchDepartmentCompany]
            DROP CONSTRAINT [UQ_BranchDeptCompany_Binding];
        PRINT 'UQ_BranchDeptCompany_Binding constraint dropped.';
    END
    ELSE
        PRINT 'UQ_BranchDeptCompany_Binding not found — skipped.';

    -- ── 1c. ALTER DepartmentID → INT NULL ───────────────────────────
    --   Guard: only alter if the column is currently NOT NULL.
    IF EXISTS (
        SELECT 1 FROM sys.columns
        WHERE  object_id  = OBJECT_ID(N'dbo.BranchDepartmentCompany')
          AND  name       = N'DepartmentID'
          AND  is_nullable = 0          -- currently NOT NULL
    )
    BEGIN
        ALTER TABLE [dbo].[BranchDepartmentCompany]
            ALTER COLUMN [DepartmentID] INT NULL;
        PRINT 'DepartmentID altered to INT NULL.';
    END
    ELSE
        PRINT 'DepartmentID is already nullable — skipped.';

    -- ── 1d. Re-add FK on DepartmentID (now nullable) ────────────────
    --   SQL Server enforces the FK only when DepartmentID IS NOT NULL.
    --   NULL values are always permitted through a nullable FK column.
    IF NOT EXISTS (
        SELECT 1 FROM sys.foreign_keys
        WHERE  name          = N'FK_BranchDeptCompany_Department'
          AND  parent_object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
    )
    BEGIN
        ALTER TABLE [dbo].[BranchDepartmentCompany]
            ADD CONSTRAINT [FK_BranchDeptCompany_Department]
                FOREIGN KEY ([DepartmentID])
                REFERENCES  [dbo].[Department] ([DeptId]);
        PRINT 'FK_BranchDeptCompany_Department re-added (nullable).';
    END
    ELSE
        PRINT 'FK_BranchDeptCompany_Department already exists — skipped.';

-- ============================================================
-- PHASE 2 — Filtered unique indexes
-- ============================================================
-- Two filtered indexes replace the old non-filtered constraint:
--
--   UQ_BDC_WithDept  — enforces uniqueness when a Department IS set
--                      (standard case: Branch + Company + Dept)
--   UQ_BDC_NoDept    — enforces uniqueness when no Department is set
--                      (YMC case: one row per Branch + Company)
--
-- NULL is not a key value in SQL Server indexed columns, so without
-- filtered indexes a second NULL row would not be caught by the index.
-- ============================================================

    -- ── 2a. Unique index: rows WITH a department ─────────────────────
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE  name      = N'UQ_BDC_WithDept'
          AND  object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_WithDept]
            ON [dbo].[BranchDepartmentCompany] ([BranchID] ASC, [CompanyID] ASC, [DepartmentID] ASC)
            WHERE [DepartmentID] IS NOT NULL;
        PRINT 'Index UQ_BDC_WithDept created.';
    END
    ELSE
        PRINT 'Index UQ_BDC_WithDept already exists — skipped.';

    -- ── 2b. Unique index: rows WITHOUT a department (YMC) ────────────
    IF NOT EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE  name      = N'UQ_BDC_NoDept'
          AND  object_id = OBJECT_ID(N'dbo.BranchDepartmentCompany')
    )
    BEGIN
        CREATE UNIQUE NONCLUSTERED INDEX [UQ_BDC_NoDept]
            ON [dbo].[BranchDepartmentCompany] ([BranchID] ASC, [CompanyID] ASC)
            WHERE [DepartmentID] IS NULL;
        PRINT 'Index UQ_BDC_NoDept created.';
    END
    ELSE
        PRINT 'Index UQ_BDC_NoDept already exists — skipped.';

-- ============================================================
-- PHASE 3 — Backfill NULL-department rows (YMC case)
-- ============================================================
-- Phase 1 migration only inserted rows where Branch.DeptId IS NOT NULL.
-- Branches where DeptId IS NULL but ComId IS NOT NULL represent
-- companies that operate without a department assignment (e.g. YMC).
-- We insert them here with DepartmentID = NULL.
--
-- Idempotency guard: NOT EXISTS on (BranchID, CompanyID) WHERE NULL
-- ============================================================

    DECLARE @NullDeptRows INT;

    INSERT INTO [dbo].[BranchDepartmentCompany]
        ([BranchID], [DepartmentID], [CompanyID], [BranchEmail], [CreatedDate])
    SELECT
        b.[BranchId],
        NULL,                            -- no department
        b.[ComId],
        ea.[EmailAddress],
        SYSUTCDATETIME()
    FROM       [dbo].[Branch]       b
    LEFT JOIN  [dbo].[EmailAddress] ea ON ea.[EmailId] = b.[EmailId]
    WHERE
        b.[DeptId] IS NULL               -- has no department
        AND b.[ComId] IS NOT NULL        -- but does belong to a company
        AND b.[Active] = 1
        -- idempotency: skip if already backfilled
        AND NOT EXISTS (
            SELECT 1
            FROM   [dbo].[BranchDepartmentCompany] bdc
            WHERE  bdc.[BranchID]    = b.[BranchId]
              AND  bdc.[CompanyID]   = b.[ComId]
              AND  bdc.[DepartmentID] IS NULL
        );

    SET @NullDeptRows = @@ROWCOUNT;
    PRINT CONCAT('Backfilled ', @NullDeptRows, ' NULL-department row(s) (YMC case).');

-- ============================================================
-- PHASE 4 — Deprecation markers (extended properties)
-- ============================================================
-- Marks Branch.DeptId and Department.BrId as deprecated in SSMS
-- without altering data or dropping columns.
-- ============================================================

    -- ── Branch.DeptId ────────────────────────────────────────────────
    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE  major_id = OBJECT_ID(N'dbo.Branch')
          AND  minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Branch'), N'DeptId', 'ColumnId')
          AND  name     = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty
            @name      = N'MS_Description',
            @value     = N'DEPRECATED — Read/write dbo.BranchDepartmentCompany.DepartmentID instead. Do not drop until all application queries are migrated.',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'DeptId';
    ELSE
        EXEC sys.sp_updateextendedproperty
            @name      = N'MS_Description',
            @value     = N'DEPRECATED — Read/write dbo.BranchDepartmentCompany.DepartmentID instead. Do not drop until all application queries are migrated.',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'DeptId';

    PRINT 'Extended property set on Branch.DeptId.';

    -- ── Department.BrId ──────────────────────────────────────────────
    IF NOT EXISTS (
        SELECT 1 FROM sys.extended_properties
        WHERE  major_id = OBJECT_ID(N'dbo.Department')
          AND  minor_id = COLUMNPROPERTY(OBJECT_ID(N'dbo.Department'), N'BrId', 'ColumnId')
          AND  name     = N'MS_Description'
    )
        EXEC sys.sp_addextendedproperty
            @name      = N'MS_Description',
            @value     = N'DEPRECATED — Read/write dbo.BranchDepartmentCompany.BranchID instead. Do not drop until all application queries are migrated.',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'BrId';
    ELSE
        EXEC sys.sp_updateextendedproperty
            @name      = N'MS_Description',
            @value     = N'DEPRECATED — Read/write dbo.BranchDepartmentCompany.BranchID instead. Do not drop until all application queries are migrated.',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Department',
            @level2type = N'COLUMN', @level2name = N'BrId';

    PRINT 'Extended property set on Department.BrId.';

-- ── Commit ───────────────────────────────────────────────────────────
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
