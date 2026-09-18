-- Migration: Create VendorCartridgeBatchLine for many-to-many Batch <-> CartridgeModel
-- Replaces the single VendorCartridgeBatch.CartridgeModelId with per-line detail rows.
-- Run AFTER Migration_Vendor_AddIsRefiller.sql
--
-- Step 1: Create the new line table
IF OBJECT_ID('dbo.VendorCartridgeBatchLine', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[VendorCartridgeBatchLine] (
        [BatchLineId]      INT      IDENTITY(1,1)  NOT NULL,
        [BatchId]          INT                     NOT NULL,
        [CartridgeModelId] INT                     NOT NULL,
        [SentQty]          INT      DEFAULT((0))   NOT NULL,
        [ReturnedQty]      INT      DEFAULT((0))   NOT NULL,
        [CreatedDate]      DATETIME DEFAULT(GETDATE()) NOT NULL,
        CONSTRAINT [PK_VendorCartridgeBatchLine]
            PRIMARY KEY CLUSTERED ([BatchLineId] ASC),
        CONSTRAINT [UQ_VendorCartridgeBatchLine_BatchModel]
            UNIQUE ([BatchId] ASC, [CartridgeModelId] ASC),
        CONSTRAINT [FK_VendorCartridgeBatchLine_Batch]
            FOREIGN KEY ([BatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId]),
        CONSTRAINT [FK_VendorCartridgeBatchLine_CartridgeModel]
            FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId])
    );

    CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatchLine_BatchId]
        ON [dbo].[VendorCartridgeBatchLine]([BatchId] ASC);

    CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatchLine_CartridgeModelId]
        ON [dbo].[VendorCartridgeBatchLine]([CartridgeModelId] ASC);
END
GO

-- Step 2: Backfill existing batches (one line per existing batch row)
INSERT INTO [dbo].[VendorCartridgeBatchLine] ([BatchId], [CartridgeModelId], [SentQty], [ReturnedQty])
SELECT
    vcb.[BatchId],
    vcb.[CartridgeModelId],
    vcb.[OriginalQty],
    vcb.[ReturnedQty]
FROM [dbo].[VendorCartridgeBatch] vcb
WHERE NOT EXISTS (
    SELECT 1
    FROM [dbo].[VendorCartridgeBatchLine] vcbl
    WHERE vcbl.[BatchId] = vcb.[BatchId]
      AND vcbl.[CartridgeModelId] = vcb.[CartridgeModelId]
);
GO
