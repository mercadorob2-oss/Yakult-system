CREATE TABLE [dbo].[CartridgeRequestModel] (
    [RequestModelId]   INT            IDENTITY (1, 1) NOT NULL,
    [ReqId]            INT            NOT NULL,
    [CartridgeModel]   NVARCHAR (100) NOT NULL,
    [RequestedQty]     INT            NOT NULL,
    [ReturnedEmptyQty] INT            NULL,
    [IssuedFullQty]    INT            NULL,
    [UnfulfilledQty]   INT            NULL,
    [Status]           VARCHAR (20)   NULL,
    [Remarks]          NVARCHAR (500) NULL,
    [ProcessedDate]    DATETIME       CONSTRAINT [DF_CartridgeRequestModel_ProcessedDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ProcessedBy]      INT            NULL,
    [CreatedDate]      DATETIME       DEFAULT (getdate()) NOT NULL,
    [GoodEmptyQty]     INT            NULL,
    [DamagedEmptyQty]  INT            NULL,
    PRIMARY KEY CLUSTERED ([RequestModelId] ASC),
    CONSTRAINT [CK_CartridgeRequestModel_DamagedEmptyQty] CHECK ([DamagedEmptyQty] IS NULL OR [DamagedEmptyQty]>=(0)),
    CONSTRAINT [CK_CartridgeRequestModel_EmptyQtySum] CHECK ([GoodEmptyQty] IS NULL OR [DamagedEmptyQty] IS NULL OR ([GoodEmptyQty]+[DamagedEmptyQty])=[RequestedQty]),
    CONSTRAINT [CK_CartridgeRequestModel_GoodEmptyQty] CHECK ([GoodEmptyQty] IS NULL OR [GoodEmptyQty]>=(0)),
    CONSTRAINT [FK_CartridgeRequestModel_Request] FOREIGN KEY ([ReqId]) REFERENCES [dbo].[Request] ([ReqId])
);




GO
CREATE NONCLUSTERED INDEX [IX_CartridgeRequestModel_ReqId]
    ON [dbo].[CartridgeRequestModel]([ReqId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_CartridgeRequestModel_Model_Status]
    ON [dbo].[CartridgeRequestModel]([CartridgeModel] ASC, [Status] ASC);

