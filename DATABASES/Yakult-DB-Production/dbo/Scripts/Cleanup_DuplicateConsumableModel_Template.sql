-- Cleanup template: merge a duplicate/typo dbo.ConsumableModel into the real one
-- Purpose : Run Diagnostic_DuplicateConsumableModel.sql first to find the ConsumableModelId
--           of the model to KEEP (correct spelling) and the one to REMOVE (the typo).
--           This script:
--             1) Reassigns every Item currently linked to the duplicate model over to
--                the kept model, so their stock is pooled correctly under one model
--             2) Deactivates the duplicate model (soft delete — IsActive = 0), it is NOT
--                physically deleted so nothing else referencing it breaks
-- Usage   : Fill in @KeepModelId and @RemoveModelId below, review the preview output,
--           then uncomment and run the UPDATE statements at the bottom.

DECLARE @KeepModelId   INT = /* the correctly-spelled model's ConsumableModelId, e.g. */ NULL;
DECLARE @RemoveModelId INT = /* the typo model's ConsumableModelId, e.g. */ NULL;

IF @KeepModelId IS NULL OR @RemoveModelId IS NULL
BEGIN
    PRINT 'Set @KeepModelId and @RemoveModelId before running this script.';
    RETURN;
END

-- ── Preview: the two models being merged ────────────────────────────────────
SELECT ConsumableModelId, ModelNumber, Category, IsActive,
       (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
       (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE ConsumableModelId IN (@KeepModelId, @RemoveModelId);

-- ── Preview: items that will be reassigned ──────────────────────────────────
SELECT ItemId, Name, Category, StockOnHand, Active
FROM dbo.Item
WHERE ConsumableModelId = @RemoveModelId;

-- ── Uncomment below once the previews above look correct ────────────────────

/*
BEGIN TRANSACTION;

-- 1) Reassign every item off the duplicate model onto the kept model
UPDATE dbo.Item
SET ConsumableModelId = @KeepModelId
WHERE ConsumableModelId = @RemoveModelId;

-- 2) Deactivate the duplicate model (soft delete, not physically removed)
UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId = @RemoveModelId;

COMMIT TRANSACTION;

PRINT 'Merged ConsumableModel ' + CAST(@RemoveModelId AS NVARCHAR(20))
    + ' into ' + CAST(@KeepModelId AS NVARCHAR(20)) + '.';
*/
GO
