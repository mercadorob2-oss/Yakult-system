CREATE TABLE [dbo].[VendorCartridgeBatchLine] (
    [BatchLineId]      INT IDENTITY (1, 1) NOT NULL,
    [BatchId]          INT NOT NULL,
    [CartridgeModelId] INT NOT NULL,
    [SentQty]          INT NOT NULL,
    [ReturnedQty]      INT DEFAULT ((0)) NOT NULL,
    CONSTRAINT [PK_VendorCartridgeBatchLine] PRIMARY KEY CLUSTERED ([BatchLineId] ASC),
    CONSTRAINT [CK_VendorCartridgeBatchLine_ReturnedQty] CHECK ([ReturnedQty]>=(0)),
    CONSTRAINT [CK_VendorCartridgeBatchLine_SentQty] CHECK ([SentQty]>(0)),
    CONSTRAINT [FK_VendorCartridgeBatchLine_Batch] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId]),
    CONSTRAINT [FK_VendorCartridgeBatchLine_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId]),
    CONSTRAINT [UQ_VendorCartridgeBatchLine_BatchModel] UNIQUE NONCLUSTERED ([BatchId] ASC, [CartridgeModelId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatchLine_CartridgeModelId]
    ON [dbo].[VendorCartridgeBatchLine]([CartridgeModelId] ASC)
    INCLUDE([BatchId], [SentQty], [ReturnedQty]);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatchLine_BatchId]
    ON [dbo].[VendorCartridgeBatchLine]([BatchId] ASC)
    INCLUDE([CartridgeModelId], [SentQty], [ReturnedQty]);

