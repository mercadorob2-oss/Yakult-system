-- Prepared for manual deployment. This script is not run by the portal.
-- Nullable columns preserve existing tickets and older desktop/mobile clients.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CallTicket') AND name = 'Classification')
BEGIN
    ALTER TABLE dbo.CallTicket ADD Classification NVARCHAR(100) NULL;
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CallTicket') AND name = 'DeviceName')
BEGIN
    ALTER TABLE dbo.CallTicket ADD DeviceName NVARCHAR(150) NULL;
END;
GO
