-- Migration: Make dbo.Request.DateRequested nullable
-- Purpose : DateRequested is now an optional, user-entered value (Add Request to Set / Set grid
--           display it as a separate field from DateCreated, which remains the row's audit
--           timestamp). Existing rows and the DEFAULT (sysutcdatetime()) constraint are
--           unaffected -- only the NOT NULL constraint is relaxed so new rows may omit it.

IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Request')
      AND name = N'DateRequested'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.Request
        ALTER COLUMN [DateRequested] DATETIME2 (2) NULL;

    PRINT 'Column DateRequested on dbo.Request altered to allow NULL.';
END
ELSE
BEGIN
    PRINT 'Column DateRequested on dbo.Request is already nullable. Skipping.';
END
GO
