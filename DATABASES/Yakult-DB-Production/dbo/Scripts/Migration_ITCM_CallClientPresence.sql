-- Migration: Create dbo.CallClientPresence (ITCM connected desktops)
-- Date: 2026-09-05
-- Purpose: Track which desktop clients are actively calling the ITCM web
--          service. Desktops POST a presence beat; the server upserts one row
--          per (MachineName, UserName) stamped with SYSUTCDATETIME (server
--          time, immune to client clock skew). Readers treat
--          LastSeenUtc >= DATEADD(minute, -15, SYSUTCDATETIME()) as online.

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.CallClientPresence')
)
BEGIN
    CREATE TABLE dbo.CallClientPresence
    (
        MachineName   NVARCHAR(64)  NOT NULL,
        UserName      NVARCHAR(128) NOT NULL,
        LastSeenUtc   DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME()),
        FirstSeenUtc  DATETIME2     NOT NULL DEFAULT (SYSUTCDATETIME()),
        ClientVersion NVARCHAR(50)  NULL,
        Module        NVARCHAR(50)  NULL,
        CONSTRAINT PK_CallClientPresence PRIMARY KEY (MachineName, UserName)
    );

    CREATE INDEX IX_CallClientPresence_LastSeenUtc
        ON dbo.CallClientPresence (LastSeenUtc DESC);

    PRINT 'Table dbo.CallClientPresence created.';
END
ELSE
BEGIN
    PRINT 'Table dbo.CallClientPresence already exists. Skipped.';
END
GO
