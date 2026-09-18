-- ==========================================================================
-- SCRIPT: Migration_NormalizeHierarchy_Phase2_DropDeprecatedColumns.sql
-- PURPOSE: Drop the 4 deprecated legacy FK columns from Branch and Department
--          after all C# code has been migrated to use dbo.BranchDepartmentCompany.
--
-- PREREQUISITES:
--   1. Migration_BranchDepartmentCompany_CreateAndPopulate.sql
--   2. Migration_BranchDepartmentCompany_NullableDept.sql
--   3. Migration_FactoryNormalization_CompanyBranchHierarchy.sql
--   4. Migration_NormalizeHierarchy_DeprecateLegacyColumns.sql
--   5. All C# application code migrated away from Branch.ComId, Branch.DeptId,
--      Department.ComId, Department.BrId
--
-- WHAT THIS DROPS:
--   Branch.ComId   — FK_Branch_Company,  IX_Branch_ComId
--   Branch.DeptId  — FK_Branch_Department, IX_Branch_DeptId
--   Department.ComId — FK_Department_Company, IX_Department_ComId
--   Department.BrId  — FK_Department_Branch, IX_Department_BrId
-- ==========================================================================

-- ── Drop FK constraints (looked up by column, not by hardcoded name) ────────
DECLARE @sql NVARCHAR(500);

-- Branch.ComId FK
SELECT @sql = 'ALTER TABLE dbo.Branch DROP CONSTRAINT ' + fk.name
FROM   sys.foreign_keys        fk
JOIN   sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN   sys.columns             c   ON c.object_id = fkc.parent_object_id
                                   AND c.column_id = fkc.parent_column_id
WHERE  fk.parent_object_id = OBJECT_ID(N'dbo.Branch')
  AND  c.name = N'ComId';
IF @sql IS NOT NULL BEGIN EXEC(@sql); SET @sql = NULL; END

-- Branch.DeptId FK
SELECT @sql = 'ALTER TABLE dbo.Branch DROP CONSTRAINT ' + fk.name
FROM   sys.foreign_keys        fk
JOIN   sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN   sys.columns             c   ON c.object_id = fkc.parent_object_id
                                   AND c.column_id = fkc.parent_column_id
WHERE  fk.parent_object_id = OBJECT_ID(N'dbo.Branch')
  AND  c.name = N'DeptId';
IF @sql IS NOT NULL BEGIN EXEC(@sql); SET @sql = NULL; END

-- Department.ComId FK
SELECT @sql = 'ALTER TABLE dbo.Department DROP CONSTRAINT ' + fk.name
FROM   sys.foreign_keys        fk
JOIN   sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN   sys.columns             c   ON c.object_id = fkc.parent_object_id
                                   AND c.column_id = fkc.parent_column_id
WHERE  fk.parent_object_id = OBJECT_ID(N'dbo.Department')
  AND  c.name = N'ComId';
IF @sql IS NOT NULL BEGIN EXEC(@sql); SET @sql = NULL; END

-- Department.BrId FK
SELECT @sql = 'ALTER TABLE dbo.Department DROP CONSTRAINT ' + fk.name
FROM   sys.foreign_keys        fk
JOIN   sys.foreign_key_columns fkc ON fkc.constraint_object_id = fk.object_id
JOIN   sys.columns             c   ON c.object_id = fkc.parent_object_id
                                   AND c.column_id = fkc.parent_column_id
WHERE  fk.parent_object_id = OBJECT_ID(N'dbo.Department')
  AND  c.name = N'BrId';
IF @sql IS NOT NULL BEGIN EXEC(@sql); SET @sql = NULL; END

-- ── Drop indexes on deprecated columns (if they exist) ───────────────────────
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch')     AND name = N'IX_Branch_ComId')
    DROP INDEX IX_Branch_ComId      ON dbo.Branch;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch')     AND name = N'IX_Branch_DeptId')
    DROP INDEX IX_Branch_DeptId     ON dbo.Branch;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'IX_Department_ComId')
    DROP INDEX IX_Department_ComId  ON dbo.Department;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'IX_Department_BrId')
    DROP INDEX IX_Department_BrId   ON dbo.Department;

-- IX_Branch_BranchType includes [ComId] and [DeptId] as covered columns.
-- It must be dropped before those columns are removed, then recreated
-- without the deprecated includes.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch')     AND name = N'IX_Branch_BranchType')
    DROP INDEX IX_Branch_BranchType ON dbo.Branch;

-- ── Drop the columns themselves ───────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Branch')     AND name = N'ComId')
    ALTER TABLE dbo.Branch     DROP COLUMN ComId;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Branch')     AND name = N'DeptId')
    ALTER TABLE dbo.Branch     DROP COLUMN DeptId;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'ComId')
    ALTER TABLE dbo.Department DROP COLUMN ComId;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Department') AND name = N'BrId')
    ALTER TABLE dbo.Department DROP COLUMN BrId;

-- ── Recreate IX_Branch_BranchType without the dropped covered columns ─────────
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Branch') AND name = N'IX_Branch_BranchType')
    CREATE NONCLUSTERED INDEX [IX_Branch_BranchType]
        ON [dbo].[Branch] ([BranchType] ASC)
        INCLUDE ([Name], [Active]);
