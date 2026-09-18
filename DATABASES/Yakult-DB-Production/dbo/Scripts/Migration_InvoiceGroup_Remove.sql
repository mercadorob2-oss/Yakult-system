-- Removes the Invoice Group feature entirely. It bundled several separate invoices
-- (separate dbo.[Set] rows / Document Numbers) under one parent group, but every actual
-- use case turned out to be several sub-type sections *within one invoice* (one Document
-- Number) — which the invoice itself already is the natural container for. There is no
-- remaining scenario where two different Document Numbers need to be bundled together.
--
-- Run this AFTER updating/redeploying vw_Invoices and vw_InvoiceItems to stop selecting
-- InvoiceGroupNum (see AlterView_vw_Invoices_RemoveInvoiceGroup.sql and
-- AlterView_vw_InvoiceItems_RemoveInvoiceGroup.sql) — both views join dbo.InvoiceGroup and
-- would break once it's dropped.

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Set_InvoiceGroup')
BEGIN
    ALTER TABLE dbo.[Set] DROP CONSTRAINT FK_Set_InvoiceGroup;
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Set_InvoiceGroupId' AND object_id = OBJECT_ID('dbo.[Set]'))
BEGIN
    DROP INDEX IX_Set_InvoiceGroupId ON dbo.[Set];
END
GO

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceGroupId')
BEGIN
    ALTER TABLE dbo.[Set] DROP COLUMN InvoiceGroupId;
END
GO

IF OBJECT_ID('dbo.InvoiceGroup', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.InvoiceGroup;
END
GO
