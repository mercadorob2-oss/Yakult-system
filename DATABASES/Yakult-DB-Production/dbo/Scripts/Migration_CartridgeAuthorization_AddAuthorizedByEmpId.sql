-- Adds AuthorizedByEmpId to CartridgeAuthorization for IT_MANUAL records.
-- IT-assisted approvers often have no system account, so we cannot reference
-- dbo.[User].UserId (SignedBySupervisorId FK). This column stores the EmpId
-- of the employee who verbally/offline authorized the request instead.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[CartridgeAuthorization]') AND name = 'AuthorizedByEmpId'
)
BEGIN
    ALTER TABLE dbo.[CartridgeAuthorization]
        ADD [AuthorizedByEmpId] INT NULL
        CONSTRAINT [FK_CartridgeAuthorization_AuthorizedByEmp]
            FOREIGN KEY REFERENCES dbo.[Employee]([EmpId]);
END
