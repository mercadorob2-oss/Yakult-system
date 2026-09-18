CREATE TABLE [dbo].[Asset] (
    [AssetId]      INT            IDENTITY (1, 1) NOT NULL,
    [ItemId]       INT            NULL,
    [SerialNumber] VARCHAR (255)  NULL,
    [ModelNumber]  NVARCHAR (100) NULL,
    [VendorId]     INT            NULL,
    [IsActive]     BIT            DEFAULT ((1)) NOT NULL,
    [CreatedAt]    DATETIME2 (7)  CONSTRAINT [DF_Asset_CreatedAt] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NOT NULL,
    PRIMARY KEY CLUSTERED ([AssetId] ASC),
    CONSTRAINT [CK_Asset_ModelOrSerial] CHECK ([ModelNumber] IS NOT NULL OR [SerialNumber] IS NOT NULL),
    CONSTRAINT [FK_Asset_Item] FOREIGN KEY ([ItemId]) REFERENCES [dbo].[Item] ([ItemId]),
    CONSTRAINT [FK_Asset_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID])
);


GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_Asset_SerialNumber]
    ON [dbo].[Asset]([SerialNumber] ASC) WHERE ([SerialNumber] IS NOT NULL);

