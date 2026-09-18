CREATE TABLE [dbo].[SystemSmtpProfile] (
    [ProfileId]       INT             IDENTITY (1, 1) NOT NULL,
    [ProfileName]     NVARCHAR (150)  NOT NULL,
    [SmtpServer]      NVARCHAR (255)  NOT NULL,
    [SmtpPort]        INT             CONSTRAINT [DF_SystemSmtpProfile_Port] DEFAULT ((587)) NOT NULL,
    [UseSsl]          BIT             CONSTRAINT [DF_SystemSmtpProfile_UseSsl] DEFAULT ((1)) NOT NULL,
    [SmtpUsername]    NVARCHAR (255)  NULL,
    [SmtpPasswordEnc] VARBINARY (512) NULL,
    [FromEmailId]     INT             NOT NULL,
    [IsActive]        BIT             CONSTRAINT [DF_SystemSmtpProfile_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]       DATETIME2 (2)   CONSTRAINT [DF_SystemSmtpProfile_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT             NULL,
    CONSTRAINT [PK_SystemSmtpProfile] PRIMARY KEY CLUSTERED ([ProfileId] ASC),
    CONSTRAINT [FK_SystemSmtpProfile_Email] FOREIGN KEY ([FromEmailId]) REFERENCES [dbo].[EmailAddress] ([EmailId]),
    CONSTRAINT [FK_SystemSmtpProfile_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_SystemSmtpProfile_ProfileName] UNIQUE NONCLUSTERED ([ProfileName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_SystemSmtpProfile_IsActive]
    ON [dbo].[SystemSmtpProfile]([IsActive] ASC)
    INCLUDE([ProfileId], [ProfileName], [SmtpServer], [SmtpPort], [UseSsl], [FromEmailId]);


GO
CREATE NONCLUSTERED INDEX [IX_SystemSmtpProfile_ProfileName]
    ON [dbo].[SystemSmtpProfile]([ProfileName] ASC)
    INCLUDE([IsActive]);

