-- Migration: Add ConsumableModelId to dbo.Item
-- Purpose : Links a physical Item catalog row to its formal Ink/Toner/Print Head
--           ConsumableModel, mirroring how CartridgeModelId links a cartridge Item row
--           to its dbo.CartridgeModel. Nullable — only assigned for items in scope.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Item') AND name = N'ConsumableModelId'
)
BEGIN
    ALTER TABLE dbo.Item
        ADD [ConsumableModelId] INT NULL
        CONSTRAINT [FK_Item_ConsumableModel] FOREIGN KEY REFERENCES dbo.ConsumableModel(ConsumableModelId);

    PRINT 'Column ConsumableModelId added to dbo.Item.';
END
ELSE
BEGIN
    PRINT 'Column ConsumableModelId already exists on dbo.Item. Skipping.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Item_ConsumableModel_Available' AND object_id = OBJECT_ID(N'dbo.Item')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Item_ConsumableModel_Available]
        ON [dbo].[Item]([ConsumableModelId] ASC)
        INCLUDE([StockOnHand]) WHERE ([Active] = (1));

    PRINT 'Index IX_Item_ConsumableModel_Available created.';
END
ELSE
BEGIN
    PRINT 'Index IX_Item_ConsumableModel_Available already exists. Skipping.';
END
GO
