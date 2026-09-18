-- Adds dbo.[Set].CurrentEmployeeId — a Set-level employee assignment that works even
-- when the Set has no dbo.Request rows (e.g. Renewal-created or Invoice-created Sets,
-- which write dbo.SetItem directly). Mirrors the existing CurrentBranchId/
-- CurrentDepartmentId columns already on dbo.[Set].
--
-- GO separators are required between each ALTER and the statements that reference the
-- new column (see Migration_Set_AddInvoiceGroupAndSubType.sql for the same lesson).
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'CurrentEmployeeId'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD CurrentEmployeeId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Set_CurrentEmployee'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT FK_Set_CurrentEmployee FOREIGN KEY (CurrentEmployeeId) REFERENCES dbo.Employee (EmpId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Set_CurrentEmployeeId' AND object_id = OBJECT_ID('dbo.[Set]')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Set_CurrentEmployeeId
        ON dbo.[Set] (CurrentEmployeeId ASC);
END
GO
