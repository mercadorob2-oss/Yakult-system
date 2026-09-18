-- =============================================================================
-- Migration: Systems Portal feature completion
-- Purpose:   Adds card access rules, health metadata,
--            maintenance scheduling, module/deep-link metadata, and settings.
-- Notes:     Safe to run repeatedly.
-- =============================================================================

IF COL_LENGTH('dbo.PortalCard', 'AllowedRoles') IS NULL
    ALTER TABLE dbo.PortalCard ADD AllowedRoles NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.PortalCard', 'AllowedUserIds') IS NULL
    ALTER TABLE dbo.PortalCard ADD AllowedUserIds NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.PortalCard', 'HealthCheckUrl') IS NULL
    ALTER TABLE dbo.PortalCard ADD HealthCheckUrl NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.PortalCard', 'LastHealthStatus') IS NULL
    ALTER TABLE dbo.PortalCard ADD LastHealthStatus NVARCHAR(30) NULL;

IF COL_LENGTH('dbo.PortalCard', 'LastHealthCheckedAt') IS NULL
    ALTER TABLE dbo.PortalCard ADD LastHealthCheckedAt DATETIME2 NULL;

IF COL_LENGTH('dbo.PortalCard', 'MaintenanceStartUtc') IS NULL
    ALTER TABLE dbo.PortalCard ADD MaintenanceStartUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.PortalCard', 'MaintenanceEndUtc') IS NULL
    ALTER TABLE dbo.PortalCard ADD MaintenanceEndUtc DATETIME2 NULL;

IF COL_LENGTH('dbo.PortalCard', 'DeepLink') IS NULL
    ALTER TABLE dbo.PortalCard ADD DeepLink NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.PortalCard', 'InstallerPath') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerPath NVARCHAR(500) NULL;

IF COL_LENGTH('dbo.PortalCard', 'InstallerVersion') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerVersion NVARCHAR(80) NULL;

IF COL_LENGTH('dbo.PortalCard', 'InstallerNotes') IS NULL
    ALTER TABLE dbo.PortalCard ADD InstallerNotes NVARCHAR(1000) NULL;

IF OBJECT_ID('dbo.PortalSetting', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalSetting (
        SettingKey NVARCHAR(120) NOT NULL PRIMARY KEY,
        SettingValue NVARCHAR(MAX) NULL,
        DateModified DATETIME2 NOT NULL CONSTRAINT DF_PortalSetting_DateModified DEFAULT (sysutcdatetime()),
        ModifiedByUserId INT NULL
    );
END
