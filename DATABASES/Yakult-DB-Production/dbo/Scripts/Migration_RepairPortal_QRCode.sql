-- Migration_RepairPortal_QRCode.sql
-- Adds scan-to-lookup QR support to Repair Tickets, mirroring dbo.[Set]'s existing
-- QRToken/QRImagePath-less/QRImageData/QRData columns. QRToken defaults to NEWID() so every
-- existing row gets a token the moment this migration runs, and every new row gets one at
-- INSERT time — unlike Sets there is no "not yet dispatched, no token yet" state for repair
-- tickets, so no nullable/opt-in token history is needed here.
-- Idempotent, safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'QRToken'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD QRToken UNIQUEIDENTIFIER NOT NULL
        CONSTRAINT DF_RepairTicket_QRToken DEFAULT (NEWID());
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'QRImageData'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD QRImageData VARBINARY(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'QRData'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD QRData NVARCHAR(MAX) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'UQ_RepairTicket_QRToken'
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_RepairTicket_QRToken
        ON dbo.RepairTicket (QRToken ASC);
END
GO
