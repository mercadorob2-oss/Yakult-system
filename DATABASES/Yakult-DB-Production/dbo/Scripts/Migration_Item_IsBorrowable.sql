-- Migration: Add IsBorrowable to dbo.Item
-- Purpose : Explicit switch so items that wouldn't normally circulate (spare hardware, demo units)
--           can be marked available for temporary borrow. Default 0 keeps the existing catalog
--           out of the Borrow Items "select from list" picker until opted in.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Item') AND name = 'IsBorrowable'
)
BEGIN
    ALTER TABLE dbo.Item ADD [IsBorrowable] BIT DEFAULT ((0)) NULL;
    PRINT 'Column IsBorrowable added to dbo.Item.';
END
ELSE
BEGIN
    PRINT 'Column IsBorrowable already exists on dbo.Item. Skipping.';
END
GO
