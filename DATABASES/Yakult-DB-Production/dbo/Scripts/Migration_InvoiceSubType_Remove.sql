-- Retires the invoice-level "Sub-Type" concept entirely — it was added by mistake
-- (Migration_InvoiceSubTypeDetails_CreateTables.sql / Migration_Set_AddInvoiceGroupAndSubType.sql).
-- Sub-Type is correctly a per-item/per-group attribute only (dbo.Item.SubType,
-- dbo.SetItem.SubType/ReferenceCode/BeginDate/EndDate, dbo.InvoicePreparation) — an
-- invoice itself never had a meaningful single Sub-Type, especially now that one
-- invoice can combine items from several different Sub-Type Groups at once.
--
-- Drops: dbo.Contract/Subscription/License/ServiceDetail (one-row-per-invoice detail
-- tables) and their vw_Contract/vw_Subscription/vw_License/vw_ServiceDetail views,
-- plus dbo.[Set].InvoiceSubType and its check constraint.
--
-- Idempotent; safe to run regardless of whether the InvoiceSubType feature was ever
-- fully applied to this database.

-- ── 1. Drop the four detail-table read views, if present ───────────────────────────
IF OBJECT_ID('dbo.vw_Contract', 'V') IS NOT NULL DROP VIEW dbo.vw_Contract;
GO
IF OBJECT_ID('dbo.vw_Subscription', 'V') IS NOT NULL DROP VIEW dbo.vw_Subscription;
GO
IF OBJECT_ID('dbo.vw_License', 'V') IS NOT NULL DROP VIEW dbo.vw_License;
GO
IF OBJECT_ID('dbo.vw_ServiceDetail', 'V') IS NOT NULL DROP VIEW dbo.vw_ServiceDetail;
GO

-- ── 2. Drop the four detail tables ──────────────────────────────────────────────────
IF OBJECT_ID('dbo.Contract', 'U') IS NOT NULL DROP TABLE dbo.Contract;
GO
IF OBJECT_ID('dbo.Subscription', 'U') IS NOT NULL DROP TABLE dbo.Subscription;
GO
IF OBJECT_ID('dbo.License', 'U') IS NOT NULL DROP TABLE dbo.License;
GO
IF OBJECT_ID('dbo.ServiceDetail', 'U') IS NOT NULL DROP TABLE dbo.ServiceDetail;
GO

-- ── 3. Drop dbo.[Set].InvoiceSubType ─────────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Set_InvoiceSubType')
BEGIN
    ALTER TABLE dbo.[Set] DROP CONSTRAINT CK_Set_InvoiceSubType;
END
GO
-- vw_InvoiceItems/vw_Invoices reference dbo.[Set].InvoiceSubType, so their ALTER VIEW
-- (redeploying the checked-in definitions, already updated to drop that column) must
-- run before this DROP COLUMN — either via a normal SSDT publish, or by re-running
-- vw_InvoiceItems.sql/vw_Invoices.sql manually first.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceSubType')
BEGIN
    ALTER TABLE dbo.[Set] DROP COLUMN InvoiceSubType;
END
GO
