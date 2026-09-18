-- Cleanup: merge EPSON 001 color-variant duplicates.
-- Keeping 1 (BLACK), 2 (BLUE), 3 (CYAN), 4 (MAGENTA), 5 (YELLOW), 8 (Light Magenta —
-- no duplicate, kept as-is since it's a genuinely distinct color from Magenta).
-- Folding in: 6 (Black Ink) -> 1, 7 (Cyan Ink) -> 3, 9 (Magenta Ink) -> 4,
--             10 (Yellow Ink) -> 5.

-- ── Preview ──────────────────────────────────────────────────────────────────
SELECT
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId IN (1, 2, 3, 4, 5, 6, 7, 8, 9, 10)
ORDER BY cm.ConsumableModelId;

-- Expected combined stock after merge: 1->10 (9+1), 3->16 (15+1), 4->16 (15+1), 5->18 (17+1)

-- ── Uncomment below once the preview looks correct ──────────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.Item SET ConsumableModelId = 1 WHERE ConsumableModelId = 6;
UPDATE dbo.Item SET ConsumableModelId = 3 WHERE ConsumableModelId = 7;
UPDATE dbo.Item SET ConsumableModelId = 4 WHERE ConsumableModelId = 9;
UPDATE dbo.Item SET ConsumableModelId = 5 WHERE ConsumableModelId = 10;

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId IN (6, 7, 9, 10);

COMMIT TRANSACTION;

PRINT 'Merged 6->1, 7->3, 9->4, 10->5. Models 2 and 8 left untouched (no duplicates).';
*/
GO
