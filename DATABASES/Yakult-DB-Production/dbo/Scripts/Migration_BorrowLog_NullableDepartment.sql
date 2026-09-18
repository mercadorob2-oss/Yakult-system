-- Migration: Make BorrowLog.BorrowedByDeptId and BorrowedByDeptName nullable
-- Allows employees without departments (e.g., executives, Japanese staff) to borrow/return items

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BorrowLog') AND name = 'BorrowedByDeptId' AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.BorrowLog ALTER COLUMN BorrowedByDeptId INT NULL;
END
GO

IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.BorrowLog') AND name = 'BorrowedByDeptName' AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.BorrowLog ALTER COLUMN BorrowedByDeptName NVARCHAR(200) NULL;
END
GO
