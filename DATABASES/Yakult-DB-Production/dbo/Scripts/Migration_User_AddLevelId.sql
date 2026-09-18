-- =============================================================================
-- Migration: Add LevelId (FK → dbo.AccountLevel) to dbo.[User]
-- Date: 2026-04-10
-- Description:
--   Adds a nullable LevelId column to dbo.[User] that references the new
--   dbo.AccountLevel hierarchy table.  NULL means "not yet assigned".
--   Existing rows are intentionally left NULL so that the current approval
--   flow continues to work until levels are assigned via the admin UI.
-- =============================================================================

-- ── 1. Add column (idempotent) ────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('dbo.[User]') AND name = 'LevelId'
)
BEGIN
    ALTER TABLE dbo.[User]
        ADD [LevelId] INT NULL;

    PRINT 'Column dbo.[User].LevelId added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.[User].LevelId already exists — skipping ALTER.';
END
GO

-- ── 2. Add FK constraint (idempotent) ─────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE  name = 'FK_User_AccountLevel'
)
BEGIN
    ALTER TABLE dbo.[User]
        ADD CONSTRAINT FK_User_AccountLevel
            FOREIGN KEY ([LevelId]) REFERENCES dbo.AccountLevel ([LevelId]);

    PRINT 'FK_User_AccountLevel constraint added.';
END
ELSE
BEGIN
    PRINT 'FK_User_AccountLevel already exists — skipping.';
END
GO
