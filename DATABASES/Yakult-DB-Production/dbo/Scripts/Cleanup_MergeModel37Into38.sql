-- Cleanup: merge ConsumableModelId 37 "HP 76X Black Lasejet Toner Cartridge" (typo)
-- into 38 "HP 76X Black Laserjet Toner Cartridge" (correct spelling).

-- ── Preview ──────────────────────────────────────────────────────────────────
SELECT
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId IN (37, 38);

SELECT ItemId, Name, ModelNumber, Category, ConsumableModelId, StockOnHand, Active
FROM dbo.Item
WHERE ConsumableModelId IN (37, 38);

-- ── Uncomment below once the preview looks correct ──────────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.Item
SET ConsumableModelId = 38
WHERE ConsumableModelId = 37;

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId = 37;

COMMIT TRANSACTION;

PRINT 'Merged 37 into 38.';
*/
GO
