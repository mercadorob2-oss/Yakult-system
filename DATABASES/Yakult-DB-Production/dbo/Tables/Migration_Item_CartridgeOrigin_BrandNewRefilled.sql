-- ============================================================
-- Migration: Cartridge Origin (Brand New / Refilled) Support
-- Feature: BatchAddItemDialog Brand New / Refilled selection
-- Date: 2026-02-27
-- ============================================================
-- NO SCHEMA CHANGES REQUIRED
-- dbo.Item.RefillStatus (NVARCHAR(50), NULL) already exists and
-- is used by this feature with the following defined values:
--
--   NULL        = Brand New cartridge (default; backward compatible with all
--                 existing records — no data backfill needed)
--
--   'Available' = Refilled cartridge entered manually via BatchAddItemDialog,
--                 OR refilled cartridge received back from a vendor via the
--                 refill reception workflow (CompleteRefillAndRestockAsync).
--                 Both cases represent a ready-to-use refilled unit.
--
-- DOWNSTREAM IMPACT:
--   * GetAvailableIssuableStock   — does NOT filter by RefillStatus; no impact.
--   * GetIssuableItemIdsForQuantity — does NOT filter by RefillStatus; no impact.
--   * IX_Item_CartridgeModel_Available index — covers RefillStatus IS NULL only
--     (Brand New items). Refilled items are excluded from this index, consistent
--     with existing behavior for vendor-refilled stock.
--   * dbo.Cartridge (deprecated) — CartridgeTypeId now resolved correctly:
--       'Available' RefillStatus → CartridgeTypeId = Refill type
--       NULL RefillStatus        → CartridgeTypeId = Brand New type
--   * dbo.CartridgeMovement — MovementType='StockIn' row now also uses the
--     correct CartridgeTypeId so Brand New / Refilled reporting is accurate.
--
-- BACKWARD COMPATIBILITY:
--   All existing dbo.Item rows with RefillStatus = NULL continue to represent
--   Brand New cartridges. No UPDATE or backfill is required.
--
-- VERIFICATION QUERY (run after deploying the application change):
SELECT
    CASE WHEN RefillStatus IS NULL THEN 'Brand New' ELSE RefillStatus END AS Origin,
    COUNT(*) AS ItemCount
FROM dbo.Item
WHERE Category = 'Cartridge'
  AND Active = 1
GROUP BY
    CASE WHEN RefillStatus IS NULL THEN 'Brand New' ELSE RefillStatus END
ORDER BY Origin;
