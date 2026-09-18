-- =============================================================================
-- Migration: Yakult Systems Portal - complete installation
-- Purpose:   Installs the database objects required by Yakult.SystemsPortal.
-- Safe:      Idempotent; safe to run repeatedly on the selected Yakult database.
-- Requires:  dbo.[User] and dbo.[Role] from the core Yakult schema.
-- Creates:   dbo.Portal
--            dbo.RolePortalAccess
--            dbo.UserPortalAccess
--            dbo.PortalCard
--            dbo.PortalSetting
--            dbo.AuditTrail (only when absent)
-- Notes:     Does not insert employee, user, account-request, or card dummy data.
-- =============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
    THROW 50001, 'Required core table dbo.[User] does not exist. Select a Yakult application database first.', 1;

IF OBJECT_ID(N'dbo.[Role]', N'U') IS NULL
    THROW 50002, 'Required core table dbo.[Role] does not exist. Select a Yakult application database first.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    -- -------------------------------------------------------------------------
    -- 1. Portal catalog
    -- -------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.Portal', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Portal
        (
            PortalId    INT          IDENTITY(1,1) NOT NULL,
            PortalKey   VARCHAR(50)  NOT NULL,
            DisplayName VARCHAR(100) NOT NULL,
            IsActive    BIT          NOT NULL CONSTRAINT DF_Portal_IsActive DEFAULT (1),
            DateCreated DATETIME     NOT NULL CONSTRAINT DF_Portal_DateCreated DEFAULT (GETDATE()),
            CONSTRAINT PK_Portal PRIMARY KEY CLUSTERED (PortalId),
            CONSTRAINT UQ_Portal_Key UNIQUE (PortalKey)
        );
    END;

    -- These are application identifiers, not demonstration records.
    ;WITH RequiredPortals (PortalKey, DisplayName) AS
    (
        SELECT PortalKey, DisplayName
        FROM (VALUES
            ('InventorySystem',     'Inventory System'),
            ('CallITMonitoring',    'Call IT Monitoring'),
            ('RequesterPortal',     'Requester Portal'),
            ('CartridgeManagement', 'Cartridge Management'),
            ('BorrowItems',         'Borrow Items'),
            ('Reports',             'Reports'),
            ('AdminPortal',         'Admin Portal')
        ) value_list (PortalKey, DisplayName)
    )
    INSERT INTO dbo.Portal (PortalKey, DisplayName)
    SELECT required.PortalKey, required.DisplayName
    FROM RequiredPortals required
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.Portal existing
        WHERE existing.PortalKey = required.PortalKey
    );

    -- -------------------------------------------------------------------------
    -- 2. Role-based and per-user portal access
    -- -------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.RolePortalAccess', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RolePortalAccess
        (
            RoleId   INT NOT NULL,
            PortalId INT NOT NULL,
            CONSTRAINT PK_RolePortalAccess PRIMARY KEY CLUSTERED (RoleId, PortalId),
            CONSTRAINT FK_RolePortalAccess_Role
                FOREIGN KEY (RoleId) REFERENCES dbo.[Role] (RoleId),
            CONSTRAINT FK_RolePortalAccess_Portal
                FOREIGN KEY (PortalId) REFERENCES dbo.Portal (PortalId)
        );
    END;

    IF OBJECT_ID(N'dbo.UserPortalAccess', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.UserPortalAccess
        (
            UserId   INT NOT NULL,
            PortalId INT NOT NULL,
            CONSTRAINT PK_UserPortalAccess PRIMARY KEY CLUSTERED (UserId, PortalId),
            CONSTRAINT FK_UserPortalAccess_User
                FOREIGN KEY (UserId) REFERENCES dbo.[User] (UserId),
            CONSTRAINT FK_UserPortalAccess_Portal
                FOREIGN KEY (PortalId) REFERENCES dbo.Portal (PortalId)
        );
    END;

    ;WITH RequiredGrants (RoleName, PortalKey) AS
    (
        SELECT RoleName, PortalKey
        FROM (VALUES
            ('Admin',            'InventorySystem'),
            ('Admin',            'CallITMonitoring'),
            ('Admin',            'RequesterPortal'),
            ('Admin',            'CartridgeManagement'),
            ('Admin',            'BorrowItems'),
            ('Admin',            'Reports'),
            ('InventoryManager', 'InventorySystem'),
            ('InventoryManager', 'Reports'),
            ('Requester',        'RequesterPortal'),
            ('Viewer',           'InventorySystem'),
            ('Viewer',           'CallITMonitoring'),
            ('Viewer',           'RequesterPortal'),
            ('IT Manager',       'InventorySystem'),
            ('IT Manager',       'CallITMonitoring'),
            ('IT Manager',       'CartridgeManagement'),
            ('IT Manager',       'BorrowItems'),
            ('IT Manager',       'Reports'),
            ('IT Manager',       'AdminPortal'),
            ('Supervisor',       'InventorySystem'),
            ('Supervisor',       'CallITMonitoring'),
            ('Supervisor',       'CartridgeManagement'),
            ('Supervisor',       'BorrowItems'),
            ('Supervisor',       'Reports'),
            ('Tech Support',     'CallITMonitoring'),
            ('Tech Support',     'CartridgeManagement'),
            ('Tech Support',     'BorrowItems')
        ) value_list (RoleName, PortalKey)
    )
    INSERT INTO dbo.RolePortalAccess (RoleId, PortalId)
    SELECT role_row.RoleId, portal_row.PortalId
    FROM RequiredGrants required
    INNER JOIN dbo.[Role] role_row
        ON role_row.RoleName = required.RoleName
    INNER JOIN dbo.Portal portal_row
        ON portal_row.PortalKey = required.PortalKey
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.RolePortalAccess existing
        WHERE existing.RoleId = role_row.RoleId
          AND existing.PortalId = portal_row.PortalId
    );

    -- -------------------------------------------------------------------------
    -- 3. Systems Portal homepage/admin cards
    -- -------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.PortalCard', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalCard
        (
            PortalCardId          INT            IDENTITY(1,1) NOT NULL,
            CardKey               NVARCHAR(100)  NOT NULL,
            Name                  NVARCHAR(150)  NOT NULL,
            Description           NVARCHAR(500)  NULL,
            Url                   NVARCHAR(500)  NULL,
            Badge                 NVARCHAR(100)  NULL,
            Theme                 NVARCHAR(50)   NULL,
            Icon                  NVARCHAR(50)   NULL,
            Status                NVARCHAR(30)   NOT NULL CONSTRAINT DF_PortalCard_Status DEFAULT (N'NotConnected'),
            MaintenanceNote       NVARCHAR(500)  NULL,
            IsRdpDownload         BIT            NOT NULL CONSTRAINT DF_PortalCard_IsRdpDownload DEFAULT (0),
            IsInstallerDownload   BIT            NOT NULL CONSTRAINT DF_PortalCard_IsInstallerDownload DEFAULT (0),
            IsVisible             BIT            NOT NULL CONSTRAINT DF_PortalCard_IsVisible DEFAULT (1),
            IsActive              BIT            NOT NULL CONSTRAINT DF_PortalCard_IsActive DEFAULT (1),
            SortOrder             INT            NOT NULL CONSTRAINT DF_PortalCard_SortOrder DEFAULT (0),
            AllowedRoles          NVARCHAR(500)  NULL,
            AllowedUserIds        NVARCHAR(500)  NULL,
            HealthCheckUrl        NVARCHAR(500)  NULL,
            LastHealthStatus      NVARCHAR(30)   NULL,
            LastHealthCheckedAt   DATETIME2(2)   NULL,
            MaintenanceStartUtc   DATETIME2(2)   NULL,
            MaintenanceEndUtc     DATETIME2(2)   NULL,
            DeepLink              NVARCHAR(500)  NULL,
            InstallerPath         NVARCHAR(500)  NULL,
            InstallerVersion      NVARCHAR(80)   NULL,
            InstallerNotes        NVARCHAR(1000) NULL,
            DateCreated           DATETIME2(2)   NOT NULL CONSTRAINT DF_PortalCard_DateCreated DEFAULT (SYSUTCDATETIME()),
            CreatedByUserId       INT            NULL,
            DateModified          DATETIME2(2)   NOT NULL CONSTRAINT DF_PortalCard_DateModified DEFAULT (SYSUTCDATETIME()),
            ModifiedByUserId      INT            NULL,
            RowVer                ROWVERSION     NOT NULL,
            CONSTRAINT PK_PortalCard PRIMARY KEY CLUSTERED (PortalCardId),
            CONSTRAINT UQ_PortalCard_CardKey UNIQUE (CardKey),
            CONSTRAINT CK_PortalCard_Status CHECK (Status IN (N'Connected', N'Maintenance', N'NotConnected', N'Disabled')),
            CONSTRAINT FK_PortalCard_CreatedByUser
                FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User] (UserId),
            CONSTRAINT FK_PortalCard_ModifiedByUser
                FOREIGN KEY (ModifiedByUserId) REFERENCES dbo.[User] (UserId)
        );
    END;

    -- Upgrade an older PortalCard table when it already exists.
    IF COL_LENGTH(N'dbo.PortalCard', N'AllowedRoles') IS NULL
        ALTER TABLE dbo.PortalCard ADD AllowedRoles NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'AllowedUserIds') IS NULL
        ALTER TABLE dbo.PortalCard ADD AllowedUserIds NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'HealthCheckUrl') IS NULL
        ALTER TABLE dbo.PortalCard ADD HealthCheckUrl NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'LastHealthStatus') IS NULL
        ALTER TABLE dbo.PortalCard ADD LastHealthStatus NVARCHAR(30) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'LastHealthCheckedAt') IS NULL
        ALTER TABLE dbo.PortalCard ADD LastHealthCheckedAt DATETIME2(2) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'MaintenanceStartUtc') IS NULL
        ALTER TABLE dbo.PortalCard ADD MaintenanceStartUtc DATETIME2(2) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'MaintenanceEndUtc') IS NULL
        ALTER TABLE dbo.PortalCard ADD MaintenanceEndUtc DATETIME2(2) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'DeepLink') IS NULL
        ALTER TABLE dbo.PortalCard ADD DeepLink NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'InstallerPath') IS NULL
        ALTER TABLE dbo.PortalCard ADD InstallerPath NVARCHAR(500) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'InstallerVersion') IS NULL
        ALTER TABLE dbo.PortalCard ADD InstallerVersion NVARCHAR(80) NULL;
    IF COL_LENGTH(N'dbo.PortalCard', N'InstallerNotes') IS NULL
        ALTER TABLE dbo.PortalCard ADD InstallerNotes NVARCHAR(1000) NULL;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.PortalCard')
          AND name = N'IX_PortalCard_VisibleSort'
    )
    AND NOT EXISTS
    (
        SELECT 1 FROM sys.stats
        WHERE object_id = OBJECT_ID(N'dbo.PortalCard')
          AND name = N'IX_PortalCard_VisibleSort'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_PortalCard_VisibleSort
                ON dbo.PortalCard (IsActive, IsVisible, SortOrder)
                INCLUDE (CardKey, Name, Status);';

    -- -------------------------------------------------------------------------
    -- 4. Portal settings (includes the global homepage notice)
    -- -------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.PortalSetting', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.PortalSetting
        (
            SettingKey      NVARCHAR(120) NOT NULL,
            SettingValue    NVARCHAR(MAX) NULL,
            DateModified    DATETIME2(2)  NOT NULL CONSTRAINT DF_PortalSetting_DateModified DEFAULT (SYSUTCDATETIME()),
            ModifiedByUserId INT          NULL,
            CONSTRAINT PK_PortalSetting PRIMARY KEY CLUSTERED (SettingKey),
            CONSTRAINT FK_PortalSetting_ModifiedByUser
                FOREIGN KEY (ModifiedByUserId) REFERENCES dbo.[User] (UserId)
        );
    END;

    -- -------------------------------------------------------------------------
    -- 5. Audit table used for card, portal, and download activity
    -- -------------------------------------------------------------------------
    IF OBJECT_ID(N'dbo.AuditTrail', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AuditTrail
        (
            Id         INT            IDENTITY(1,1) NOT NULL,
            Action     NVARCHAR(100)  NOT NULL,
            EntityId   INT            NULL,
            EntityType NVARCHAR(100)  NULL,
            UserId     INT            NULL,
            UserName   NVARCHAR(255)  NULL,
            [Timestamp] DATETIME2(7) NOT NULL CONSTRAINT DF_AuditTrail_Timestamp DEFAULT (SYSUTCDATETIME()),
            Notes      NVARCHAR(MAX)  NULL,
            OldValues  NVARCHAR(MAX)  NULL,
            NewValues  NVARCHAR(MAX)  NULL,
            IpAddress  NVARCHAR(50)   NULL,
            CONSTRAINT PK_AuditTrail PRIMARY KEY CLUSTERED (Id)
        );
    END;

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AuditTrail')
          AND name = N'IX_AuditTrail_Entity'
    )
    AND NOT EXISTS
    (
        SELECT 1 FROM sys.stats
        WHERE object_id = OBJECT_ID(N'dbo.AuditTrail')
          AND name = N'IX_AuditTrail_Entity'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_AuditTrail_Entity
                ON dbo.AuditTrail (EntityType, EntityId)
                INCLUDE (Action, UserId, UserName, [Timestamp]);';

    IF NOT EXISTS
    (
        SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.AuditTrail')
          AND name = N'IX_AuditTrail_Timestamp'
    )
    AND NOT EXISTS
    (
        SELECT 1 FROM sys.stats
        WHERE object_id = OBJECT_ID(N'dbo.AuditTrail')
          AND name = N'IX_AuditTrail_Timestamp'
    )
        EXEC sys.sp_executesql N'
            CREATE INDEX IX_AuditTrail_Timestamp
                ON dbo.AuditTrail ([Timestamp]);';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;

-- =============================================================================
-- Verification output
-- =============================================================================
SELECT
    ObjectName,
    CASE WHEN OBJECT_ID(N'dbo.' + ObjectName, N'U') IS NULL THEN 'MISSING' ELSE 'READY' END AS InstallationStatus
FROM (VALUES
    ('Portal'),
    ('RolePortalAccess'),
    ('UserPortalAccess'),
    ('PortalCard'),
    ('PortalSetting'),
    ('AuditTrail')
) objects (ObjectName)
ORDER BY ObjectName;

SELECT PortalId, PortalKey, DisplayName, IsActive
FROM dbo.Portal
ORDER BY PortalId;

SELECT role_row.RoleName, portal_row.PortalKey
FROM dbo.RolePortalAccess access_row
INNER JOIN dbo.[Role] role_row ON role_row.RoleId = access_row.RoleId
INNER JOIN dbo.Portal portal_row ON portal_row.PortalId = access_row.PortalId
ORDER BY role_row.RoleName, portal_row.PortalKey;

PRINT 'Yakult Systems Portal database installation completed successfully.';
