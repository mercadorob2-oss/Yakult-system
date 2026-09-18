/* =====================================================================================
   IT Call Monitoring - Branch SMTP + Notification Recipients
   - Adds per-branch SMTP sender mapping (Option B: reusable profiles) and per-branch
     recipient overrides for Call Monitoring email notifications.
   - Run in SSMS against your YIMS database.
   ===================================================================================== */

/* 1) Branch -> SMTP Profile mapping (Option B) */
IF OBJECT_ID('dbo.CallBranchSmtpProfileLink', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallBranchSmtpProfileLink
    (
        BranchId INT NOT NULL CONSTRAINT PK_CallBranchSmtpProfileLink PRIMARY KEY,
        ProfileId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallBranchSmtpLink_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallBranchSmtpLink_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_Profile')
BEGIN
    IF OBJECT_ID('dbo.CallSmtpProfile', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
        ADD CONSTRAINT FK_CallBranchSmtpLink_Profile FOREIGN KEY (ProfileId) REFERENCES dbo.CallSmtpProfile(ProfileId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_Branch')
BEGIN
    IF OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
        ADD CONSTRAINT FK_CallBranchSmtpLink_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch(BranchId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_User')
BEGIN
    IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
        ADD CONSTRAINT FK_CallBranchSmtpLink_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallBranchSmtpLink_ProfileId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallBranchSmtpLink_ProfileId ON dbo.CallBranchSmtpProfileLink (ProfileId);
END
GO

/* 2) Branch recipients override */
IF OBJECT_ID('dbo.CallBranchNotificationRecipient', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallBranchNotificationRecipient
    (
        BranchId INT NOT NULL CONSTRAINT PK_CallBranchNotificationRecipient PRIMARY KEY,
        RecipientEmails NVARCHAR(2000) NULL,
        EscalationEmails NVARCHAR(2000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallBranchNotif_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallBranchNotif_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchNotif_Branch')
BEGIN
    IF OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallBranchNotif_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch(BranchId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchNotif_User')
BEGIN
    IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallBranchNotif_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallBranchNotif_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallBranchNotif_IsActive ON dbo.CallBranchNotificationRecipient (IsActive) INCLUDE (BranchId);
END
GO

