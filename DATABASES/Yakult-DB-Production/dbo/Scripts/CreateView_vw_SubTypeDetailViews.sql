-- Deploys the four Invoice Sub-Type detail views (vw_Subscription, vw_Contract,
-- vw_License, vw_ServiceDetail) against an existing database. Run after
-- Migration_InvoiceGroup_CreateTable.sql, Migration_Set_AddInvoiceGroupAndSubType.sql,
-- and Migration_InvoiceSubTypeDetails_CreateTables.sql.
-- Idempotent: drops and recreates each view so re-running this script is always safe.

IF OBJECT_ID('dbo.vw_Subscription', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Subscription;
GO
CREATE VIEW dbo.vw_Subscription
AS
SELECT
    sub.SubscriptionId,
    sub.SubscriptionCode,
    sub.SetId,
    s.SetCode,
    s.DocumentNumber,
    s.Status,
    s.StartDate,
    s.EndDate,
    s.InvoiceGroupId,
    ig.InvoiceGroupNum
FROM dbo.Subscription sub
JOIN dbo.[Set] s          ON sub.SetId = s.SetId
LEFT JOIN dbo.InvoiceGroup ig ON s.InvoiceGroupId = ig.InvoiceGroupId;
GO

IF OBJECT_ID('dbo.vw_Contract', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Contract;
GO
CREATE VIEW dbo.vw_Contract
AS
SELECT
    c.ContractId,
    c.ContractCode,
    c.SetId,
    s.SetCode,
    s.DocumentNumber,
    s.Status,
    s.StartDate,
    s.EndDate,
    s.InvoiceGroupId,
    ig.InvoiceGroupNum
FROM dbo.Contract c
JOIN dbo.[Set] s          ON c.SetId = s.SetId
LEFT JOIN dbo.InvoiceGroup ig ON s.InvoiceGroupId = ig.InvoiceGroupId;
GO

IF OBJECT_ID('dbo.vw_License', 'V') IS NOT NULL
    DROP VIEW dbo.vw_License;
GO
CREATE VIEW dbo.vw_License
AS
SELECT
    l.LicenseId,
    l.LicenseCode,
    l.SetId,
    s.SetCode,
    s.DocumentNumber,
    s.Status,
    s.StartDate,
    s.EndDate,
    s.InvoiceGroupId,
    ig.InvoiceGroupNum
FROM dbo.License l
JOIN dbo.[Set] s          ON l.SetId = s.SetId
LEFT JOIN dbo.InvoiceGroup ig ON s.InvoiceGroupId = ig.InvoiceGroupId;
GO

IF OBJECT_ID('dbo.vw_ServiceDetail', 'V') IS NOT NULL
    DROP VIEW dbo.vw_ServiceDetail;
GO
CREATE VIEW dbo.vw_ServiceDetail
AS
SELECT
    sd.ServiceDetailId,
    sd.ServiceCode,
    sd.SetId,
    s.SetCode,
    s.DocumentNumber,
    s.Status,
    s.StartDate,
    s.EndDate,
    s.InvoiceGroupId,
    ig.InvoiceGroupNum
FROM dbo.ServiceDetail sd
JOIN dbo.[Set] s          ON sd.SetId = s.SetId
LEFT JOIN dbo.InvoiceGroup ig ON s.InvoiceGroupId = ig.InvoiceGroupId;
GO
