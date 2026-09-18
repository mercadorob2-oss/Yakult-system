/* =====================================================================================
   Auto-escalation assignee (App-driven auto escalation)
   - Allows choosing a single Supervisor employee that auto-escalated tickets will be assigned to.
   ===================================================================================== */

IF OBJECT_ID('dbo.CallAutoEscalationAssigneeSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallAutoEscalationAssigneeSettings
    (
        SettingsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallAutoEscalationAssigneeSettings PRIMARY KEY,
        AssigneeEmpId INT NULL,
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallAutoEscAssignee_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAutoEscalationAssigneeSettings_Emp')
BEGIN
    ALTER TABLE dbo.CallAutoEscalationAssigneeSettings WITH CHECK
    ADD CONSTRAINT FK_CallAutoEscalationAssigneeSettings_Emp FOREIGN KEY (AssigneeEmpId) REFERENCES dbo.Employee(EmpId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAutoEscalationAssigneeSettings_User')
BEGIN
    ALTER TABLE dbo.CallAutoEscalationAssigneeSettings WITH CHECK
    ADD CONSTRAINT FK_CallAutoEscalationAssigneeSettings_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

