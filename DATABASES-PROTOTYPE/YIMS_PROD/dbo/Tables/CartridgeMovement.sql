CREATE TABLE [dbo].[CartridgeMovement] (
    [MovementId]         INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]             INT            NOT NULL,
    [EmployeeId]         INT            NULL,
    [Quantity]           INT            NOT NULL,
    [MovementType]       VARCHAR (30)   NOT NULL,
    [CartridgeTypeId]    INT            NOT NULL,
    [ReferenceRequestId] INT            NULL,
    [CreatedBy]          INT            NOT NULL,
    [CreatedAt]          DATETIME       DEFAULT (getdate()) NOT NULL,
    [BranchId]           INT            NULL,
    [DeptId]             INT            NULL,
    [ConditionType]      VARCHAR (20)   NULL,
    [Remarks]            NVARCHAR (500) NULL,
    PRIMARY KEY CLUSTERED ([MovementId] ASC),
    CHECK ([MovementType]='Adjustment' OR [MovementType]='RefillIn' OR [MovementType]='Returned' OR [MovementType]='Issued' OR [MovementType]='StockIn'),
    CHECK ([Quantity]>(0)),
    CONSTRAINT [FK_CartridgeMovement_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_CartridgeMovement_CartridgeType] FOREIGN KEY ([CartridgeTypeId]) REFERENCES [dbo].[CartridgeType] ([CartridgeTypeId]),
    CONSTRAINT [FK_CartridgeMovement_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_CartridgeMovement_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId])
);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_ItemId]
    ON [dbo].[CartridgeMovement]([ItemId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_EmployeeId]
    ON [dbo].[CartridgeMovement]([EmployeeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_CreatedAt]
    ON [dbo].[CartridgeMovement]([CreatedAt] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_CartridgeTypeId]
    ON [dbo].[CartridgeMovement]([CartridgeTypeId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_BranchId]
    ON [dbo].[CartridgeMovement]([BranchId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_DeptId]
    ON [dbo].[CartridgeMovement]([DeptId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeMovement_Item_CreatedAt]
    ON [dbo].[CartridgeMovement]([ItemId] ASC, [CreatedAt] DESC)
    INCLUDE([Quantity], [MovementType], [CartridgeTypeId], [EmployeeId], [BranchId], [DeptId]);

