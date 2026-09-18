-- Diagnostic: Consumable Item rows that share a name with a linked ConsumableModel
--             but carry no ConsumableModelId of their own ("orphans").
--
-- Context : The Batch Add Request item picker (and stock aggregation) collapses
--           Ink / Toner / Print Head items to one entry per ConsumableModelId.
--           Item rows left with ConsumableModelId = NULL escape that collapse and
--           show up as separate near-duplicate picks. This script lists, per model,
--           every Item row involved and how much StockOnHand sits on each so the
--           orphans can be re-linked (or retired) with full visibility.
--
-- Read-only. No changes are made.

SET NOCOUNT ON;

------------------------------------------------------------------------
-- 1. Find the affected groups: an Item Name+Category that has at least one
--    linked row and at least one orphan row, within a consumable category.
------------------------------------------------------------------------
IF OBJECT_ID('tempdb..#Groups') IS NOT NULL DROP TABLE #Groups;

SELECT
    i.Name,
    i.CategoryId,
    i.Category,
    COUNT(*)                                                            AS TotalRows,
    COUNT(i.ConsumableModelId)                                          AS LinkedRows,
    SUM(CASE WHEN i.ConsumableModelId IS NULL THEN 1 ELSE 0 END)        AS OrphanRows,
    COUNT(DISTINCT i.ConsumableModelId)                                 AS DistinctModelIds,
    MIN(i.ConsumableModelId)                                            AS ModelId,
    SUM(i.StockOnHand)                                                  AS TotalStock,
    SUM(CASE WHEN i.ConsumableModelId IS NULL THEN i.StockOnHand ELSE 0 END) AS OrphanStock,
    SUM(CASE WHEN i.ConsumableModelId IS NOT NULL THEN i.StockOnHand ELSE 0 END) AS LinkedStock
INTO #Groups
FROM dbo.Item i
WHERE i.Active = 1
  AND (
        REPLACE(LOWER(i.Category), ' ', '') LIKE '%ink%'
     OR REPLACE(LOWER(i.Category), ' ', '') LIKE '%toner%'
     OR REPLACE(LOWER(i.Category), ' ', '') LIKE '%printhead%'
      )
GROUP BY i.Name, i.CategoryId, i.Category
HAVING COUNT(*) > 1
   AND SUM(CASE WHEN i.ConsumableModelId IS NULL THEN 1 ELSE 0 END) > 0
   AND COUNT(i.ConsumableModelId) > 0;   -- at least one linked row to re-link the orphans to

------------------------------------------------------------------------
-- 2. Per-group summary + the target ConsumableModel row
------------------------------------------------------------------------
SELECT
    g.Name,
    g.Category,
    g.TotalRows,
    g.LinkedRows,
    g.OrphanRows,
    g.DistinctModelIds,     -- >1 means orphans link to several models: merge models first, do not blind re-link
    g.ModelId,
    cm.ModelNumber          AS ModelNumber,
    cm.Category             AS ModelCategory,
    cm.IsActive             AS ModelIsActive,
    cm.IsRequestable        AS ModelIsRequestable,
    cm.TotalQuantity        AS ModelTotalQuantity,
    g.LinkedStock,
    g.OrphanStock,
    g.TotalStock,
    CASE WHEN g.TotalStock = 0 THEN 'NO STOCK ANYWHERE - safe to merge/retire'
         WHEN g.OrphanStock = 0 THEN 'orphans empty - just re-link'
         ELSE 'ORPHANS HOLD STOCK - re-link to preserve it'
    END                     AS Assessment
FROM #Groups g
LEFT JOIN dbo.ConsumableModel cm ON cm.ConsumableModelId = g.ModelId
ORDER BY g.OrphanStock DESC, g.OrphanRows DESC;

------------------------------------------------------------------------
-- 3. Row-by-row: every Item involved, orphan vs linked, with its stock
------------------------------------------------------------------------
SELECT
    i.Name,
    i.Category,
    i.ItemId,
    i.ConsumableModelId,
    CASE WHEN i.ConsumableModelId IS NULL THEN 'ORPHAN' ELSE 'linked' END AS LinkStatus,
    i.StockOnHand,
    (SELECT COUNT(*) FROM dbo.Request r WHERE r.ItemId = i.ItemId)        AS RequestRefs
FROM dbo.Item i
JOIN #Groups g
  ON g.Name = i.Name AND g.CategoryId = i.CategoryId
WHERE i.Active = 1
ORDER BY i.Name, LinkStatus DESC, i.StockOnHand DESC, i.ItemId;

------------------------------------------------------------------------
-- 4. Ready-made re-link statement (review section 3 first, then run this)
------------------------------------------------------------------------
--  UPDATE i
--  SET i.ConsumableModelId = g.ModelId
--  FROM dbo.Item i
--  JOIN #Groups g ON g.Name = i.Name AND g.CategoryId = i.CategoryId
--  WHERE i.Active = 1
--    AND i.ConsumableModelId IS NULL;
--  -- the trg_Item_ConsumableModel_SyncTotalQuantity trigger will refresh
--  -- ConsumableModel.TotalQuantity automatically.

IF OBJECT_ID('tempdb..#Groups') IS NOT NULL DROP TABLE #Groups;
