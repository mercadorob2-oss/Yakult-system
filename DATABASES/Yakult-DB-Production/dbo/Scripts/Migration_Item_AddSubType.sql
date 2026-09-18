-- Nullable default Sub-Type classification (Contract/Subscription/License/Services) on
-- the master Item catalog — set once via BatchAddItemDialog, distinct from the per-invoice
-- Sub-Type Entries (dbo.Contract/Subscription/License/ServiceDetail) which carry their own
-- reference code and date range. This is a default/reporting hint only, optional just like
-- dbo.[Set].InvoiceSubType.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'SubType'
)
BEGIN
    ALTER TABLE dbo.Item ADD SubType NVARCHAR (20) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Item_SubType'
)
BEGIN
    ALTER TABLE dbo.Item
        ADD CONSTRAINT CK_Item_SubType CHECK (
            SubType IN ('Contract', 'Subscription', 'License', 'Services')
            OR SubType IS NULL
        );
END
GO
