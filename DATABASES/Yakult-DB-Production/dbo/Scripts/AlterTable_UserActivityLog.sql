-- ============================================================
-- ALTER: dbo.UserActivityLog
-- Purpose:
--   1. Fix CreatedDate default from sysdatetime() (local) to
--      SYSUTCDATETIME() (UTC) for consistent cross-timezone logging.
--   2. Add non-clustered indexes to support the admin filter
--      page range scans on UserId, CreatedDate, and EntityType.
--
-- Safety: ALTER only — no data is dropped or modified.
-- Run   : Once against the target database.
-- ============================================================

-- ─────────────────────────────────────────────────────────────
-- Step 1: Fix CreatedDate default to SYSUTCDATETIME()
-- ─────────────────────────────────────────────────────────────

-- Find the auto-generated default constraint name and drop it.
DECLARE @df NVARCHAR(256);

SELECT @df = dc.name
FROM   sys.default_constraints dc
JOIN   sys.columns             c
       ON  dc.parent_object_id = c.object_id
       AND dc.parent_column_id  = c.column_id
WHERE  dc.parent_object_id = OBJECT_ID('dbo.UserActivityLog')
  AND  c.name               = 'CreatedDate';

IF @df IS NOT NULL
    EXEC (N'ALTER TABLE dbo.UserActivityLog DROP CONSTRAINT [' + @df + N']');

-- Add named constraint with UTC function.
ALTER TABLE dbo.UserActivityLog
    ADD CONSTRAINT DF_UserActivityLog_CreatedDate
    DEFAULT (SYSUTCDATETIME()) FOR CreatedDate;

PRINT 'Step 1 complete: CreatedDate default updated to SYSUTCDATETIME()';

-- ─────────────────────────────────────────────────────────────
-- Step 2: Non-clustered indexes for the admin filter page
-- ─────────────────────────────────────────────────────────────

-- Index A: Filter by user, sort newest-first.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID('dbo.UserActivityLog')
      AND  name      = 'IX_UserActivityLog_UserId_CreatedDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UserActivityLog_UserId_CreatedDate
        ON dbo.UserActivityLog (UserId ASC, CreatedDate DESC)
        INCLUDE (ActionType, EntityType, EntityId, Description);

    PRINT 'Index IX_UserActivityLog_UserId_CreatedDate created.';
END
ELSE
    PRINT 'Index IX_UserActivityLog_UserId_CreatedDate already exists — skipped.';

-- Index B: Date-range scan across all users, newest-first.
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID('dbo.UserActivityLog')
      AND  name      = 'IX_UserActivityLog_CreatedDate')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UserActivityLog_CreatedDate
        ON dbo.UserActivityLog (CreatedDate DESC)
        INCLUDE (UserId, ActionType, EntityType, EntityId);

    PRINT 'Index IX_UserActivityLog_CreatedDate created.';
END
ELSE
    PRINT 'Index IX_UserActivityLog_CreatedDate already exists — skipped.';

-- Index C: Filter by EntityType / ActionType (e.g. "all Renewals", "all Updates").
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE  object_id = OBJECT_ID('dbo.UserActivityLog')
      AND  name      = 'IX_UserActivityLog_EntityType_ActionType')
BEGIN
    CREATE NONCLUSTERED INDEX IX_UserActivityLog_EntityType_ActionType
        ON dbo.UserActivityLog (EntityType ASC, ActionType ASC)
        INCLUDE (UserId, CreatedDate, EntityId);

    PRINT 'Index IX_UserActivityLog_EntityType_ActionType created.';
END
ELSE
    PRINT 'Index IX_UserActivityLog_EntityType_ActionType already exists — skipped.';

PRINT 'AlterTable_UserActivityLog.sql complete.';
