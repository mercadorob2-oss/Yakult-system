CREATE TABLE [dbo].[SystemEmailLog] (
    [LogId]        INT            IDENTITY (1, 1) NOT NULL,
    [TemplateKey]  NVARCHAR (100) NULL,
    [Recipients]   NVARCHAR (MAX) NULL,
    [Subject]      NVARCHAR (500) NULL,
    [Status]       NVARCHAR (50)  NOT NULL,
    [ErrorMessage] NVARCHAR (MAX) NULL,
    [ProfileId]    INT            NULL,
    [EntityType]   NVARCHAR (50)  NULL,
    [EntityId]     INT            NULL,
    [SentDate]     DATETIME2 (2)  CONSTRAINT [DF_SystemEmailLog_SentDate] DEFAULT (sysutcdatetime()) NOT NULL,
    [SentByUserId] INT            NULL,
    CONSTRAINT [PK_SystemEmailLog] PRIMARY KEY CLUSTERED ([LogId] ASC),
    CONSTRAINT [FK_SystemEmailLog_SystemSmtpProfile] FOREIGN KEY ([ProfileId]) REFERENCES [dbo].[SystemSmtpProfile] ([ProfileId]),
    CONSTRAINT [FK_SystemEmailLog_User] FOREIGN KEY ([SentByUserId]) REFERENCES [dbo].[User] ([UserId])
);


GO
CREATE NONCLUSTERED INDEX [IX_SystemEmailLog_SentDate]
    ON [dbo].[SystemEmailLog]([SentDate] DESC);


GO
CREATE NONCLUSTERED INDEX [IX_SystemEmailLog_Entity]
    ON [dbo].[SystemEmailLog]([EntityType] ASC, [EntityId] ASC)
    INCLUDE([SentDate], [Status]);


GO
CREATE NONCLUSTERED INDEX [IX_SystemEmailLog_Status]
    ON [dbo].[SystemEmailLog]([Status] ASC, [SentDate] DESC);

