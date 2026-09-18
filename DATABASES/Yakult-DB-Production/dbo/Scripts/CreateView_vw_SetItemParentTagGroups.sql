-- Aggregated read surface for Parent Tag Groups on an invoice. Mirrors the convention of
-- dbo.vw_SetItemSubTypeGroups, but Parent Tag has no per-group override columns (VAT/WHT/
-- Discount/Subtotal) -- it's a read-only display/rollup grouping only.
-- Idempotent: drops and recreates so re-running this script is always safe.
-- Run after Migration_SetItemParentTagGroup_CreateTable.sql and Migration_SetItem_AddParentTagGroupId.sql.

IF OBJECT_ID('dbo.vw_SetItemParentTagGroups', 'V') IS NOT NULL
    DROP VIEW dbo.vw_SetItemParentTagGroups;
GO
CREATE VIEW dbo.vw_SetItemParentTagGroups
AS
SELECT
    g.ParentTagGroupId,
    g.SetId,
    s.DocumentNumber,
    g.Label,
    COUNT(*)       AS ItemCount,
    SUM(si.Amount) AS Subtotal
FROM dbo.SetItemParentTagGroup g
JOIN dbo.SetItem si ON si.ParentTagGroupId = g.ParentTagGroupId
JOIN dbo.[Set] s ON g.SetId = s.SetId
GROUP BY g.ParentTagGroupId, g.SetId, s.DocumentNumber, g.Label;
GO
