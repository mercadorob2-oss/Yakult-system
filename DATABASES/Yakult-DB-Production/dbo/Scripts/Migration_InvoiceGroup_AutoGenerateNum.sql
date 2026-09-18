-- Converts dbo.InvoiceGroup.InvoiceGroupNum from user-typed free text to an
-- auto-generated computed column, mirroring dbo.[Set].SetCode's pattern
-- (e.g. IG-0001, IG-0002, ...).
--
-- WARNING: this is a one-way, data-losing change for any InvoiceGroupNum values
-- already typed by a user — DROP COLUMN discards them, and every existing row's
-- code becomes its auto-generated IG-#### instead. Confirmed acceptable (dev/test
-- data only) before running this.
--
-- GO separators are required throughout (see Migration_Set_AddInvoiceGroupAndSubType.sql
-- for the same lesson: a batch can't see a column/index change made earlier in the
-- same batch).
IF NOT EXISTS (
    SELECT 1 FROM sys.computed_columns
    WHERE object_id = OBJECT_ID('dbo.InvoiceGroup') AND name = 'InvoiceGroupNum'
)
BEGIN
    DROP INDEX UQ_InvoiceGroup_Num ON dbo.InvoiceGroup;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.computed_columns
    WHERE object_id = OBJECT_ID('dbo.InvoiceGroup') AND name = 'InvoiceGroupNum'
)
BEGIN
    ALTER TABLE dbo.InvoiceGroup DROP COLUMN InvoiceGroupNum;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.computed_columns
    WHERE object_id = OBJECT_ID('dbo.InvoiceGroup') AND name = 'InvoiceGroupNum'
)
BEGIN
    ALTER TABLE dbo.InvoiceGroup ADD InvoiceGroupNum
        AS (concat('IG-', right('0000'+CONVERT([varchar](4),[InvoiceGroupId]),(4)))) PERSISTED NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'UQ_InvoiceGroup_Num' AND object_id = OBJECT_ID('dbo.InvoiceGroup')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_InvoiceGroup_Num
        ON dbo.InvoiceGroup (InvoiceGroupNum ASC);
END
GO
