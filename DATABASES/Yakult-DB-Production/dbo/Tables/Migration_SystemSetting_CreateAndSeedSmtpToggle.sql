-- ============================================================
-- Migration : Create dbo.SystemSetting and seed SmtpEnabled
-- Purpose   : Introduces a general-purpose key-value system-settings
--             table, then inserts the SmtpEnabled toggle (ON by default).
-- Safe      : All steps guarded with IF NOT EXISTS — safe to re-run.
-- Date      : 2026-02-28
-- ============================================================

SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================
-- Step 1: Create the SystemSetting table
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM   sys.objects
    WHERE  object_id = OBJECT_ID(N'[dbo].[SystemSetting]')
      AND  type      = N'U'
)
BEGIN
    CREATE TABLE [dbo].[SystemSetting] (
        -- Primary key
        [SettingId]        INT            IDENTITY (1, 1) NOT NULL,

        -- Setting identity and value
        [SettingKey]       NVARCHAR (100) NOT NULL,
        [SettingValue]     NVARCHAR (500) NOT NULL,
        [Description]      NVARCHAR (500) NULL,

        -- Audit columns (consistent with Item, Request, Department)
        [DateCreated]      DATETIME2 (2)  CONSTRAINT [DF_SystemSetting_DateCreated]  DEFAULT (sysutcdatetime())                                         NOT NULL,
        [DateModified]     DATETIME2 (2)  CONSTRAINT [DF_SystemSetting_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
        [ModifiedByUserId] INT            NULL,

        CONSTRAINT [PK_SystemSetting]            PRIMARY KEY CLUSTERED ([SettingId] ASC),
        CONSTRAINT [UQ_SystemSetting_SettingKey] UNIQUE NONCLUSTERED ([SettingKey] ASC),
        CONSTRAINT [FK_SystemSetting_User]       FOREIGN KEY ([ModifiedByUserId]) REFERENCES [dbo].[User] ([UserId])
    );

    PRINT '[dbo].[SystemSetting] created.';
END
ELSE
    PRINT '[dbo].[SystemSetting] already exists — skipping creation.';
GO

-- ============================================================
-- Step 2: Create lookup index on SettingKey
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM   sys.indexes
    WHERE  object_id = OBJECT_ID(N'[dbo].[SystemSetting]')
      AND  name      = N'IX_SystemSetting_SettingKey'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SystemSetting_SettingKey]
        ON [dbo].[SystemSetting] ([SettingKey] ASC)
        INCLUDE ([SettingValue]);

    PRINT 'Index [IX_SystemSetting_SettingKey] created.';
END
ELSE
    PRINT 'Index [IX_SystemSetting_SettingKey] already exists — skipping creation.';
GO

-- ============================================================
-- Step 3: Seed the SmtpEnabled setting
--
-- Value semantics:
--   '1'  =  SMTP dispatch ENABLED  (system sends emails normally)
--   '0'  =  SMTP dispatch DISABLED (all outgoing emails suppressed;
--            email log entries are still written)
--
-- Default: '1' (ON) — preserves existing behaviour on first deploy.
-- ============================================================
IF NOT EXISTS (
    SELECT 1
    FROM   [dbo].[SystemSetting]
    WHERE  [SettingKey] = N'SmtpEnabled'
)
BEGIN
    INSERT INTO [dbo].[SystemSetting] ([SettingKey], [SettingValue], [Description])
    VALUES (
        N'SmtpEnabled',
        N'1',
        N'Global SMTP send toggle. ''1'' = outgoing email enabled; ''0'' = all SMTP dispatch suppressed system-wide. Disabling does not remove email log entries.'
    );

    PRINT 'Setting [SmtpEnabled] seeded with value ''1'' (ON).';
END
ELSE
    PRINT 'Setting [SmtpEnabled] already exists — skipping seed.';
GO
