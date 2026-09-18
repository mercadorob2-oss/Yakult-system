CREATE TABLE [dbo].[VendorItemCategory] (
    [VendorItemCategoryId] INT           IDENTITY (1, 1) NOT NULL,
    [VendorId]             INT           NOT NULL,
    [CategoryId]           INT           NULL,
    [DateCreated]          DATETIME      DEFAULT (getdate()) NOT NULL,
    [CreatedBy]            INT           NOT NULL,
    [ItemType]             NVARCHAR (50) NULL,
    PRIMARY KEY CLUSTERED ([VendorItemCategoryId] ASC),
    CONSTRAINT [CK_VendorItemCategory_EitherCategoryOrType] CHECK ([CategoryId] IS NOT NULL OR [ItemType] IS NOT NULL),
    CONSTRAINT [CK_VendorItemCategory_ItemType] CHECK ([ItemType]='Service' OR [ItemType]='Software/License' OR [ItemType]='Consumable/Hardware'),
    CONSTRAINT [FK_VendorItemCategory_Category] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[ItemCategory] ([CategoryId]),
    CONSTRAINT [FK_VendorItemCategory_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [UQ_VendorItemCategory_Combined] UNIQUE NONCLUSTERED ([VendorId] ASC, [CategoryId] ASC, [ItemType] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_VendorItemCategory_VendorId]
    ON [dbo].[VendorItemCategory]([VendorId] ASC)
    INCLUDE([CategoryId], [ItemType]);


GO
CREATE NONCLUSTERED INDEX [IX_VendorItemCategory_CategoryId]
    ON [dbo].[VendorItemCategory]([CategoryId] ASC)
    INCLUDE([VendorId]);


GO
CREATE NONCLUSTERED INDEX [IX_VendorItemCategory_ItemType]
    ON [dbo].[VendorItemCategory]([ItemType] ASC)
    INCLUDE([VendorId]);

