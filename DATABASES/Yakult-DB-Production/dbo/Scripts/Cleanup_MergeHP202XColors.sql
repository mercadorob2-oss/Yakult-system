-- Cleanup: merge HP 202X color-variant duplicates into the "HP LASERJET 202X (COLOR)" set.
-- Keeping 51 (BLACK), 52 (CYAN), 53 (MAGENTA), 54 (YELLOW).
-- Folding in by color: 25 (CF500X Black) -> 51
--                      26 (CF501X Cyan), 29 (bare Cyan) -> 52
--                      28 (CF503X Magenta), 30 (bare Magenta) -> 53
--                      27 (CF502X Yellow), 31 (bare Yellow) -> 54

-- ── Preview ──────────────────────────────────────────────────────────────────
SELECT
    cm.ConsumableModelId, cm.ModelNumber, cm.Category, cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ConsumableModelId IN (25, 26, 27, 28, 29, 30, 31, 51, 52, 53, 54)
ORDER BY cm.ConsumableModelId;

-- Expected combined stock after merge:
--   51 (Black)   = 35 + 20            = 55
--   52 (Cyan)    = 21 + 10 + 10       = 41
--   53 (Magenta) = 40 + 10 + 10       = 60
--   54 (Yellow)  = 22 + 10 + 10       = 42

-- ── Uncomment below once the preview looks correct ──────────────────────────

/*
BEGIN TRANSACTION;

UPDATE dbo.Item SET ConsumableModelId = 51 WHERE ConsumableModelId = 25;                 -- Black
UPDATE dbo.Item SET ConsumableModelId = 52 WHERE ConsumableModelId IN (26, 29);          -- Cyan
UPDATE dbo.Item SET ConsumableModelId = 53 WHERE ConsumableModelId IN (28, 30);          -- Magenta
UPDATE dbo.Item SET ConsumableModelId = 54 WHERE ConsumableModelId IN (27, 31);          -- Yellow

UPDATE dbo.ConsumableModel
SET IsActive = 0
WHERE ConsumableModelId IN (25, 26, 27, 28, 29, 30, 31);

COMMIT TRANSACTION;

PRINT 'Merged HP 202X cluster: 25->51, {26,29}->52, {28,30}->53, {27,31}->54.';
*/
GO
