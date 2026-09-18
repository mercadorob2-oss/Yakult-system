CREATE TABLE [dbo].[EmailTemplate] (
    [TemplateId]           INT            IDENTITY (1, 1) NOT NULL,
    [TemplateKey]          NVARCHAR (100) NOT NULL,
    [SubjectTemplate]      NVARCHAR (255) NOT NULL,
    [BodyTemplate]         NVARCHAR (MAX) NOT NULL,
    [IsHtml]               BIT            CONSTRAINT [DF_EmailTemplate_IsHtml] DEFAULT ((1)) NOT NULL,
    [IsActive]             BIT            CONSTRAINT [DF_EmailTemplate_IsActive] DEFAULT ((1)) NOT NULL,
    [DateCreated]          DATETIME2 (2)  CONSTRAINT [DF_EmailTemplate_DateCreated] DEFAULT (sysutcdatetime()) NOT NULL,
    [DefaultSmtpProfileId] INT            NULL,
    CONSTRAINT [PK_EmailTemplate] PRIMARY KEY CLUSTERED ([TemplateId] ASC),
    CONSTRAINT [FK_EmailTemplate_SmtpProfile] FOREIGN KEY ([DefaultSmtpProfileId]) REFERENCES [dbo].[SystemSmtpProfile] ([ProfileId]) ON DELETE SET NULL,
    CONSTRAINT [UQ_EmailTemplate_Key] UNIQUE NONCLUSTERED ([TemplateKey] ASC)
);




GO
CREATE NONCLUSTERED INDEX [IX_EmailTemplate_TemplateKey]
    ON [dbo].[EmailTemplate]([TemplateKey] ASC)
    INCLUDE([SubjectTemplate], [IsHtml], [IsActive]);


GO
CREATE NONCLUSTERED INDEX [IX_EmailTemplate_IsActive]
    ON [dbo].[EmailTemplate]([IsActive] ASC)
    INCLUDE([TemplateKey]);

