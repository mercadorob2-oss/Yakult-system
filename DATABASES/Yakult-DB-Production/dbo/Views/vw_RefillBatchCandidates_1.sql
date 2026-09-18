-- ============================================================
-- View:    dbo.vw_RefillBatchCandidates
-- Created: 2026-02-20
-- ============================================================
--
-- BUSINESS RULE ENFORCED:
--   Only EmptyCartridge rows with CartridgeModel.IsRefillable = 1
--   may become part of a dbo.VendorCartridgeBatch.
--   This view aggregates eligible pending empties by cartridge model
--   to provide the batch-creation summary used when IT initiates a
--   new vendor refill batch.
--
-- ROOT CAUSE THIS FIXES:
--   GetEmptyCartridgesByVendorAsync in CartridgeRefillRepository.cs
--   performed no IsRefillable filtering.  Its SQL was:
--
--       FROM dbo.EmptyCartridge ec
--       LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
--       LEFT JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId = vcb.BatchId
--       WHERE ec.Status = 'Pending'
--         AND (
--             ec.VendorBatchId IS NULL
--             OR ec.VendorBatchId = 0
--             OR vcb.Status = 'Active'
--         )
--       GROUP BY ec.CartridgeModelId, cm.ModelNumber
--       HAVING SUM(ec.Quantity) > 0
--
--   Non-refillable models appeared alongside refillable ones in the
--   batch-creation page.  The only protection was:
--     (a) UI button state (IsRefillable check on selection)
--     (b) AssertBatchCartridgeModelIsRefillableAsync in the service layer
--   Neither of these guards prevented the model from appearing as a
--   candidate in the list — they only blocked the final submit action.
--
--   This view adds an explicit SQL-level guard that excludes
--   non-refillable models from the candidate pool entirely.
--
-- WHY LEFT JOIN ON VendorCartridgeBatch (not INNER JOIN):
--   EmptyCartridge rows with VendorBatchId IS NULL (the majority of
--   unassigned returns) have no matching row in VendorCartridgeBatch.
--   An INNER JOIN would drop all unassigned empties, returning only
--   rows linked to an existing batch — the opposite of what is needed.
--   LEFT JOIN is mandatory here to preserve the unassigned rows.
--   The WHERE clause then re-applies the "unassigned or collecting"
--   predicate as a row-level filter after the join completes.
--
-- WHY LEFT JOIN ON CartridgeModel (not INNER JOIN):
--   Defensive coding — see vw_NonRefillableEmptyCartridges header.
--   ISNULL(cm.IsRefillable, 1) = 1 treats an unknown model as
--   refillable (consistent with CartridgeModel.IsRefillable DEFAULT 1).
-- ============================================================

CREATE VIEW [dbo].[vw_RefillBatchCandidates]
AS
SELECT
    ec.CartridgeModelId,
    -- Surface a diagnostic label when CartridgeModelId has no matching CartridgeModel row
    ISNULL(
        cm.ModelNumber,
        '[DATA ERROR: Missing CartridgeModel for EmptyCartridgeId '
            + CAST(MIN(ec.EmptyCartridgeId) AS NVARCHAR(20)) + ']'
    )                                           AS CartridgeModel,
    ISNULL(cm.Brand, '')                        AS Brand,
    -- IsRefillable is always 1 for rows returned by this view.
    -- Surfaced so the batch-creation page can display it without a secondary lookup.
    ISNULL(cm.IsRefillable, 1)                  AS IsRefillable,
    SUM(ec.Quantity)                            AS PendingEmptyQty,
    MIN(ec.ReturnedAt)                          AS OldestReturn,
    MAX(ec.ReturnedAt)                          AS LatestReturn,
    COUNT(DISTINCT ec.EmptyCartridgeId)         AS ReturnRowCount
FROM dbo.EmptyCartridge            ec
-- LEFT JOIN: mandatory — rows with VendorBatchId IS NULL have no matching VendorCartridgeBatch
-- row; INNER JOIN would drop them, defeating the purpose of this view entirely.
LEFT JOIN dbo.CartridgeModel       cm  ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId    = vcb.BatchId
WHERE ISNULL(cm.IsRefillable, 1) = 1   -- refillable models only (fail-safe: NULL → 1)
  AND ec.Status = 'Pending'             -- not yet dispatched, completed, or otherwise closed
  AND (
      ec.VendorBatchId IS NULL          -- unassigned: most common case
   OR ec.VendorBatchId = 0             -- legacy zero sentinel (pre-constraint data)
   OR vcb.Status = 'Active'            -- assigned to a batch that is still collecting
  )
GROUP BY
    ec.CartridgeModelId,
    cm.ModelNumber,
    cm.Brand,
    cm.IsRefillable
HAVING SUM(ec.Quantity) > 0;           -- suppress models with net-zero pending quantity