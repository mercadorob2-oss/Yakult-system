CREATE TABLE [dbo].[SystemSetting] (
    [SettingId]        INT            IDENTITY (1, 1) NOT NULL,
    [SettingKey]       NVARCHAR (100) NOT NULL,
    [SettingValue]     NVARCHAR (500) NOT NULL,
    [Description]      NVARCHAR (500) NULL,
    [DateCreated]      DATETIME2 (2)  CONSTRAINT [DF_SystemSetting_DateCreated]  DEFAULT (sysutcdatetime()) NOT NULL,
    [DateModified]     DATETIME2 (2)  CONSTRAINT [DF_SystemSetting_DateModified] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [ModifiedByUserId] INT            NULL,
    CONSTRAINT [PK_SystemSetting]            PRIMARY KEY CLUSTERED ([SettingId] ASC),
    CONSTRAINT [UQ_SystemSetting_SettingKey] UNIQUE NONCLUSTERED ([SettingKey] ASC),
    CONSTRAINT [FK_SystemSetting_User]       FOREIGN KEY ([ModifiedByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_SystemSetting_SettingKey]
    ON [dbo].[SystemSetting] ([SettingKey] ASC)
    INCLUDE ([SettingValue]);


GO
