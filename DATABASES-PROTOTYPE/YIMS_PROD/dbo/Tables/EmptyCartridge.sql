CREATE TABLE [dbo].[EmptyCartridge] (
    [EmptyCartridgeId]    INT            IDENTITY (1, 1) NOT NULL,
    [CartridgeModelId]    INT            NOT NULL,
    [VendorId]            INT            NULL,
    [Quantity]            INT            DEFAULT ((1)) NOT NULL,
    [ConditionId]         INT            NULL,
    [VendorBatchId]       INT            NULL,
    [Status]              VARCHAR (50)   DEFAULT ('Pending') NOT NULL,
    [ReturnedAt]          DATETIME       DEFAULT (getdate()) NOT NULL,
    [ReturnedBy]          INT            NOT NULL,
    [ReqId]               INT            NULL,
    [EmpId]               INT            NULL,
    [BranchId]            INT            NULL,
    [DeptId]              INT            NULL,
    [Remarks]             NVARCHAR (500) NULL,
    [CreatedDate]         DATETIME       DEFAULT (getdate()) NOT NULL,
    [CreatedBy]           INT            NOT NULL,
    [DateModified]        DATETIME       CONSTRAINT [DF_EmptyCartridge_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]          INT            NULL,
    [SourceItemId]        INT            NULL,
    [ReturnTransactionId] INT            NULL,
    [RefillStatus]        VARCHAR (20)   NULL,
    [DisposalCompanyName] NVARCHAR (200) NULL,
    [ConditionStatus]     VARCHAR (10)   NULL,
    CONSTRAINT [PK_EmptyCartridge] PRIMARY KEY CLUSTERED ([EmptyCartridgeId] ASC),
    CONSTRAINT [CK_EmptyCartridge_ConditionStatus] CHECK ([ConditionStatus]='DAMAGED' OR [ConditionStatus]='GOOD' OR [ConditionStatus] IS NULL),
    CONSTRAINT [CK_EmptyCartridge_Quantity] CHECK ([Quantity]>(0)),
    CONSTRAINT [CK_EmptyCartridge_RefillStatus] CHECK ([RefillStatus]='Refilled' OR [RefillStatus]='Refilling' OR [RefillStatus]='For Refill' OR [RefillStatus] IS NULL),
    CONSTRAINT [FK_EmptyCartridge_Branch] FOREIGN KEY ([BranchId]) REFERENCES [dbo].[Branch] ([BranchId]),
    CONSTRAINT [FK_EmptyCartridge_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId]),
    CONSTRAINT [FK_EmptyCartridge_Condition] FOREIGN KEY ([ConditionId]) REFERENCES [dbo].[Condition] ([ConditionID]),
    CONSTRAINT [FK_EmptyCartridge_Department] FOREIGN KEY ([DeptId]) REFERENCES [dbo].[Department] ([DeptId]),
    CONSTRAINT [FK_EmptyCartridge_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_EmptyCartridge_Request] FOREIGN KEY ([ReqId]) REFERENCES [dbo].[Request] ([ReqId]),
    CONSTRAINT [FK_EmptyCartridge_SourceItem] FOREIGN KEY ([SourceItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_EmptyCartridge_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [FK_EmptyCartridge_VendorBatch] FOREIGN KEY ([VendorBatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId])
);






GO
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_VendorBatch]
    ON [dbo].[EmptyCartridge]([VendorBatchId] ASC) WHERE ([VendorBatchId] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_RefillStatus]
    ON [dbo].[EmptyCartridge]([RefillStatus] ASC) WHERE ([RefillStatus] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_ReqId]
    ON [dbo].[EmptyCartridge]([ReqId] ASC)
    INCLUDE([RefillStatus], [EmptyCartridgeId]) WHERE ([ReqId] IS NOT NULL);


GO
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_VendorModel_Pending]
    ON [dbo].[EmptyCartridge]([VendorId] ASC, [CartridgeModelId] ASC, [Status] ASC) WHERE ([Status]='Pending' AND [VendorBatchId] IS NULL);


GO
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_ConditionStatus]
    ON [dbo].[EmptyCartridge]([ConditionStatus] ASC) WHERE ([ConditionStatus] IS NOT NULL);

