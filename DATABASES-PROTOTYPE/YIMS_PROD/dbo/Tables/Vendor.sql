CREATE TABLE [dbo].[Vendor] (
    [VendorID]    INT            IDENTITY (1, 1) NOT NULL,
    [VendorName]  NVARCHAR (100) NOT NULL,
    [Address]     NVARCHAR (200) NULL,
    [IsActive]    BIT            DEFAULT ((1)) NOT NULL,
    [CreatedDate] DATETIME       DEFAULT (getdate()) NOT NULL,
    [TIN]         NVARCHAR (50)  NULL,
    [IsRefiller]  BIT            CONSTRAINT [DF_Vendor_IsRefiller]  DEFAULT ((0)) NOT NULL,
    [IsDisposer]  BIT            CONSTRAINT [DF_Vendor_IsDisposer]  DEFAULT ((0)) NOT NULL,
    [IsBuyer]     BIT            CONSTRAINT [DF_Vendor_IsBuyer]     DEFAULT ((0)) NOT NULL,
    PRIMARY KEY CLUSTERED ([VendorID] ASC)
);




GO
CREATE NONCLUSTERED INDEX [IX_Vendor_IsActive]
    ON [dbo].[Vendor]([IsActive] ASC)
    INCLUDE([VendorName], [Address], [TIN]);


GO
CREATE NONCLUSTERED INDEX [IX_Vendor_VendorName]
    ON [dbo].[Vendor]([VendorName] ASC);


GO
CREATE NONCLUSTERED INDEX [IX_Vendor_TIN]
    ON [dbo].[Vendor]([TIN] ASC);

