CREATE TABLE [dbo].[Renewals] (
    [RenewalId]     INT             IDENTITY (1, 1) NOT NULL,
    [ItemId]        INT             NULL,
    [RenewalStatus] NVARCHAR (20)   DEFAULT ('None') NOT NULL,
    [OnHoldDate]    DATETIME2 (7)   CONSTRAINT [DF_Renewals_OnHoldDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RenewedDate]   DATETIME2 (7)   CONSTRAINT [DF_Renewals_RenewedDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RenewalCount]  INT             DEFAULT ((0)) NOT NULL,
    [NewStartDate]  DATETIME2 (7)   CONSTRAINT [DF_Renewals_NewStartDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [NewEndDate]    DATETIME2 (7)   CONSTRAINT [DF_Renewals_NewEndDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RenewalYears]  INT             NULL,
    [IsArchived]    BIT             DEFAULT ((0)) NOT NULL,
    [ArchivedDate]  DATETIME2 (7)   CONSTRAINT [DF_Renewals_ArchivedDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ArchiveReason] NVARCHAR (500)  NULL,
    [CreatedBy]     INT             NOT NULL,
    [CreatedAt]     DATETIME2 (7)   DEFAULT (getdate()) NOT NULL,
    [ModifiedBy]    INT             NULL,
    [ModifiedAt]    DATETIME2 (7)   CONSTRAINT [DF_Renewals_ModifiedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [RenewalNotes]  NVARCHAR (1000) NULL,
    [RenewalAmount] DECIMAL (18, 2) NULL,
    [AssetId]       INT             NULL,
    [PartNumber]    NVARCHAR (100)  NULL,
    CONSTRAINT [PK_Renewals] PRIMARY KEY CLUSTERED ([RenewalId] ASC),
    CONSTRAINT [FK_Renewals_Asset] FOREIGN KEY ([AssetId]) REFERENCES [dbo].[Asset] ([AssetId]),
    CONSTRAINT [FK_Renewals_CreatedBy] FOREIGN KEY ([CreatedBy]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [FK_Renewals_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId])
);


GO
CREATE NONCLUSTERED INDEX [IX_Renewals_ItemId]
    ON [dbo].[Renewals]([ItemId] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Renewals_RenewalStatus]
    ON [dbo].[Renewals]([RenewalStatus] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Renewals_OnHoldDate]
    ON [dbo].[Renewals]([OnHoldDate] ASC) WHERE ([RenewalStatus]='On Hold' AND [IsArchived]=(0));

