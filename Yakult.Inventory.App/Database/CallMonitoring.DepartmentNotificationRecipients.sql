/* =====================================================================================
   IT Call Monitoring - Department Notification Recipients
   - Adds per-department recipient overrides for Call Monitoring email notifications.
   - Run in SSMS against your YIMS database.
   ===================================================================================== */

IF OBJECT_ID('dbo.CallDepartmentNotificationRecipient', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallDepartmentNotificationRecipient
    (
        DeptId INT NOT NULL CONSTRAINT PK_CallDepartmentNotificationRecipient PRIMARY KEY,
        RecipientEmails NVARCHAR(2000) NULL,
        EscalationEmails NVARCHAR(2000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallDeptNotif_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallDeptNotif_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptNotif_Department')
BEGIN
    IF OBJECT_ID('dbo.Department', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallDepartmentNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallDeptNotif_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department(DeptId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptNotif_User')
BEGIN
    IF OBJECT_ID('dbo.[User]', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallDepartmentNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallDeptNotif_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallDeptNotif_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallDeptNotif_IsActive ON dbo.CallDepartmentNotificationRecipient (IsActive) INCLUDE (DeptId);
END
GO

