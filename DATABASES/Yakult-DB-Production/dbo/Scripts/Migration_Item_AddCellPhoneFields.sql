-- Migration: Add CellPhoneNumber, IMEI1, IMEI2 to dbo.Item
-- Purpose : Cell phone items (Category = CellPhone) need to record a phone number and up to two
--           IMEI numbers per unit. These columns are NULL for every other category.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'CellPhoneNumber'
)
BEGIN
    ALTER TABLE dbo.Item ADD [CellPhoneNumber] VARCHAR (50) NULL;
    PRINT 'Column CellPhoneNumber added to dbo.Item.';
END
ELSE
BEGIN
    PRINT 'Column CellPhoneNumber already exists on dbo.Item. Skipping.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'IMEI1'
)
BEGIN
    ALTER TABLE dbo.Item ADD [IMEI1] VARCHAR (50) NULL;
    PRINT 'Column IMEI1 added to dbo.Item.';
END
ELSE
BEGIN
    PRINT 'Column IMEI1 already exists on dbo.Item. Skipping.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'IMEI2'
)
BEGIN
    ALTER TABLE dbo.Item ADD [IMEI2] VARCHAR (50) NULL;
    PRINT 'Column IMEI2 added to dbo.Item.';
END
ELSE
BEGIN
    PRINT 'Column IMEI2 already exists on dbo.Item. Skipping.';
END
GO
