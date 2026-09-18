CREATE TABLE [dbo].[UnfulfilledCartridgeExchange] (
    [UnfulfilledId]    INT             IDENTITY (1, 1) NOT NULL,
    [ReqId]            INT             NOT NULL,
    [EmpId]            INT             NOT NULL,
    [BranchId]         INT             NULL,
    [DeptId]           INT             NULL,
    [CartridgeModel]   NVARCHAR (100)  NOT NULL,
    [RequestedQty]     INT             NOT NULL,
    [ReturnedEmptyQty] INT             NOT NULL,
    [IssuedFullQty]    INT             NOT NULL,
    [UnfulfilledQty]   INT             NOT NULL,
    [Remarks]          NVARCHAR (1000) NULL,
    [Status]           VARCHAR (20)    DEFAULT ('Pending') NOT NULL,
    [CreatedDate]      DATETIME        DEFAULT (getdate()) NOT NULL,
    [CreatedBy]        INT             NOT NULL,
    [FulfilledDate]    DATETIME        CONSTRAINT [DF_UnfulfilledCartridgeExchange_FulfilledDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [FulfilledBy]      INT             NULL,
    [FulfilledRemarks] NVARCHAR (500)  NULL,
    [GoodEmptyQty]     INT             NULL,
    [DamagedEmptyQty]  INT             NULL,
    PRIMARY KEY CLUSTERED ([UnfulfilledId] ASC),
    CONSTRAINT [CK_UnfulfilledCartridgeExchange_Quantities] CHECK ([UnfulfilledQty]=([ReturnedEmptyQty]-[IssuedFullQty])),
    CONSTRAINT [CK_UnfulfilledCartridgeExchange_Status] CHECK ([Status]='Fulfilled' OR [Status]='Pending'),
    CONSTRAINT [CK_UnfulfilledCartridgeExchange_UnfulfilledQty] CHECK ([UnfulfilledQty]>=(0)),
    CONSTRAINT [FK_UnfulfilledCartridgeExchange_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId]),
    CONSTRAINT [FK_UnfulfilledCartridgeExchange_Request] FOREIGN KEY ([ReqId]) REFERENCES [dbo].[Request] ([ReqId])
);




GO
CREATE NONCLUSTERED INDEX [IX_UnfulfilledCartridgeExchange_Status_CreatedDate]
    ON [dbo].[UnfulfilledCartridgeExchange]([Status] ASC, [CreatedDate] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_UnfulfilledCartridgeExchange_CartridgeModel]
    ON [dbo].[UnfulfilledCartridgeExchange]([CartridgeModel] ASC, [Status] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_UnfulfilledCartridgeExchange_ReqId]
    ON [dbo].[UnfulfilledCartridgeExchange]([ReqId] ASC);

