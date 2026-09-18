-- Reverts Migration_InvoiceSubTypeDetail_AllowMultiple.sql. On reflection, one invoice
-- (identified by its single Document Number / dbo.[Set] row) is the natural boundary —
-- there's no need for several distinct Contract/Subscription/License/Services "entries"
-- with their own reference code and date range within one invoice. Each invoice keeps
-- exactly one sub-type detail row (dbo.[Set].InvoiceSubType + the matching Contract/
-- Subscription/License/ServiceDetail row), same as before that change. Item-level
-- categorization is now a simple nullable dbo.SetItem.SubType tag instead (see
-- Migration_SetItem_AddSubType.sql) — no separate entry to link items to.
--
-- Idempotent and safe to run whether or not Migration_InvoiceSubTypeDetail_AllowMultiple.sql
-- was ever applied.

-- ── 1. Drop the item -> entry link columns (superseded by dbo.SetItem.SubType) ──────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'SubTypeKind')
BEGIN
    ALTER TABLE dbo.SetItem DROP COLUMN SubTypeKind;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.SetItem') AND name = 'SubTypeDetailId')
BEGIN
    ALTER TABLE dbo.SetItem DROP COLUMN SubTypeDetailId;
END
GO

-- ── 2. Drop the primary-entry pointer on dbo.[Set] ──────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'PrimarySubTypeKind')
BEGIN
    ALTER TABLE dbo.[Set] DROP COLUMN PrimarySubTypeKind;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'PrimarySubTypeDetailId')
BEGIN
    ALTER TABLE dbo.[Set] DROP COLUMN PrimarySubTypeDetailId;
END
GO

-- ── 3. Collapse each detail table back to one row per SetId (keep the earliest row,
--        matching InvoiceSubTypeDetailWriter's original "one entry" behavior) before the
--        unique constraint can be restored ──────────────────────────────────────────
DELETE c FROM dbo.Contract c
WHERE c.ContractId NOT IN (SELECT MIN(ContractId) FROM dbo.Contract GROUP BY SetId);
GO
DELETE s FROM dbo.Subscription s
WHERE s.SubscriptionId NOT IN (SELECT MIN(SubscriptionId) FROM dbo.Subscription GROUP BY SetId);
GO
DELETE l FROM dbo.License l
WHERE l.LicenseId NOT IN (SELECT MIN(LicenseId) FROM dbo.License GROUP BY SetId);
GO
DELETE sd FROM dbo.ServiceDetail sd
WHERE sd.ServiceDetailId NOT IN (SELECT MIN(ServiceDetailId) FROM dbo.ServiceDetail GROUP BY SetId);
GO

-- ── 4. Drop the per-entry Begin/End Date columns ────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contract') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.Contract DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contract') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.Contract DROP COLUMN EndDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN EndDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.License') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.License DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.License') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.License DROP COLUMN EndDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceDetail') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.ServiceDetail DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceDetail') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.ServiceDetail DROP COLUMN EndDate;
END
GO

-- ── 5. Restore the one-entry-per-invoice unique constraints ────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Contract_SetId')
BEGIN
    ALTER TABLE dbo.Contract ADD CONSTRAINT UQ_Contract_SetId UNIQUE (SetId);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Subscription_SetId')
BEGIN
    ALTER TABLE dbo.Subscription ADD CONSTRAINT UQ_Subscription_SetId UNIQUE (SetId);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_License_SetId')
BEGIN
    ALTER TABLE dbo.License ADD CONSTRAINT UQ_License_SetId UNIQUE (SetId);
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ServiceDetail_SetId')
BEGIN
    ALTER TABLE dbo.ServiceDetail ADD CONSTRAINT UQ_ServiceDetail_SetId UNIQUE (SetId);
END
GO
