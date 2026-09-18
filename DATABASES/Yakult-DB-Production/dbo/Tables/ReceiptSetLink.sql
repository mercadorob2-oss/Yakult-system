CREATE TABLE [dbo].[ReceiptSetLink] (
    [ReceiptSetId]      INT           NOT NULL,
    [SetId]             INT           NOT NULL,
    [CreatedAt]         DATETIME      CONSTRAINT [DF_ReceiptSetLink_CreatedAt] DEFAULT (getdate()) NOT NULL,
    [CreatedBy]         INT           NULL,
    [ModifiedAt]        DATETIME      CONSTRAINT [DF_ReceiptSetLink_ModifiedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedBy]        INT           NULL,
    [CoverageStartDate] DATETIME2 (7) NULL,
    [CoverageEndDate]   DATETIME2 (7) NULL,
    CONSTRAINT [PK_ReceiptSetLink] PRIMARY KEY CLUSTERED ([ReceiptSetId] ASC, [SetId] ASC),
    CONSTRAINT [FK_ReceiptSetLink_ReceiptSet] FOREIGN KEY ([ReceiptSetId]) REFERENCES [dbo].[ReceiptSet] ([ReceiptSetId]) ON DELETE CASCADE,
    CONSTRAINT [FK_ReceiptSetLink_Set] FOREIGN KEY ([SetId]) REFERENCES [dbo].[Set] ([SetId]) ON DELETE CASCADE
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_ReceiptSetLink_Set_Coverage]
    ON [dbo].[ReceiptSetLink]([SetId] ASC, [CoverageStartDate] ASC, [CoverageEndDate] ASC) WHERE ([CoverageStartDate] IS NOT NULL AND [CoverageEndDate] IS NOT NULL);

