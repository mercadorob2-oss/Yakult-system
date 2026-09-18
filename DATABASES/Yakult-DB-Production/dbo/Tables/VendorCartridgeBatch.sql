CREATE TABLE [dbo].[VendorCartridgeBatch] (
    [BatchId]             INT            IDENTITY (1, 1) NOT NULL,
    [VendorId]            INT            NOT NULL,
    [OriginalQty]         INT            NOT NULL,
    [Status]              VARCHAR (30)   NOT NULL,
    [DateReceived]        DATETIME       CONSTRAINT [DF_VendorCartridgeBatch_DateReceived] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NOT NULL,
    [CreatedBy]           INT            NOT NULL,
    [CreatedDate]         DATETIME       CONSTRAINT [DF_VendorCartridgeBatch_CreatedDate] DEFAULT (getdate()) NOT NULL,
    [Remarks]             NVARCHAR (500) NULL,
    [ReturnedQty]         INT            DEFAULT ((0)) NOT NULL,
    [CartridgeModelId]    INT            NULL,
    [MarkedForReturnDate] DATETIME2 (2)  CONSTRAINT [DF_VendorCartridgeBatch_MarkedForReturnDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [MarkedForReturnBy]   INT            NULL,
    [ClosedDate]          DATETIME2 (2)  CONSTRAINT [DF_VendorCartridgeBatch_ClosedDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ClosedBy]            INT            NULL,
    [BatchPurpose]        VARCHAR (10)   CONSTRAINT [DF_VendorCartridgeBatch_BatchPurpose] DEFAULT ('REFILL') NOT NULL,
    CONSTRAINT [PK_VendorCartridgeBatch] PRIMARY KEY CLUSTERED ([BatchId] ASC),
    CONSTRAINT [CK_VendorCartridgeBatch_BatchPurpose] CHECK ([BatchPurpose]='SELL' OR [BatchPurpose]='DISPOSE' OR [BatchPurpose]='REFILL'),
    CONSTRAINT [CK_VendorCartridgeBatch_Status] CHECK ([Status]='Sold' OR [Status]='Disposed' OR [Status]='Active' OR [Status]='ForReturn' OR [Status]='SentForRefill' OR [Status]='Completed' OR [Status]='Closed'),
    CONSTRAINT [FK_VendorCartridgeBatch_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId]),
    CONSTRAINT [FK_VendorCartridgeBatch_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID])
);




GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatch_CartridgeModelId]
    ON [dbo].[VendorCartridgeBatch]([CartridgeModelId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatch_BatchId]
    ON [dbo].[VendorCartridgeBatch]([BatchId] ASC)
    INCLUDE([VendorId], [CartridgeModelId], [Status], [ReturnedQty]);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatch_Status]
    ON [dbo].[VendorCartridgeBatch]([Status] ASC)
    INCLUDE([BatchId], [VendorId], [CartridgeModelId]);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatch_VendorId]
    ON [dbo].[VendorCartridgeBatch]([VendorId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeBatch_BatchPurpose]
    ON [dbo].[VendorCartridgeBatch]([BatchPurpose] ASC, [Status] ASC) WHERE ([BatchPurpose] IN ('DISPOSE', 'SELL'));

