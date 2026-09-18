CREATE TABLE [dbo].[ReceiptSet] (
    [ReceiptSetId] INT             IDENTITY (1, 1) NOT NULL,
    [SetId]        INT             NULL,
    [RenewedFromReceiptSetId] INT  NULL,
    [Supplier]     NVARCHAR (200)  NULL,
    [SiNumber]     NVARCHAR (50)   NULL,
    [DrNumber]     NVARCHAR (50)   NULL,
    [PoNumber]     NVARCHAR (50)   NULL,
    [SiImagePath]  NVARCHAR (500)  NULL,
    [DrImagePath]  NVARCHAR (500)  NULL,
    [PoImagePath]  NVARCHAR (500)  NULL,
    [CreatedAt]    DATETIME        DEFAULT (getdate()) NOT NULL,
    [CreatedBy]    INT             NULL,
    [ModifiedAt]   DATETIME        CONSTRAINT [DF_ReceiptSet_ModifiedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]   INT             NULL,
    [SiImage]      VARBINARY (MAX) NULL,
    [DrImage]      VARBINARY (MAX) NULL,
    [PoImage]      VARBINARY (MAX) NULL,
    PRIMARY KEY CLUSTERED ([ReceiptSetId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ReceiptSet_SetId]
    ON [dbo].[ReceiptSet]([SetId] ASC)
    INCLUDE([Supplier], [SiNumber], [DrNumber], [PoNumber], [CreatedAt]);


GO
CREATE NONCLUSTERED INDEX [IX_ReceiptSet_DocumentNumbers]
    ON [dbo].[ReceiptSet]([SiNumber] ASC, [DrNumber] ASC, [PoNumber] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_ReceiptSet_CreatedAt]
    ON [dbo].[ReceiptSet]([CreatedAt] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_ReceiptSet_RenewedFromReceiptSetId]
    ON [dbo].[ReceiptSet]([RenewedFromReceiptSetId] ASC);
