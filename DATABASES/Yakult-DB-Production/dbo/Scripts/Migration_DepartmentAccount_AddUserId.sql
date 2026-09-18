-- ============================================================
-- Migration: Add UserId FK to dbo.DepartmentAccount
-- Purpose:   Each DepartmentAccount is backed by a dbo.[User]
--            row so it can be used as CreatedBy in transactions.
--            New accounts create the User row first; existing
--            accounts without a UserId still need manual backfill
--            or re-creation via the admin page.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.DepartmentAccount')
      AND name = 'UserId'
)
BEGIN
    ALTER TABLE dbo.DepartmentAccount
        ADD [UserId] INT NULL;

    ALTER TABLE dbo.DepartmentAccount
        ADD CONSTRAINT [FK_DepartmentAccount_User]
        FOREIGN KEY ([UserId]) REFERENCES dbo.[User] ([UserId]);

    PRINT 'Column UserId added to dbo.DepartmentAccount.';
END
ELSE
BEGIN
    PRINT 'Column UserId already exists. Skipping.';
END
GO
