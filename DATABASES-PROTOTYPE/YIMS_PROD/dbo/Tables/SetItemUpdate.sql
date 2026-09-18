CREATE TABLE [dbo].[SetItemUpdate] (
    [UpdateId]        INT            IDENTITY (1, 1) NOT NULL,
    [SetId]           INT            NULL,
    [SetCode]         NVARCHAR (50)  NOT NULL,
    [ItemId]          INT            NULL,
    [ItemType]        NVARCHAR (200) NULL,
    [SerialNumber]    NVARCHAR (100) NULL,
    [ModelNumber]     NVARCHAR (100) NULL,
    [PreviousStatus]  NVARCHAR (100) NULL,
    [NewStatus]       NVARCHAR (100) NULL,
    [Remark]          NVARCHAR (500) NULL,
    [UpdatedByUserId] NVARCHAR (100) NULL,
    [UpdatedByName]   NVARCHAR (200) NULL,
    [Source]          NVARCHAR (50)  CONSTRAINT [DF_SetItemUpdate_Source] DEFAULT ('Android') NOT NULL,
    [CreatedAt]       DATETIME2 (0)  CONSTRAINT [DF_SetItemUpdate_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [Processed]       BIT            CONSTRAINT [DF_SetItemUpdate_Processed] DEFAULT ((0)) NOT NULL,
    [ProcessedBy]     NVARCHAR (200) NULL,
    [ProcessedAt]     DATETIME2 (0)  CONSTRAINT [DF_SetItemUpdate_ProcessedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    CONSTRAINT [PK_SetItemUpdate] PRIMARY KEY CLUSTERED ([UpdateId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_SetItemUpdate_Processed]
    ON [dbo].[SetItemUpdate]([Processed] ASC, [CreatedAt] ASC)
    INCLUDE([SetId], [ItemId], [SerialNumber], [ModelNumber], [NewStatus]);


GO
CREATE NONCLUSTERED INDEX [IX_SetItemUpdate_SetId]
    ON [dbo].[SetItemUpdate]([SetId] ASC)
    INCLUDE([SetCode], [ItemId], [PreviousStatus], [NewStatus], [CreatedAt]);


GO
CREATE NONCLUSTERED INDEX [IX_SetItemUpdate_Item]
    ON [dbo].[SetItemUpdate]([ItemId] ASC)
    INCLUDE([SerialNumber], [ModelNumber], [NewStatus], [CreatedAt]);

