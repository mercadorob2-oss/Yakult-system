-- Migration: Add "Requested By" (Department or Employee) to dbo.RepairTicket.
-- This is distinct from SubmittedByEmpId/SubmittedByUserId (who physically brought/logged the
-- item) — RequestedBy represents who is asking for the repair to be done, which can be a whole
-- Department rather than a specific person.
-- Idempotent — safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'RequestedByType'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD RequestedByType NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RepairTicket_RequestedByType'
)
BEGIN
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT CK_RepairTicket_RequestedByType CHECK (RequestedByType IS NULL OR RequestedByType IN ('Department', 'Employee'));
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'RequestedByDeptId'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD RequestedByDeptId INT NULL;
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT FK_RepairTicket_RequestedByDept FOREIGN KEY (RequestedByDeptId) REFERENCES dbo.Department (DeptId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'RequestedByEmpId'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD RequestedByEmpId INT NULL;
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT FK_RepairTicket_RequestedByEmp FOREIGN KEY (RequestedByEmpId) REFERENCES dbo.Employee (EmpId);
END
GO
