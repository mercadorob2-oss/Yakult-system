-- Migration: Add DateRequested to dbo.Set (auto-derived from dbo.Request)
-- Purpose : Persists the earliest DateRequested among the Requests attached to a Set directly on
--           dbo.Set, instead of recomputing MIN(Request.DateRequested) on every Sets-grid query.
--           The column is NOT independently editable -- it is kept in sync by a trigger on
--           dbo.Request so it stays correct no matter which code path links/unlinks/edits a
--           Request's SetId or DateRequested (WinForms dialogs, Portal service, batch import, etc).
-- Note    : NULL when the Set has no attached Requests yet.

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Set')
      AND name = N'DateRequested'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD [DateRequested] DATETIME2 (2) NULL;

    PRINT 'Column DateRequested added to dbo.Set.';
END
ELSE
BEGIN
    PRINT 'Column DateRequested already exists on dbo.Set. Skipping.';
END
GO

-- Backfill existing Sets from their currently-attached Requests.
UPDATE s
SET s.DateRequested = (
    SELECT MIN(r.DateRequested)
    FROM dbo.Request r
    WHERE r.SetId = s.SetId
)
FROM dbo.[Set] s;
GO

-- Keep dbo.Set.DateRequested in sync whenever a Request is linked/unlinked/edited.
CREATE OR ALTER TRIGGER dbo.trg_Request_SyncSetDateRequested
ON dbo.Request
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH AffectedSets AS (
        SELECT SetId FROM inserted WHERE SetId IS NOT NULL
        UNION
        SELECT SetId FROM deleted  WHERE SetId IS NOT NULL
    )
    UPDATE s
    SET s.DateRequested = (
        SELECT MIN(r.DateRequested)
        FROM dbo.Request r
        WHERE r.SetId = s.SetId
    )
    FROM dbo.[Set] s
    INNER JOIN AffectedSets a ON a.SetId = s.SetId;
END;
GO
