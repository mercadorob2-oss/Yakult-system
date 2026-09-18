-- ============================================================
-- SCHEMA CHANGE: Create TVP type dbo.IntIdList
--
-- Used by bulk-update stored procedures and C# SqlCommand calls
-- that need to pass a list of integer IDs without string concat.
--
-- Required before running BulkAssignDepartmentAsync in C#.
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM   sys.types
    WHERE  name          = N'IntIdList'
      AND  is_table_type = 1
)
BEGIN
    CREATE TYPE [dbo].[IntIdList] AS TABLE (
        [Id] INT NOT NULL
    );
    PRINT 'Type dbo.IntIdList created.';
END
ELSE
BEGIN
    PRINT 'Type dbo.IntIdList already exists — skipped.';
END
GO
