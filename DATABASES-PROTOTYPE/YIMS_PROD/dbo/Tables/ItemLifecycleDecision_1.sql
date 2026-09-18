CREATE TABLE [dbo].[ItemLifecycleDecision] (
    [DecisionId]     INT             IDENTITY (1, 1) NOT NULL,
    [ItemId]         INT             NOT NULL,
    [DecisionTypeId] INT             NOT NULL,
    [ConditionId]    INT             NULL,
    [Quantity]       INT             CONSTRAINT [DF_ItemLifecycleDecision_Quantity] DEFAULT ((1)) NOT NULL,
    [DecisionStatus] NVARCHAR (20)   CONSTRAINT [DF_ItemLifecycleDecision_Status] DEFAULT ('Pending') NOT NULL,
    [DecidedAt]      DATETIME2 (2)   CONSTRAINT [DF_ItemLifecycleDecision_DecidedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [DecidedBy]      INT             NOT NULL,
    [RecipientName]  NVARCHAR (200)  NULL,
    [SaleAmount]     DECIMAL (18, 2) NULL,
    [Remarks]        NVARCHAR (500)  NULL,
    CONSTRAINT [PK_ItemLifecycleDecision] PRIMARY KEY CLUSTERED ([DecisionId] ASC),
    CONSTRAINT [CK_ItemLifecycleDecision_Quantity] CHECK ([Quantity]>(0)),
    CONSTRAINT [CK_ItemLifecycleDecision_Status] CHECK ([DecisionStatus]='Cancelled' OR [DecisionStatus]='Executed' OR [DecisionStatus]='Pending'),
    CONSTRAINT [FK_ItemLifecycleDecision_Condition] FOREIGN KEY ([ConditionId]) REFERENCES [dbo].[Condition] ([ConditionID]),
    CONSTRAINT [FK_ItemLifecycleDecision_DecidedBy] FOREIGN KEY ([DecidedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_ItemLifecycleDecision_DecisionType] FOREIGN KEY ([DecisionTypeId]) REFERENCES [dbo].[ItemDecisionType] ([DecisionTypeId]),
    CONSTRAINT [FK_ItemLifecycleDecision_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId])
);


GO
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_DecisionType]
    ON [dbo].[ItemLifecycleDecision]([DecisionTypeId] ASC, [DecisionStatus] ASC)
    INCLUDE([ItemId], [DecidedAt]);


GO
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_DecidedAt]
    ON [dbo].[ItemLifecycleDecision]([DecidedAt] DESC)
    INCLUDE([ItemId], [DecisionTypeId], [DecisionStatus], [DecidedBy]);


GO
CREATE NONCLUSTERED INDEX [IX_ItemLifecycleDecision_ItemId]
    ON [dbo].[ItemLifecycleDecision]([ItemId] ASC, [DecidedAt] DESC)
    INCLUDE([DecisionTypeId], [DecisionStatus], [DecidedBy]);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UQ_ItemLifecycleDecision_Executed]
    ON [dbo].[ItemLifecycleDecision]([ItemId] ASC) WHERE ([DecisionStatus]='Executed');

