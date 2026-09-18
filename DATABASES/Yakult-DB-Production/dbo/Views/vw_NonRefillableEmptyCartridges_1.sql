-- ============================================================
-- View:    dbo.vw_NonRefillableEmptyCartridges
-- Created: 2026-02-20
-- Updated: 2026-02-21 — exclude Disposed/Sold (terminal states)
-- ============================================================
--
-- BUSINESS RULE ENFORCED:
--   CartridgeModel.IsRefillable = 0 means the model is retained by IT
--   for disposal or internal resale.  These returned empty cartridges
--   must NEVER appear in dbo.VendorCartridgeBatch or be presented as
--   vendor refill batch candidates.
--
-- SCOPE:
--   Returns dbo.EmptyCartridge rows whose CartridgeModel has
--   IsRefillable = 0, limited to rows that:
--     (a) have NOT been assigned to a VendorCartridgeBatch, AND
--     (b) are NOT in a terminal IT-action state (Disposed, Sold).
--
--   This keeps the view focused on empties still awaiting IT action.
--   Disposed and Sold rows are queryable directly on dbo.EmptyCartridge
--   for historical reporting or audit.
--
--   Rows with VendorBatchId IS NOT NULL would indicate a data-integrity
--   violation from before the IsRefillable rule was enforced.  Those
--   rows are intentionally excluded here; they can still be found via
--   a direct query on dbo.EmptyCartridge WHERE VendorBatchId IS NOT NULL.
--
-- TERMINAL STATUSES (non-refillable path only):
--   'Disposed' -- disposed of by IT; negative Inventory entry recorded
--   'Sold'     -- sold by IT;         negative Inventory entry recorded
--   Both are excluded here because the inventory impact has already been
--   applied and no further IT action is pending.
--
-- WHY LEFT JOIN ON CartridgeModel (not INNER JOIN):
--   EmptyCartridge.CartridgeModelId is NOT NULL with a FK constraint,
--   so orphaned rows cannot exist in production.  The LEFT JOIN is used
--   defensively: if a data-integrity issue ever produces an orphan,
--   the row surfaces here with NULL model columns instead of silently
--   disappearing.  ISNULL(cm.IsRefillable, 0) = 0 treats an unknown
--   model as non-refillable (fail-safe — surface the record, do not
--   discard it silently).
--
-- WHY NOT INNER JOIN ON VendorCartridgeBatch:
--   Non-refillable empties have VendorBatchId = NULL by design.
--   An INNER JOIN to VendorCartridgeBatch on VendorBatchId would drop
--   every correctly-classified non-refillable empty, leaving the view
--   empty.  The VendorBatchId IS NULL predicate is applied directly on
--   EmptyCartridge — no join to VendorCartridgeBatch is required.
-- ============================================================

CREATE   VIEW [dbo].[vw_NonRefillableEmptyCartridges]
AS
SELECT
    ec.EmptyCartridgeId,
    ec.CartridgeModelId,
    ISNULL(cm.ModelNumber, '[Unknown Model]')   AS CartridgeModel,
    ISNULL(cm.Brand,       '[Unknown Brand]')   AS Brand,
    ec.Quantity,
    ec.Status,
    ec.ReturnedAt,
    ec.ReqId,
    ec.VendorId                                 AS SupplierId,
    ISNULL(v.VendorName,  '[Unknown Vendor]')   AS SupplierName,
    ec.EmpId,
    ec.BranchId,
    ec.DeptId,
    ISNULL(ec.Remarks, '')                      AS Remarks,
    ec.CreatedDate,
    ec.CreatedBy,
    -- Surfaced explicitly so callers can confirm the IsRefillable filter was applied.
    -- Will be 0 for all rows returned by this view; NULL means unknown model (orphan).
    ISNULL(cm.IsRefillable, 0)                  AS IsRefillable
FROM dbo.EmptyCartridge ec
-- LEFT JOIN: defensive — surfaces orphaned rows instead of silently dropping them
LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT JOIN dbo.Vendor         v  ON ec.VendorId         = v.VendorID
WHERE ISNULL(cm.IsRefillable, 0) = 0   -- non-refillable models only (fail-safe: NULL → 0)
  AND ec.VendorBatchId IS NULL          -- correctly unassigned; excludes pre-fix violations
  AND ec.Status NOT IN ('Disposed', 'Sold'); -- exclude terminal IT-actioned rows