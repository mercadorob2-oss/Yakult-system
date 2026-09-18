CREATE TABLE [dbo].[CallEmailSettings] (
    [SettingsId]      INT             IDENTITY (1, 1) NOT NULL,
    [SmtpServer]      NVARCHAR (255)  NOT NULL,
    [SmtpPort]        INT             CONSTRAINT [DF_CallEmailSettings_Port] DEFAULT ((587)) NOT NULL,
    [UseSsl]          BIT             CONSTRAINT [DF_CallEmailSettings_UseSsl] DEFAULT ((1)) NOT NULL,
    [SmtpUsername]    NVARCHAR (255)  NULL,
    [SmtpPasswordEnc] VARBINARY (512) NULL,
    [FromName]        NVARCHAR (150)  NOT NULL,
    [FromEmail]       NVARCHAR (255)  NOT NULL,
    [UpdatedAt]       DATETIME2 (2)   CONSTRAINT [DF_CallEmailSettings_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT             NULL,
    CONSTRAINT [PK_CallEmailSettings] PRIMARY KEY CLUSTERED ([SettingsId] ASC),
    CONSTRAINT [FK_CallEmailSettings_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId])
);

