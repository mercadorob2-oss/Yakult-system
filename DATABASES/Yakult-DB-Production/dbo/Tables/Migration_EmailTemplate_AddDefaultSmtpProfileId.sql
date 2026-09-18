-- Migration: Add DefaultSmtpProfileId to dbo.EmailTemplate
-- Allows each email template to specify a default SMTP sender profile.
-- The column is nullable; NULL means the caller must supply a profile at send time.

ALTER TABLE [dbo].[EmailTemplate]
    ADD [DefaultSmtpProfileId] INT NULL
        CONSTRAINT [FK_EmailTemplate_SmtpProfile] FOREIGN KEY REFERENCES [dbo].[SystemSmtpProfile] ([ProfileId]) ON DELETE SET NULL;
GO
