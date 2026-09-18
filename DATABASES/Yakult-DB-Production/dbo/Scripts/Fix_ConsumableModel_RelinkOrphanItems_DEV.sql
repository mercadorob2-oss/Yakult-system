-- Fix: Re-link orphaned consumable Item rows to their existing ConsumableModel.
--
-- Target  : Yakult_Inventory_System_DEV
-- Context : The Batch Add Request item picker collapses Ink/Toner/Print Head items
--           to one entry per ConsumableModelId. These 3 rows were created in one
--           batch on 2026-08-24 with ConsumableModelId = NULL, so they showed as
--           duplicate picks. All have StockOnHand = 0 and no dbo.Request references,
--           so re-linking changes nothing except the picker.
-- Safe    : idempotent (guarded by ConsumableModelId IS NULL + name match),
--           wrapped in a transaction, auto-rolls back if row counts look wrong.
--           The trg_Item_ConsumableModel_SyncTotalQuantity trigger refreshes
--           ConsumableModel.TotalQuantity automatically (stays 0 here).

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

UPDATE dbo.Item SET ConsumableModelId = 159
WHERE ItemId = 3268 AND ConsumableModelId IS NULL AND Name = 'EPSON 057 (YELLOW)';

UPDATE dbo.Item SET ConsumableModelId = 163
WHERE ItemId = 3272 AND ConsumableModelId IS NULL AND Name = 'EPSON 664 (YELLOW)';

UPDATE dbo.Item SET ConsumableModelId = 193
WHERE ItemId = 3302 AND ConsumableModelId IS NULL AND Name = 'HP LASERJET W2041XC (CYAN)';

-- Verify: no linkable orphans left in any consumable category
DECLARE @remaining int = (
    SELECT COUNT(*)
    FROM dbo.Item i
    WHERE i.Active = 1
      AND i.ConsumableModelId IS NULL
      AND (REPLACE(LOWER(i.Category),' ','') LIKE '%ink%'
        OR REPLACE(LOWER(i.Category),' ','') LIKE '%toner%'
        OR REPLACE(LOWER(i.Category),' ','') LIKE '%printhead%')
      AND EXISTS (SELECT 1 FROM dbo.Item j
                  WHERE j.Name = i.Name AND j.CategoryId = i.CategoryId
                    AND j.Active = 1 AND j.ItemId <> i.ItemId)
      AND EXISTS (SELECT 1 FROM dbo.Item k
                  WHERE k.Name = i.Name AND k.CategoryId = i.CategoryId
                    AND k.Active = 1 AND k.ConsumableModelId IS NOT NULL)
);
PRINT 'Remaining linkable orphans after fix: ' + CAST(@remaining AS varchar(10));

IF @remaining = 0
BEGIN
    COMMIT;
    PRINT 'Committed.';
END
ELSE
BEGIN
    ROLLBACK;
    PRINT 'Unexpected state - rolled back, no changes made.';
END

-- Post-check
SELECT i.ItemId, i.Name, i.Category, i.ConsumableModelId AS CMId,
       i.StockOnHand AS Stk, cm.TotalQuantity AS ModelTotalQuantity
FROM dbo.Item i
LEFT JOIN dbo.ConsumableModel cm ON cm.ConsumableModelId = i.ConsumableModelId
WHERE i.ItemId IN (3268, 3272, 3302)
ORDER BY i.ItemId;
