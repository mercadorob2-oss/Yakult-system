IF OBJECT_ID('dbo.CallAssignmentEligibility', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallAssignmentEligibility
    (
        EmpId INT NOT NULL CONSTRAINT PK_CallAssignmentEligibility PRIMARY KEY,
        IsAssignmentEligible BIT NOT NULL CONSTRAINT DF_CallAssignmentEligibility_Assignment DEFAULT ((1)),
        IsEscalationEligible BIT NOT NULL CONSTRAINT DF_CallAssignmentEligibility_Escalation DEFAULT ((1)),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallAssignmentEligibility_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAssignmentEligibility_Emp')
BEGIN
    ALTER TABLE dbo.CallAssignmentEligibility WITH CHECK
    ADD CONSTRAINT FK_CallAssignmentEligibility_Emp FOREIGN KEY (EmpId) REFERENCES dbo.Employee(EmpId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAssignmentEligibility_User')
BEGIN
    ALTER TABLE dbo.CallAssignmentEligibility WITH CHECK
    ADD CONSTRAINT FK_CallAssignmentEligibility_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO
