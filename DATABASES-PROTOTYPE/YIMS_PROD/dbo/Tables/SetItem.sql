CREATE TABLE [dbo].[SetItem] (
    [SetItemId]     INT             IDENTITY (1, 1) NOT NULL,
    [SetId]         INT             NOT NULL,
    [ItemId]        INT             NOT NULL,
    [ItemCode]      NVARCHAR (100)  NULL,
    [Description]   NVARCHAR (4000) NULL,
    [Quantity]      DECIMAL (18, 2) DEFAULT ((0)) NOT NULL,
    [UnitOfMeasure] NVARCHAR (50)   NULL,
    [UnitPrice]     DECIMAL (18, 2) DEFAULT ((0)) NOT NULL,
    [Amount]        DECIMAL (18, 2) DEFAULT ((0)) NOT NULL,
    [LineStartDate] DATETIME2 (7)   CONSTRAINT [DF_SetItem_LineStartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [LineEndDate]   DATETIME2 (7)   CONSTRAINT [DF_SetItem_LineEndDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [CreatedBy]     INT             NULL,
    [CreatedAt]     DATETIME2 (7)   DEFAULT (sysutcdatetime()) NOT NULL,
    [RenewalStatus]      NVARCHAR (20)   NULL,
    [RenewalReferenceId] INT             NULL,
    [RenewalSubcategory] NVARCHAR (50)   NULL,
    [RenewalIdentifier]  NVARCHAR (100)  NULL,
    PRIMARY KEY CLUSTERED ([SetItemId] ASC),
    CONSTRAINT [FK_SetItem_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_SetItem_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId])
);


GO
CREATE NONCLUSTERED INDEX [IX_SetItem_SetId]
    ON [dbo].[SetItem]([SetId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_SetItem_ItemId]
    ON [dbo].[SetItem]([ItemId] ASC)
    INCLUDE([SetId]);

