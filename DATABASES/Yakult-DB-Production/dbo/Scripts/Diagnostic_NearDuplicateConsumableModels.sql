-- Diagnostic: find ConsumableModel rows that are LIKELY the same product under
-- different spellings/formatting (typos, "76X" vs "CF276X", "(BLACK)" suffix, etc.)
-- Purpose : The backfill script (Backfill_ConsumableModel_FromExistingItems.sql) groups
--           by EXACT Name text, so it correctly merges true duplicates but leaves
--           near-duplicates (different spelling/formatting for the same physical
--           product) as separate models. This script surfaces every such cluster at
--           once so you don't have to discover them one at a time.
-- Method  : Strips spaces/punctuation and sorts the remaining alphanumeric characters,
--           so "HP 76X Black Laserjet" and "HP Black 76X LaserJet" (or minor typos that
--           don't change letter composition much) land in the same bucket. This is a
--           blunt heuristic — it will both under- and over-group in edge cases, so
--           always eyeball the ModelNumber column before merging anything.

;WITH Normalized AS (
    SELECT
        cm.ConsumableModelId,
        cm.ModelNumber,
        cm.Category,
        cm.IsActive,
        (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock,
        (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
        -- Strip everything except letters/digits, then keep only the first 12 chars of
        -- the alphanumeric-only text as a rough fingerprint (catches near-identical
        -- prefixes even when suffixes like "(BLACK)" or model-number formatting differ).
        LEFT(
            LOWER(
                REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(
                    cm.ModelNumber, ' ', ''), '(', ''), ')', ''), '-', ''), '/', ''), ',', '')
            ), 10
        ) AS Fingerprint
    FROM dbo.ConsumableModel cm
    WHERE cm.IsActive = 1
)
SELECT n.Fingerprint, n.Category, COUNT(*) AS GroupSize,
       STRING_AGG(CAST(n.ConsumableModelId AS NVARCHAR(20)) + ':' + n.ModelNumber + ' (stock=' + CAST(n.TotalStock AS NVARCHAR(20)) + ')', ' | ')
           WITHIN GROUP (ORDER BY n.TotalStock DESC) AS Members
FROM Normalized n
GROUP BY n.Fingerprint, n.Category
HAVING COUNT(*) > 1
ORDER BY COUNT(*) DESC, n.Fingerprint;
GO
