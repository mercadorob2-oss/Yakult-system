-- Migration: Create dbo.AdminAlertBroadcast + dbo.AdminAlertAck (admin surprise alerts)
-- Date: 2026-10-14
-- Purpose: Lets an admin pop the STOP alert dialog on chosen PCs. The admin
--          inserts one broadcast row (optionally targeted at a machine and/or
--          user; NULL means everyone). Each desktop polls for unexpired,
--          unacknowledged rows matching its machine/user, shows the dialog
--          once, then writes an ack row. Readers treat
--          ExpiresAt > SYSUTCDATETIME() as live.

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.AdminAlertBroadcast')
)
BEGIN
    CREATE TABLE dbo.AdminAlertBroadcast
    (
        BroadcastId   INT            IDENTITY(1,1) NOT NULL,
        Title         NVARCHAR(100)  NOT NULL DEFAULT ('STOP'),
        Message       NVARCHAR(500)  NULL,
        TargetMachine NVARCHAR(64)   NULL,
        TargetUser    NVARCHAR(128)  NULL,
        CreatedBy     INT            NOT NULL,
        CreatedByName NVARCHAR(128)  NOT NULL DEFAULT (''),
        CreatedAt     DATETIME2      NOT NULL DEFAULT (SYSUTCDATETIME()),
        ExpiresAt     DATETIME2      NOT NULL,
        CONSTRAINT PK_AdminAlertBroadcast PRIMARY KEY (BroadcastId)
    );

    CREATE INDEX IX_AdminAlertBroadcast_ExpiresAt
        ON dbo.AdminAlertBroadcast (ExpiresAt DESC);

    PRINT 'Table dbo.AdminAlertBroadcast created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.AdminAlertBroadcast already exists. Skipped.';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.AdminAlertAck')
)
BEGIN
    CREATE TABLE dbo.AdminAlertAck
    (
        BroadcastId INT           NOT NULL,
        MachineName NVARCHAR(64)  NOT NULL,
        UserName    NVARCHAR(128) NOT NULL,
        AckAt       DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_AdminAlertAck PRIMARY KEY (BroadcastId, MachineName, UserName),
        CONSTRAINT FK_AdminAlertAck_Broadcast FOREIGN KEY (BroadcastId)
            REFERENCES dbo.AdminAlertBroadcast (BroadcastId)
    );

    PRINT 'Table dbo.AdminAlertAck created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.AdminAlertAck already exists. Skipped.';
END
GO
