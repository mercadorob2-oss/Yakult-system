CREATE TABLE [dbo].[VendorCartridgeModel] (
    [VendorId]         INT      NOT NULL,
    [CartridgeModelId] INT      NOT NULL,
    [IsActive]         BIT      NOT NULL CONSTRAINT [DF_VendorCartridgeModel_IsActive] DEFAULT ((1)),
    [CreatedDate]      DATETIME NOT NULL CONSTRAINT [DF_VendorCartridgeModel_CreatedDate] DEFAULT (GETDATE()),
    [CreatedBy]        INT      NULL,

    CONSTRAINT [PK_VendorCartridgeModel] PRIMARY KEY CLUSTERED ([VendorId] ASC, [CartridgeModelId] ASC),
    CONSTRAINT [FK_VendorCartridgeModel_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID]),
    CONSTRAINT [FK_VendorCartridgeModel_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId])
);


GO
CREATE NONCLUSTERED INDEX [IX_VendorCartridgeModel_CartridgeModelId]
    ON [dbo].[VendorCartridgeModel]([CartridgeModelId] ASC, [VendorId] ASC)
    INCLUDE([IsActive]);


