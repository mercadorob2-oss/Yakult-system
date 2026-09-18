-- =============================================================================
-- Migration: Create dbo.UserPortalAccess
-- Purpose:   Stores per-user portal access grants that supplement role-based
--            access. A user can access a portal if their role allows it OR if
--            a row exists here for them.
-- Run after: Migration_Portal_CreateAndSeed.sql
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.UserPortalAccess') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.UserPortalAccess (
        UserId   INT NOT NULL,
        PortalId INT NOT NULL,

        CONSTRAINT PK_UserPortalAccess
            PRIMARY KEY CLUSTERED (UserId, PortalId),

        CONSTRAINT FK_UserPortalAccess_User
            FOREIGN KEY (UserId) REFERENCES dbo.[User] (UserId),

        CONSTRAINT FK_UserPortalAccess_Portal
            FOREIGN KEY (PortalId) REFERENCES dbo.Portal (PortalId)
    );
END
