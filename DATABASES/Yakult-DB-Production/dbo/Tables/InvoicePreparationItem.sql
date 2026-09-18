CREATE TABLE [dbo].[InvoicePreparationItem] (
    [PreparationItemId] INT             IDENTITY (1, 1) NOT NULL,
    [PreparationId]     INT             NOT NULL,
    [ItemId]            INT             NOT NULL,
    [Quantity]          DECIMAL (18, 2) CONSTRAINT [DF_InvoicePreparationItem_Quantity] DEFAULT ((0)) NOT NULL,
    [UnitPrice]         DECIMAL (18, 2) CONSTRAINT [DF_InvoicePreparationItem_UnitPrice] DEFAULT ((0)) NOT NULL,
    [Remarks]           NVARCHAR (400)  NULL,
    [CreatedBy]         INT             NOT NULL,
    [CreatedAt]         DATETIME2 (7)   CONSTRAINT [DF_InvoicePreparationItem_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    PRIMARY KEY CLUSTERED ([PreparationItemId] ASC),
    CONSTRAINT [FK_InvoicePreparationItem_Preparation] FOREIGN KEY ([PreparationId]) REFERENCES [dbo].[InvoicePreparation] ([PreparationId]) ON DELETE CASCADE,
    CONSTRAINT [FK_InvoicePreparationItem_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId])
);


GO
CREATE NONCLUSTERED INDEX [IX_InvoicePreparationItem_PreparationId]
    ON [dbo].[InvoicePreparationItem]([PreparationId] ASC);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_InvoicePreparationItem_PreparationId_ItemId]
    ON [dbo].[InvoicePreparationItem]([PreparationId] ASC, [ItemId] ASC);
