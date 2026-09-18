CREATE TABLE [dbo].[CallSmtpProfile] (
    [ProfileId]       INT             IDENTITY (1, 1) NOT NULL,
    [ProfileName]     NVARCHAR (150)  NOT NULL,
    [SmtpServer]      NVARCHAR (255)  NOT NULL,
    [SmtpPort]        INT             CONSTRAINT [DF_CallSmtpProfile_Port] DEFAULT ((587)) NOT NULL,
    [UseSsl]          BIT             CONSTRAINT [DF_CallSmtpProfile_UseSsl] DEFAULT ((1)) NOT NULL,
    [SmtpUsername]    NVARCHAR (255)  NULL,
    [SmtpPasswordEnc] VARBINARY (512) NULL,
    [FromName]        NVARCHAR (150)  NULL,
    [FromEmail]       NVARCHAR (255)  NULL,
    [IsActive]        BIT             CONSTRAINT [DF_CallSmtpProfile_IsActive] DEFAULT ((1)) NOT NULL,
    [UpdatedAt]       DATETIME2 (2)   CONSTRAINT [DF_CallSmtpProfile_UpdatedAt] DEFAULT (sysutcdatetime()) NOT NULL,
    [UpdatedByUserId] INT             NULL,
    CONSTRAINT [PK_CallSmtpProfile] PRIMARY KEY CLUSTERED ([ProfileId] ASC),
    CONSTRAINT [FK_CallSmtpProfile_User] FOREIGN KEY ([UpdatedByUserId]) REFERENCES [dbo].[User] ([UserId]),
    CONSTRAINT [UQ_CallSmtpProfile_ProfileName] UNIQUE NONCLUSTERED ([ProfileName] ASC)
);


GO
CREATE NONCLUSTERED INDEX [IX_CallSmtpProfile_IsActive]
    ON [dbo].[CallSmtpProfile]([IsActive] ASC)
    INCLUDE([ProfileName]);

