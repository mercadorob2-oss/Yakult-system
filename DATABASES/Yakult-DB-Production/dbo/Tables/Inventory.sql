CREATE TABLE [dbo].[Inventory] (
    [InvId]       INT            IDENTITY (1, 1) NOT NULL,
    [Description] NVARCHAR (400) NULL,
    [EntryType]   NVARCHAR (20)  NOT NULL,
    [Quantity]    INT            NOT NULL,
    [DatePosted]  DATETIME2 (2)  DEFAULT (sysutcdatetime()) NOT NULL,
    [PostedBy]    INT            NOT NULL,
    [ReqId]       INT            NULL,
    [RowVer]      ROWVERSION     NOT NULL,
    [ItemId]      INT            NULL,
    [SetId]       INT            NULL,
    [Active]      BIT            CONSTRAINT [DF_Inventory_Active] DEFAULT ((1)) NOT NULL,
    [ConditionID] INT            NULL,
    PRIMARY KEY CLUSTERED ([InvId] ASC),
    CONSTRAINT [CK_Inventory_EntryType_Valid] CHECK ([EntryType]='Fixed Assets' OR [EntryType]='Negative' OR [EntryType]='Positive'),
    CONSTRAINT [CK_Inventory_ItemOrRequest] CHECK ([ItemId] IS NOT NULL OR [ReqId] IS NOT NULL),
    CONSTRAINT [FK_Inventory_Condition] FOREIGN KEY ([ConditionID]) REFERENCES [dbo].[Condition] ([ConditionID]),
    CONSTRAINT [FK_Inventory_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_Inventory_PostedBy] FOREIGN KEY ([PostedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Inventory_Request] FOREIGN KEY ([ReqId]) REFERENCES [dbo].[Request] ([ReqId]),
    CONSTRAINT [FK_Inventory_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Inventory_PostedBy]
    ON [dbo].[Inventory]([PostedBy] ASC);

