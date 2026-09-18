/*
    Converts dbo.Item.WarrantyEndDate from a PERSISTED computed column
    (dateadd(year, WarrantyYears, WarrantyStartDate)) into a real, independently
    nullable DATETIME2 column, so Edit Item can set Warranty End to any date
    instead of it always being derived from Warranty Start + Warranty Years.

    No object is renamed: the computed column is dropped and a plain column is
    added back under the same name, then backfilled by recomputing the same
    formula the old computed column used (Start + Years) — not by copying
    through a temp column. Idempotent: once WarrantyEndDate is no longer a
    computed column, this script is a no-op on rerun.
*/
IF EXISTS (
    SELECT 1 FROM sys.computed_columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'WarrantyEndDate'
)
BEGIN
    IF EXISTS (
        SELECT 1 FROM sys.indexes
        WHERE name = 'IX_Item_WarrantyEndDate' AND object_id = OBJECT_ID('dbo.Item')
    )
        DROP INDEX [IX_Item_WarrantyEndDate] ON dbo.Item;

    -- Each step runs as its own dynamic-SQL batch: SQL Server binds an entire
    -- static batch (including the column names each statement references)
    -- before executing any of it, so a later statement in the same batch can't
    -- see a column an earlier statement in that same batch just dropped or added.
    EXEC ('ALTER TABLE dbo.Item DROP COLUMN WarrantyEndDate;');

    EXEC ('ALTER TABLE dbo.Item ADD WarrantyEndDate DATETIME2(7) NULL;');

    EXEC ('UPDATE dbo.Item
           SET WarrantyEndDate = DATEADD(YEAR, WarrantyYears, WarrantyStartDate)
           WHERE WarrantyStartDate IS NOT NULL;');

    EXEC ('CREATE NONCLUSTERED INDEX [IX_Item_WarrantyEndDate]
        ON dbo.Item([WarrantyEndDate] ASC) WHERE ([WarrantyStartDate] IS NOT NULL);');
END
