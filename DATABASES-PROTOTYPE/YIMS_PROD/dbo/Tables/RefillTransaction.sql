CREATE TABLE [dbo].[RefillTransaction] (
    [RefillTransactionId] INT            IDENTITY (1, 1) NOT NULL,
    [BatchId]             INT            NOT NULL,
    [VendorId]            INT            NOT NULL,
    [SentQty]             INT            NOT NULL,
    [SentDate]            DATETIME       CONSTRAINT [DF_RefillTransaction_SentDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NOT NULL,
    [ReceivedQty]         INT            NULL,
    [ReceivedDate]        DATETIME       CONSTRAINT [DF_RefillTransaction_ReceivedDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [Status]              VARCHAR (20)   CONSTRAINT [DF_RefillTransaction_Status] DEFAULT ('Sent') NOT NULL,
    [CreatedBy]           INT            NOT NULL,
    [CreatedDate]         DATETIME       CONSTRAINT [DF_RefillTransaction_CreatedDate] DEFAULT (getdate()) NOT NULL,
    [Remarks]             NVARCHAR (500) NULL,
    CONSTRAINT [PK_RefillTransaction] PRIMARY KEY CLUSTERED ([RefillTransactionId] ASC),
    CONSTRAINT [CK_RefillTransaction_Qty] CHECK ([SentQty]>(0) AND ([ReceivedQty] IS NULL OR [ReceivedQty]>=(0))),
    CONSTRAINT [CK_RefillTransaction_Status] CHECK ([Status]='Completed' OR [Status]='PartiallyReceived' OR [Status]='Sent'),
    CONSTRAINT [FK_RefillTransaction_Batch] FOREIGN KEY ([BatchId]) REFERENCES [dbo].[VendorCartridgeBatch] ([BatchId]),
    CONSTRAINT [FK_RefillTransaction_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID])
);


GO
CREATE NONCLUSTERED INDEX [IX_RefillTransaction_BatchId]
    ON [dbo].[RefillTransaction]([BatchId] ASC)
    INCLUDE([Status], [SentQty], [ReceivedQty]);


GO
CREATE TRIGGER dbo.trg_RefillTransaction_EnforceVendorMatchesBatch
ON dbo.RefillTransaction
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted i
        INNER JOIN dbo.VendorCartridgeBatch b ON b.BatchId = i.BatchId
        WHERE i.VendorId <> b.VendorId
    )
    BEGIN
        RAISERROR ('RefillTransaction.VendorId must match VendorCartridgeBatch.VendorId for the given BatchId.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END
END;
