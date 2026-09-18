-- Aggregated read surface for Sub-Type Groups on an invoice. dbo.SetItemSubTypeGroup is now
-- the authoritative one-row-per-group entity (GroupId/ReferenceCode/BeginDate/EndDate, plus
-- the user's persisted SubtotalOverride/VatPercent/WhtPercent/DiscountPercent); this view
-- joins through it and aggregates the raw computed Subtotal/ItemCount from the member
-- dbo.SetItem rows, mirroring the existing dbo.vw_Invoices / dbo.vw_InvoiceItems convention.
-- Callers (ViewInvoiceDetailPage) prefer SubtotalOverride/VatPercent/WhtPercent/
-- DiscountPercent when not NULL, falling back to the computed Subtotal / header defaults
-- otherwise — see Migration_SetItemSubTypeGroup_AddOverrides.sql.
-- Idempotent: drops and recreates so re-running this script is always safe.
-- Run after Migration_SetItemSubTypeGroup_CreateTable.sql, Migration_SetItem_AddGroupId.sql,
-- Migration_SetItemSubTypeGroup_Backfill.sql, and Migration_SetItemSubTypeGroup_AddOverrides.sql.

IF OBJECT_ID('dbo.vw_SetItemSubTypeGroups', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SetItemSubTypeGroups;
GO
CREATE VIEW dbo.vw_SetItemSubTypeGroups
AS
SELECT
    g.GroupId,
    g.SetId,
    s.DocumentNumber,
    g.SubType,
    g.ReferenceCode,
    g.BeginDate,
    g.EndDate,
    COUNT(*)         AS ItemCount,
    SUM(si.Amount)   AS Subtotal,
    g.SubtotalOverride,
    g.VatPercent,
    g.WhtPercent,
    g.DiscountPercent
FROM dbo.SetItemSubTypeGroup g
JOIN dbo.SetItem si ON si.GroupId = g.GroupId
JOIN dbo.[Set] s ON g.SetId = s.SetId
GROUP BY g.GroupId, g.SetId, s.DocumentNumber, g.SubType, g.ReferenceCode, g.BeginDate, g.EndDate,
         g.SubtotalOverride, g.VatPercent, g.WhtPercent, g.DiscountPercent;
GO
