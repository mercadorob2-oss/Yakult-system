-- Cleanup template: merge SEVERAL duplicate/near-duplicate ConsumableModel rows into one
-- Purpose : For clusters like the "HP 76X / CF276X / Laserjet / Lasejet" case where more
--           than two ConsumableModel rows represent the same physical product. Reassigns
--           every Item linked to any of the "remove" models onto the "keep" model, then
--           deactivates all the removed models in one transaction.
-- Usage   : Fill in @KeepModelId and the comma-separated @RemoveModelIdsCsv below, review
--           the preview output, then uncomment and run the block at the bottom.

DECLARE @KeepModelId       INT           = /* e.g. 42 */ NULL;
DECLARE @RemoveModelIdsCsv NVARCHAR(200) = /* e.g. '37,38,67,71' */ NULL;

IF @KeepModelId IS NULL OR @RemoveModelIdsCsv IS NULL
BEGIN
    PRINT 'Set @KeepModelId and @RemoveModelIdsCsv before running this script.';
    RETURN;
END

DECLARE @RemoveModelIds TABLE (ConsumableModelId INT);
INSERT INTO @RemoveModelIds (ConsumableModelId)
SELECT TRY_CAST(value AS INT) FROM STRING_SPLIT(@RemoveModelIdsCsv, ',') WHERE TRY_CAST(value AS INT) IS NOT NULL;

-- ── Preview: every model involved, plus the total stock the merge will produce ──
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
    ) AS CombinedStockAfterMerge;

-- ── Preview: items that will be reassigned ──────────────────────────────────
SELECT ItemId, Name, ModelNumber, Category, ConsumableModelId, StockOnHand, Active
FROM dbo.Item
WHERE ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds);

-- ── Uncomment below once the previews above look correct ────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.Item
SET ConsumableModelId = @KeepModelId
WHERE ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds);

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId IN (SELECT ConsumableModelId FROM @RemoveModelIds);

COMMIT TRANSACTION;

PRINT 'Merged models [' + @RemoveModelIdsCsv + '] into ' + CAST(@KeepModelId AS NVARCHAR(20)) + '.';
*/
GO
