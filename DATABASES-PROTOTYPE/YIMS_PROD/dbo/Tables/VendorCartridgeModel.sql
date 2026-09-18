CREATE TABLE [dbo].[VendorCartridgeModel] (
    [VendorId]         INT      NOT NULL,
    [CartridgeModelId] INT      NOT NULL,
    [IsActive]         BIT      CONSTRAINT [DF_VendorCartridgeModel_IsActive] DEFAULT ((1)) NOT NULL,
    [CreatedDate]      DATETIME CONSTRAINT [DF_VendorCartridgeModel_CreatedDate] DEFAULT (getdate()) NOT NULL,
    [CreatedBy]        INT      NULL,
    CONSTRAINT [PK_VendorCartridgeModel] PRIMARY KEY CLUSTERED ([VendorId] ASC, [CartridgeModelId] ASC),
    CONSTRAINT [FK_VendorCartridgeModel_CartridgeModel] FOREIGN KEY ([CartridgeModelId]) REFERENCES [dbo].[CartridgeModel] ([CartridgeModelId]),
    CONSTRAINT [FK_VendorCartridgeModel_Vendor] FOREIGN KEY ([VendorId]) REFERENCES [dbo].[Vendor] ([VendorID])
);

