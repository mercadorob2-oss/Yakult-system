-- ============================================================
-- View:    dbo.vw_UnassignedRefillableEmpties
-- Created: 2026-02-20
-- ============================================================
--
-- BUSINESS RULE ENFORCED:
--   Only EmptyCartridge rows whose CartridgeModel.IsRefillable = 1
--   are eligible to be assigned to a dbo.VendorCartridgeBatch.
--   This view is the correct source for:
--     - The Manual Batch Assignment wizard
--     - Any UI that lists "returned empties awaiting batch assignment"
--
-- ROOT CAUSE THIS FIXES:
--   The prior inline SQL query (GetUnassignedReturnsAsync in
--   CartridgeRefillRepository.cs) filtered only on:
--
--       WHERE ec.VendorBatchId IS NULL
--         AND ec.Status        = 'Pending'
--
--   There was NO IsRefillable guard at the SQL level.  As a result,
--   non-refillable empties (IsRefillable = 0) appeared in the same
--   unassigned pool as refillable ones, making them visible in the
--   Manual Batch Assignment dialog as if they were eligible for a
--   vendor refill batch.
--
--   The only protection was at the application layer (UI button state
--   and AssertBatchCartridgeModelIsRefillableAsync in the service).
--   This view adds an explicit SQL-level guard that is enforced
--   regardless of which application layer calls it.
--
-- WHY LEFT JOIN ON CartridgeModel (not INNER JOIN):
--   Defensive coding — if a CartridgeModel row were ever missing,
--   INNER JOIN would silently drop the EmptyCartridge row from this
--   view, hiding data that IT would need to investigate.
--   LEFT JOIN + ISNULL(cm.IsRefillable, 1) = 1 keeps the row visible
--   (unknown model defaults to refillable, consistent with
--   CartridgeModel.IsRefillable DEFAULT ((1))).
-- ============================================================

CREATE VIEW [dbo].[vw_UnassignedRefillableEmpties]
AS
SELECT
    ec.EmptyCartridgeId,
    ec.CartridgeModelId,
    ISNULL(cm.ModelNumber, '[Unknown Model]')   AS CartridgeModel,
    ISNULL(cm.Brand,       '')                  AS Brand,
    ec.VendorId                                 AS SupplierId,
    ISNULL(v.VendorName,  '[Unknown Vendor]')   AS SupplierName,
    ec.Quantity,
    ec.Status,
    ec.ReturnedAt,
    ec.ReqId,
    ISNULL(ec.Remarks, '')                      AS Remarks,
    -- Surfaced explicitly so callers can confirm the IsRefillable filter was applied.
    -- Will be 1 for all rows returned by this view; NULL means unknown model (orphan).
    ISNULL(cm.IsRefillable, 1)                  AS IsRefillable
FROM dbo.EmptyCartridge ec
-- LEFT JOIN: defensive — surfaces orphaned rows instead of silently dropping them
LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
WHERE ISNULL(cm.IsRefillable, 1) = 1   -- refillable models only (fail-safe: NULL → 1)
  AND ec.VendorBatchId IS NULL          -- not yet assigned to any vendor batch
  AND ec.Status        = 'Pending';     -- not yet dispatched, completed, or otherwise closed