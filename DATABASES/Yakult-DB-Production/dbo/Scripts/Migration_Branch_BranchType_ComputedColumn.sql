-- ============================================================
-- Migration: Convert Branch.BranchType to a Persisted Computed Column
-- ============================================================
-- PURPOSE
--   BranchType was added as a plain NVARCHAR(50) column and
--   backfilled via a one-time UPDATE. Any branch inserted afterward
--   without an explicit BranchType value gets NULL.
--
--   This script converts BranchType into a PERSISTED computed column
--   derived directly from the existing flag columns (IsDistributor,
--   IsDepot, IsCenter, IsFactory).  The value is always correct —
--   no manual UPDATE or trigger required.
--
-- SAFE TO RE-RUN   — guarded by sys.columns checks
-- NO DATA LOSS     — value is recomputed from flags that still exist
-- ============================================================

SET NOCOUNT ON;

-- ── Pre-flight: verify flag columns exist ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Branch') AND name = N'IsCenter')
BEGIN
    RAISERROR('PREREQUISITE MISSING: IsCenter column not found on dbo.Branch.', 16, 1);
    RETURN;
END

-- ── Step 1: Drop IX_Branch_BranchType if it references the column ─
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch') AND name = N'IX_Branch_BranchType')
BEGIN
    DROP INDEX IX_Branch_BranchType ON dbo.Branch;
    PRINT 'Dropped IX_Branch_BranchType.';
END

-- ── Step 2: Drop the plain BranchType column ──────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Branch') AND name = N'BranchType')
BEGIN
    -- Drop any extended property on it first (won't error if absent)
    IF EXISTS (
        SELECT 1 FROM sys.fn_listextendedproperty(
            'MS_Description', 'SCHEMA', 'dbo', 'TABLE', 'Branch', 'COLUMN', 'BranchType')
    )
        EXEC sys.sp_dropextendedproperty
            @name       = N'MS_Description',
            @level0type = N'SCHEMA', @level0name = N'dbo',
            @level1type = N'TABLE',  @level1name = N'Branch',
            @level2type = N'COLUMN', @level2name = N'BranchType';

    ALTER TABLE dbo.Branch DROP COLUMN BranchType;
    PRINT 'Dropped plain BranchType column.';
END

-- ── Step 3: Re-add BranchType as a persisted computed column ──
ALTER TABLE dbo.Branch
    ADD BranchType AS (
        CAST(
            CASE
                WHEN IsDistributor = 1 THEN N'Distributor'
                WHEN IsDepot       = 1 THEN N'Depot'
                WHEN IsCenter      = 1 THEN N'Center'
                WHEN IsFactory     = 1 THEN N'Factory'
                ELSE                        N'Office'
            END
        AS NVARCHAR(50))
    ) PERSISTED;

PRINT 'BranchType re-added as a PERSISTED computed column.';

-- ── Step 4: Recreate the index on the computed column ─────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch') AND name = N'IX_Branch_BranchType')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Branch_BranchType]
        ON [dbo].[Branch] ([BranchType] ASC)
        INCLUDE ([Name], [Active]);

    PRINT 'Recreated IX_Branch_BranchType.';
END

-- ── Verification ──────────────────────────────────────────────
PRINT '';
PRINT '--- BranchType distribution (should have no NULLs) ---';
SELECT
    ISNULL(BranchType, '(NULL)') AS BranchType,
    COUNT(*)                      AS BranchCount
FROM   dbo.Branch
WHERE  Active = 1
GROUP  BY BranchType
ORDER  BY BranchCount DESC;
GO
