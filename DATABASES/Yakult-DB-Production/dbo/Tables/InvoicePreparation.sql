CREATE TABLE [dbo].[InvoicePreparation] (
    [PreparationId]  INT            IDENTITY (1, 1) NOT NULL,
    [SubType]        NVARCHAR (20)  NOT NULL,
    [ReferenceCode]  NVARCHAR (100) NULL,
    [SupplierId]     INT            NULL,
    [BeginDate]      DATE           NULL,
    [EndDate]        DATE           NULL,
    [Status]         NVARCHAR (20)  CONSTRAINT [DF_InvoicePreparation_Status] DEFAULT ('Draft') NOT NULL,
    [GeneratedSetId] INT            NULL,
    [CreatedBy]      INT            NOT NULL,
    [CreatedAt]      DATETIME2 (7)  CONSTRAINT [DF_InvoicePreparation_CreatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [ModifiedBy]     INT            NULL,
    [ModifiedAt]     DATETIME2 (7)  NULL,
    PRIMARY KEY CLUSTERED ([PreparationId] ASC),
    CONSTRAINT [CK_InvoicePreparation_Status] CHECK ([Status]='Draft' OR [Status]='Completed' OR [Status]='Cancelled'),
    CONSTRAINT [CK_InvoicePreparation_SubType] CHECK (
        [SubType]='Contract' OR [SubType]='Subscription' OR [SubType]='License' OR [SubType]='Services'
    ),
    CONSTRAINT [FK_InvoicePreparation_Supplier] FOREIGN KEY ([SupplierId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [FK_InvoicePreparation_GeneratedSet] FOREIGN KEY ([GeneratedSetId]) REFERENCES [dbo].[Set] ([SetId]),
    CONSTRAINT [FK_InvoicePreparation_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_InvoicePreparation_SubType]
    ON [dbo].[InvoicePreparation]([SubType] ASC);
