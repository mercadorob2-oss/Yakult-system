-- Cleanup: merge the remaining HP 76X / CF276X Toner cluster into one model.
-- Keeping 67 "HP LASERJET 76X (BLACK)" per user's choice.

DECLARE @KeepModelId       INT           = 67;
DECLARE @RemoveModelIdsCsv NVARCHAR(200) = '38,42,71';

DECLARE @RemoveModelIds TABLE (ConsumableModelId INT);
INSERT INTO @RemoveModelIds (ConsumableModelId)
SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@RemoveModelIdsCsv, ',');

-- ── Preview ──────────────────────────────────────────────────────────────────
SELECT
    CASE WHEN cm.ConsumableModelId = @KeepModelId THEN '>>> KEEP <<<' ELSE 'remove' END AS Action,
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId = @KeepModelId
   OR cm.ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds)
ORDER BY Action DESC;

SELECT
    (SELECT ISNULL(SUM(i.StockOnHand), 0)
     FROM dbo.Item i
     WHERE i.Active = 1
       AND (i.ConsumableModelId = @KeepModelId OR i.ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds))
    ) AS CombinedStockAfterMerge;   -- should be 147

-- ── Uncomment below once the preview looks correct ──────────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.Item
SET ConsumableModelId = @KeepModelId
WHERE ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds);

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds);

COMMIT TRANSACTION;

PRINT 'Merged [38, 42, 71] into 67. Combined stock should now read 147 on the Consumable Models page.';
*/
GO
