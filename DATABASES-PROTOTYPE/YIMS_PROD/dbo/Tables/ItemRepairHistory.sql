CREATE TABLE [dbo].[ItemRepairHistory] (
    [RepairId]          INT            IDENTITY (1, 1) NOT NULL,
    [UpdateId]          INT            NOT NULL,
    [ItemId]            INT            NULL,
    [SerialNumber]      NVARCHAR (100) NULL,
    [SetId]             INT            NULL,
    [SetCode]           NVARCHAR (50)  NULL,
    [PreviousStatus]    NVARCHAR (100) NULL,
    [NewStatus]         NVARCHAR (100) NULL,
    [ConditionId]       INT            NOT NULL,
    [ConditionName]     NVARCHAR (100) NOT NULL,
    [RepairAction]      NVARCHAR (100) NOT NULL,
    [Remark]            NVARCHAR (500) NULL,
    [CreatedAt]         DATETIME2 (0)  CONSTRAINT [DF_ItemRepairHistory_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [ProcessedByUserId] INT            NULL,
    [ProcessedByName]   NVARCHAR (200) NULL,
    CONSTRAINT [PK_ItemRepairHistory] PRIMARY KEY CLUSTERED ([RepairId] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemRepairHistory_Item]
    ON [dbo].[ItemRepairHistory]([ItemId] ASC, [SerialNumber] ASC, [CreatedAt] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_ItemRepairHistory_Update]
    ON [dbo].[ItemRepairHistory]([UpdateId] ASC);

