CREATE TABLE [dbo].[ItemInspectionLog] (
    [InspectionId]   INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]         INT            NOT NULL,
    [ConditionId]    INT            NULL,
    [InspectedAt]    DATETIME2 (2)  CONSTRAINT [DF_ItemInspectionLog_InspectedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [InspectedBy]    INT            NOT NULL,
    [Recommendation] NVARCHAR (20)  NULL,
    [Notes]          NVARCHAR (500) NULL,
    CONSTRAINT [PK_ItemInspectionLog] PRIMARY KEY CLUSTERED ([InspectionId] ASC),
    CONSTRAINT [CK_ItemInspectionLog_Recommendation] CHECK ([Recommendation]='RETAIN' OR [Recommendation]='REPAIR' OR [Recommendation]='DISPOSE' OR [Recommendation]='SELL' OR [Recommendation] IS NULL),
    CONSTRAINT [FK_ItemInspectionLog_Condition] FOREIGN KEY ([ConditionId]) REFERENCES [dbo].[Condition] ([ConditionID]),
    CONSTRAINT [FK_ItemInspectionLog_InspectedBy] FOREIGN KEY ([InspectedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_ItemInspectionLog_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId])
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemInspectionLog_InspectedAt]
    ON [dbo].[ItemInspectionLog]([InspectedAt] DESC)
    INCLUDE([ItemId], [InspectedBy], [Recommendation]);


GO
CREATE NONCLUSTERED INDEX [IX_ItemInspectionLog_ItemId]
    ON [dbo].[ItemInspectionLog]([ItemId] ASC, [InspectedAt] DESC)
    INCLUDE([ConditionId], [Recommendation], [InspectedBy]);

