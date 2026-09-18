-- Migration: Add Company/Branch context to dbo.RepairTicket's "Requested By" (Department mode).
-- Requesting a repair "for a Department" is ambiguous on its own — the same department name can
-- exist under multiple Companies/Branches (see dbo.BranchDepartmentCompany). This adds
-- RequestedByComId/RequestedByBranchId alongside the existing RequestedByDeptId so the technician
-- can pin down exactly which org unit is asking, via a Company -> Branch -> Department cascade.
-- Idempotent — safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'RequestedByComId'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD RequestedByComId INT NULL;
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT FK_RepairTicket_RequestedByCom FOREIGN KEY (RequestedByComId) REFERENCES dbo.Company (ComId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicket') AND name = 'RequestedByBranchId'
)
BEGIN
    ALTER TABLE dbo.RepairTicket ADD RequestedByBranchId INT NULL;
    ALTER TABLE dbo.RepairTicket
        ADD CONSTRAINT FK_RepairTicket_RequestedByBranch FOREIGN KEY (RequestedByBranchId) REFERENCES dbo.Branch (BranchId);
END
GO
