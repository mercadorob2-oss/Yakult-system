-- ============================================================
-- SCHEMA CHANGES — Part 1: Department Hierarchy
-- Adds ParentDeptId (self-referencing FK) to dbo.Department
-- Safe: NULL allowed, no existing columns removed, idempotent
-- ============================================================

-- ── Step 1: Add ParentDeptId column ──────────────────────────
IF NOT EXISTS (
    SELECT 1
    FROM   sys.columns
    WHERE  object_id = OBJECT_ID(N'dbo.Department')
      AND  name      = N'ParentDeptId'
)
BEGIN
    ALTER TABLE [dbo].[Department]
        ADD [ParentDeptId] INT NULL;

    PRINT 'Column ParentDeptId added to dbo.Department.';
END
ELSE
BEGIN
    PRINT 'Column ParentDeptId already exists on dbo.Department — skipped.';
END
GO

-- ── Step 2: Add self-referencing FK ──────────────────────────
--   Deferred constraint (no FK cycles with Branch because
--   Branch → Department is a separate constraint chain).
IF NOT EXISTS (
    SELECT 1
    FROM   sys.foreign_keys
    WHERE  name          = N'FK_Department_ParentDept'
      AND  parent_object_id = OBJECT_ID(N'dbo.Department')
)
BEGIN
    ALTER TABLE [dbo].[Department]
        ADD CONSTRAINT [FK_Department_ParentDept]
            FOREIGN KEY ([ParentDeptId])
            REFERENCES  [dbo].[Department] ([DeptId]);

    PRINT 'FK FK_Department_ParentDept created.';
END
ELSE
BEGIN
    PRINT 'FK FK_Department_ParentDept already exists — skipped.';
END
GO

-- ── Step 3: Supporting index for hierarchy traversal ─────────
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'dbo.Department')
      AND  name      = N'IX_Department_ParentDeptId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Department_ParentDeptId]
        ON [dbo].[Department] ([ParentDeptId] ASC)
        INCLUDE ([Name], [Active], [ComId]);

    PRINT 'Index IX_Department_ParentDeptId created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Department_ParentDeptId already exists — skipped.';
END
GO
