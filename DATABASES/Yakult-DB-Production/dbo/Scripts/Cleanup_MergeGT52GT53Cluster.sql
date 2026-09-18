-- Cleanup: rename the ambiguous "GT52/53" model to GT52 (per user confirmation it's
-- purely GT52 black stock, not a GT52/GT53 mix), then merge the GT52 color duplicates
-- and the GT53 Black duplicate.
--
-- Renaming: 46 "HP GT52/53 (BLACK)" -> "HP GT52 (BLACK)"
-- Merging:  81 (Cyan, M0H5AA)    -> 43 (Cyan)
--           79 (Magenta, M0H55AA) -> 44 (Magenta)
--           80 (Yellow, M0H55AA)  -> 45 (Yellow)
--           47 (GT53 Black, no part#) -> 22 (GT53 Black, HP1VV22AA)
-- Untouched: 46 (GT52 Black, renamed, no duplicate found) and 78 (Tri-Color Printhead,
--            different category, no duplicate found).

-- ── Preview ──────────────────────────────────────────────────────────────────
SELECT
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId IN (22, 43, 44, 45, 46, 47, 78, 79, 80, 81)
ORDER BY cm.ConsumableModelId;

-- Expected combined stock after merge:
--   22 (GT53 Black) = 100 + 1  = 101
--   43 (GT52 Cyan)    = 10 + 40  = 50
--   44 (GT52 Magenta) = 18 + 40  = 58
--   45 (GT52 Yellow)  = 15 + 40  = 55
--   46 (GT52 Black)   = 65        (unchanged, no duplicate)
--   78 (Printhead)    = 1         (unchanged, no duplicate)

-- ── Uncomment below once the preview looks correct ──────────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.ConsumableModel
SET ModelNumber = 'HP GT52 (BLACK)'
WHERE ConsumableModelId = 46;

UPDATE dbo.Item SET ConsumableModelId = 43 WHERE ConsumableModelId = 81; -- Cyan
UPDATE dbo.Item SET ConsumableModelId = 44 WHERE ConsumableModelId = 79; -- Magenta
UPDATE dbo.Item SET ConsumableModelId = 45 WHERE ConsumableModelId = 80; -- Yellow
UPDATE dbo.Item SET ConsumableModelId = 22 WHERE ConsumableModelId = 47; -- GT53 Black

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId IN (81, 79, 80, 47);

COMMIT TRANSACTION;

PRINT 'Renamed 46 to GT52 (BLACK). Merged 81->43, 79->44, 80->45, 47->22.';
*/
GO
