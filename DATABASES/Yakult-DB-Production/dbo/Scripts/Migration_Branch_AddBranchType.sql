-- ============================================================
-- SCHEMA CHANGES — Part 2: Branch Classification
-- Adds BranchType NVARCHAR(50) derived from existing flags.
-- Existing flag columns (IsDistributor, IsDepot, IsCenter,
-- IsFactory) are KEPT — no breaking changes.
-- ============================================================

-- ── Step 1: Add BranchType column ────────────────────────────
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.Branch')
      AND  name      = N'BranchType'
)
BEGIN
    ALTER TABLE [dbo].[Branch]
        ADD [BranchType] NVARCHAR(50) NULL;

    PRINT 'Column BranchType added to dbo.Branch.';
END
ELSE
BEGIN
    PRINT 'Column BranchType already exists on dbo.Branch — skipped.';
END
GO

-- ── Step 2: Populate BranchType from existing flags ──────────
--   Priority order (a branch may have multiple flags set):
--   IsDistributor > IsDepot > IsCenter > IsFactory > Office
--   Only updates rows where BranchType is still NULL so the
--   statement is safe to re-run after manual corrections.
UPDATE [dbo].[Branch]
SET    [BranchType] = CASE
                          WHEN [IsDistributor] = 1 THEN N'Distributor'
                          WHEN [IsDepot]       = 1 THEN N'Depot'
                          WHEN [IsCenter]      = 1 THEN N'Center'
                          WHEN [IsFactory]     = 1 THEN N'Factory'
                          ELSE                           N'Office'
                      END
WHERE  [BranchType] IS NULL;

PRINT CAST(@@ROWCOUNT AS VARCHAR) + ' Branch row(s) populated with BranchType.';
GO

-- ── Step 3: Index to support type-based filtering ────────────
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.Branch')
      AND  name      = N'IX_Branch_BranchType'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Branch_BranchType]
        ON [dbo].[Branch] ([BranchType] ASC)
        INCLUDE ([Name], [ComId], [DeptId], [Active]);

    PRINT 'Index IX_Branch_BranchType created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Branch_BranchType already exists — skipped.';
END
GO

-- ── Verification ─────────────────────────────────────────────
SELECT [BranchType],
       COUNT(*) AS BranchCount
FROM   [dbo].[Branch]
GROUP  BY [BranchType]
ORDER  BY BranchCount DESC;
GO
