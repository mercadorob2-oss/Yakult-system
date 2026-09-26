-- ============================================================================
-- Migration: DepartmentAccount — add ComId / DeptId / BranchId
--
-- Department accounts were linked to their Company / Department / Branch by
-- NAME only, and every login re-resolved the IDs by name. Renaming a Company,
-- Department or Branch therefore silently cut the account off from its own
-- transactions (Request History matches on the IDs saved on dbo.Request).
--
-- These columns pin the account to stable IDs. Logins prefer them and fall back
-- to the name lookup when they are NULL (rows inserted by older code paths).
--
-- Idempotent: safe to run more than once. The backfill only fills NULLs and only
-- where the name matches exactly one row, so an ambiguous name is left for the
-- name fallback instead of being pinned to the wrong ID.
-- ============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[DepartmentAccount]') AND name = 'ComId'
)
BEGIN
    ALTER TABLE dbo.[DepartmentAccount] ADD [ComId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[DepartmentAccount]') AND name = 'DeptId'
)
BEGIN
    ALTER TABLE dbo.[DepartmentAccount] ADD [DeptId] INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[DepartmentAccount]') AND name = 'BranchId'
)
BEGIN
    ALTER TABLE dbo.[DepartmentAccount] ADD [BranchId] INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DepartmentAccount_Company')
    ALTER TABLE dbo.[DepartmentAccount]
        ADD CONSTRAINT [FK_DepartmentAccount_Company] FOREIGN KEY ([ComId]) REFERENCES dbo.[Company] ([ComId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DepartmentAccount_Department')
    ALTER TABLE dbo.[DepartmentAccount]
        ADD CONSTRAINT [FK_DepartmentAccount_Department] FOREIGN KEY ([DeptId]) REFERENCES dbo.[Department] ([DeptId]);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_DepartmentAccount_Branch')
    ALTER TABLE dbo.[DepartmentAccount]
        ADD CONSTRAINT [FK_DepartmentAccount_Branch] FOREIGN KEY ([BranchId]) REFERENCES dbo.[Branch] ([BranchId]);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[DepartmentAccount]') AND name = 'IX_DepartmentAccount_Scope'
)
    CREATE NONCLUSTERED INDEX [IX_DepartmentAccount_Scope]
        ON dbo.[DepartmentAccount] ([ComId], [BranchId], [DeptId]);
GO

-- ── Backfill from names (unambiguous matches only) ──────────────────────────
UPDATE da
SET    da.ComId = (SELECT MIN(c.ComId) FROM dbo.Company c WHERE c.Name = da.CompanyName)
FROM   dbo.[DepartmentAccount] da
WHERE  da.ComId IS NULL
  AND  (SELECT COUNT(*) FROM dbo.Company c WHERE c.Name = da.CompanyName) = 1;

UPDATE da
SET    da.DeptId = (SELECT MIN(d.DeptId) FROM dbo.Department d WHERE d.Name = da.DepartmentName)
FROM   dbo.[DepartmentAccount] da
WHERE  da.DeptId IS NULL
  AND  (SELECT COUNT(*) FROM dbo.Department d WHERE d.Name = da.DepartmentName) = 1;

UPDATE da
SET    da.BranchId = (SELECT MIN(b.BranchId) FROM dbo.Branch b WHERE b.Name = da.BranchName)
FROM   dbo.[DepartmentAccount] da
WHERE  da.BranchId IS NULL
  AND  (SELECT COUNT(*) FROM dbo.Branch b WHERE b.Name = da.BranchName) = 1;
GO

-- Rows still missing an ID (ambiguous or unmatched name), for review:
SELECT Id, CompanyName, DepartmentName, BranchName, Username, ComId, DeptId, BranchId
FROM   dbo.[DepartmentAccount]
WHERE  ComId IS NULL OR DeptId IS NULL OR BranchId IS NULL;
GO
