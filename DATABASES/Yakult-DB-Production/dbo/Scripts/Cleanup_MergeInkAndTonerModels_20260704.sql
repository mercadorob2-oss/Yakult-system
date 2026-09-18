-- Cleanup: merge the Ink Bottle (22/23) and Toner (24/25) duplicate ConsumableModel pairs
-- Purpose : Both pairs came from the same backfill run (exact-Name grouping), splitting
--           what's really one product each into two models. See conversation for the
--           naming-convention reasoning behind which side of each pair to keep.
-- Usage   : Review the preview output first. If the recommended Keep/Remove choice below
--           doesn't match what you want, just swap the IDs before uncommenting.

-- ── Preview: both pairs side by side ────────────────────────────────────────
SELECT
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId IN (22, 23, 24, 25)
ORDER BY cm.ConsumableModelId;

SELECT ItemId, Name, ModelNumber, Category, ConsumableModelId, StockOnHand, Active
FROM dbo.Item
WHERE ConsumableModelId IN (22, 23, 24, 25)
ORDER BY ConsumableModelId;

-- ── Uncomment below once the previews above look correct ────────────────────

/*
BEGIN TRANSACTION;

-- Ink Bottle: keep 22, fold in 23. Also fix the typo in 22's own ModelNumber
-- ("1VV222AA" -> "1VV22AA") so it matches its own correct parenthetical suffix.
UPDATE dbo.Item
SET ConsumableModelId = 22
WHERE ConsumableModelId = 23;

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId = 23;

UPDATE dbo.ConsumableModel
SET ModelNumber = 'HP 1VV22AA GT53 Black Original Ink Bottle (HP1VV22AA)'
WHERE ConsumableModelId = 22;

-- Toner: keep 25, fold in 24.
UPDATE dbo.Item
SET ConsumableModelId = 25
WHERE ConsumableModelId = 24;

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId = 24;

COMMIT TRANSACTION;

PRINT 'Merged 23 into 22 (typo corrected) and 24 into 25.';
*/
GO
