-- Migration: Allow a borrow to be attributed to a department as a whole,
-- for cases like "IT borrowed this projector" where naming one employee doesn't fit.
-- Loosens BorrowLog.BorrowedByEmpId to nullable and adds a CHECK constraint requiring
-- at least one of BorrowedByEmpId / BorrowedByDeptId to be present.

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BorrowLog')
      AND name = 'BorrowedByEmpId' AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.BorrowLog ALTER COLUMN BorrowedByEmpId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_BorrowLog_RequesterPresent'
)
BEGIN
    ALTER TABLE dbo.BorrowLog ADD CONSTRAINT CK_BorrowLog_RequesterPresent
        CHECK (BorrowedByEmpId IS NOT NULL OR BorrowedByDeptId IS NOT NULL);
END
GO
