CREATE TABLE [dbo].[Cartridge] (
    [ItemId]          INT           NOT NULL,
    [CartridgeTypeId] INT           NOT NULL,
    [IsActive]        BIT           DEFAULT ((1)) NOT NULL,
    [BatchId]         INT           NULL,
    [RefillStatus]    NVARCHAR (20) NULL,
    [StockType]       NVARCHAR (20) DEFAULT ('Brand New') NOT NULL,
    CONSTRAINT [PK_Cartridge] PRIMARY KEY CLUSTERED ([ItemId] ASC),
    CONSTRAINT [CK_Cartridge_RefillStatus] CHECK ([RefillStatus]='Ready' OR [RefillStatus]='Refilled' OR [RefillStatus]='Refilling' OR [RefillStatus]='For Refill' OR [RefillStatus] IS NULL),
    CONSTRAINT [CK_Cartridge_StockType] CHECK ([StockType]='Refilled' OR [StockType]='Brand New'),
    CONSTRAINT [FK_Cartridge_CartridgeType] FOREIGN KEY ([CartridgeTypeId]) REFERENCES [dbo].[CartridgeType] ([CartridgeTypeId]),
    CONSTRAINT [FK_Cartridge_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_Cartridge_VendorCartridgeBatch] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Cartridge_ItemId]
    ON [dbo].[Cartridge]([ItemId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Cartridge_CartridgeTypeId]
    ON [dbo].[Cartridge]([CartridgeTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Cartridge_BatchId]
    ON [dbo].[Cartridge]([BatchId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Cartridge_RefillStatus]
    ON [dbo].[Cartridge]([RefillStatus] ASC) WHERE ([RefillStatus] IS NOT NULL);

