-- Migration: Add IsSuperAdmin column to dbo.[User]
-- Date: 2026-04-10
-- Purpose: Allows designating a SuperAdmin user directly on the User record,
--          so they can manage system roles and permissions via the UI without
--          requiring backend code changes and app reinstallation.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[User]')
      AND name = 'IsSuperAdmin'
)
BEGIN
    ALTER TABLE dbo.[User]
        ADD [IsSuperAdmin] BIT NOT NULL DEFAULT (0);

    PRINT 'Column IsSuperAdmin added to dbo.[User].';
END
ELSE
BEGIN
    PRINT 'Column IsSuperAdmin already exists on dbo.[User]. Skipped.';
END
