-- Migration: Add IsRefiller flag to Vendor to distinguish refill vendors from suppliers
-- Run BEFORE Migration_VendorCartridgeBatch_AddBatchLine.sql

IF COL_LENGTH('dbo.Vendor', 'IsRefiller') IS NULL
BEGIN
    ALTER TABLE dbo.Vendor
    ADD [IsRefiller] BIT NOT NULL
        CONSTRAINT [DF_Vendor_IsRefiller] DEFAULT (0);
END
GO
