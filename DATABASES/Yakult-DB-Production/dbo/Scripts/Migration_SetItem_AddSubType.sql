-- Each item recorded on an invoice can optionally be tagged with a Sub-Type
-- (Contract/Subscription/License/Services) directly — no separate "entry" row, no shared
-- reference code/date range. The item stays the same SetItem row it always was; this is
-- just one more nullable attribute on it. All items with the same Sub-Type on the same
-- invoice are, by definition, already grouped — they share the same Set (one Document
-- Number = one invoice).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'SubType'
)
BEGIN
    ALTER TABLE dbo.SetItem ADD SubType NVARCHAR (20) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_SetItem_SubType'
)
BEGIN
    ALTER TABLE dbo.SetItem
        ADD CONSTRAINT CK_SetItem_SubType CHECK (
            SubType IN ('Contract', 'Subscription', 'License', 'Services')
            OR SubType IS NULL
        );
END
GO
