-- ============================================================
-- Audit / Diagnostic Script (READ-ONLY — no data changes)
-- Date:    2026-02-20
-- Purpose: Identify pre-existing data violations caused by the absence
--          of an IsRefillable guard in the empty-cartridge return flow.
-- ============================================================
--
-- BACKGROUND:
--   Prior to enforcing CartridgeModel.IsRefillable, the queries that
--   populated vendor refill batch candidates had no IsRefillable filter.
--   Two specific code paths could produce bad data:
--
--   1. GetUnassignedReturnsAsync (CartridgeRefillRepository)
--      Returned ALL pending, unassigned empties regardless of IsRefillable.
--      Downstream: Manual Batch Assignment dialog could select and assign
--      non-refillable empties to a VendorCartridgeBatch.
--
--   2. GetEmptyCartridgesByVendorAsync (CartridgeRefillRepository)
--      Aggregated ALL pending empties by model with no IsRefillable guard.
--      Downstream: Batch creation page displayed non-refillable models as
--      refill candidates; IT could create a VendorCartridgeBatch for them.
--
--   3. CreateBatchAndAssignEmptiesAsync inner getEmptiesSql
--      Collected empties by CartridgeModelId with no IsRefillable check.
--      Only the service-layer guard (AssertBatchCartridgeModelIsRefillableAsync)
--      blocked this path after the model-validation step.  If called
--      from a non-standard code path, non-refillable empties could be
--      assigned to a batch.
--
-- The three queries below are READ-ONLY diagnostics.  Run them to
-- determine whether any corrective action is needed on live data.
-- ============================================================


-- ============================================================
-- DIAGNOSTIC 1: Non-refillable empties that already have a VendorBatchId
-- ============================================================
-- These rows should not exist.  Each row represents a non-refillable
-- returned empty that was incorrectly assigned to a vendor refill batch
-- before the IsRefillable rule was enforced.
--
-- If this query returns rows, review the associated batches and decide
-- whether to clear ec.VendorBatchId (and update VendorCartridgeBatch.ReturnedQty)
-- or leave them in place and track them separately.
-- ============================================================

SELECT
    ec.EmptyCartridgeId,
    ec.CartridgeModelId,
    cm.ModelNumber                              AS CartridgeModel,
    cm.IsRefillable,
    ec.VendorBatchId,
    vcb.Status                                  AS BatchStatus,
    ec.Quantity,
    ec.Status                                   AS EmptyStatus,
    ec.ReturnedAt,
    ec.Remarks
FROM dbo.EmptyCartridge            ec
LEFT JOIN dbo.CartridgeModel       cm  ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT JOIN dbo.VendorCartridgeBatch vcb ON ec.VendorBatchId    = vcb.BatchId
WHERE ISNULL(cm.IsRefillable, 0) = 0   -- non-refillable model
  AND ec.VendorBatchId IS NOT NULL      -- violation: should never be in a batch
ORDER BY ec.ReturnedAt DESC;
GO


-- ============================================================
-- DIAGNOSTIC 2: Summary — unassigned non-refillable empties by model
-- ============================================================
-- These are the rows that vw_NonRefillableEmptyCartridges will expose
-- going forward.  Shows IT the scope of what is currently in custody
-- awaiting disposal or internal resale.
-- ============================================================

SELECT
    cm.CartridgeModelId,
    ISNULL(cm.ModelNumber, '[Unknown]')         AS CartridgeModel,
    ISNULL(cm.Brand, '')                        AS Brand,
    COUNT(DISTINCT ec.EmptyCartridgeId)         AS ReturnRowCount,
    SUM(ec.Quantity)                            AS TotalUnits,
    MIN(ec.ReturnedAt)                          AS OldestReturn,
    MAX(ec.ReturnedAt)                          AS LatestReturn
FROM dbo.EmptyCartridge      ec
LEFT JOIN dbo.CartridgeModel cm ON ec.CartridgeModelId = cm.CartridgeModelId
WHERE ISNULL(cm.IsRefillable, 0) = 0
  AND ec.VendorBatchId IS NULL
GROUP BY
    cm.CartridgeModelId,
    cm.ModelNumber,
    cm.Brand
ORDER BY SUM(ec.Quantity) DESC;
GO


-- ============================================================
-- DIAGNOSTIC 3: Confirm refillable-flow queries now exclude
--               non-refillable models (expected: zero rows)
-- ============================================================
-- Cross-checks vw_UnassignedRefillableEmpties and vw_RefillBatchCandidates.
-- Both should return zero non-refillable rows.  If either returns rows,
-- the view definition contains a bug.
-- ============================================================

-- Should return 0 rows
SELECT 'vw_UnassignedRefillableEmpties' AS ViewName, COUNT(*) AS NonRefillableRows
FROM dbo.vw_UnassignedRefillableEmpties
WHERE IsRefillable <> 1

UNION ALL

-- Should return 0 rows
SELECT 'vw_RefillBatchCandidates' AS ViewName, COUNT(*) AS NonRefillableRows
FROM dbo.vw_RefillBatchCandidates
WHERE IsRefillable <> 1;
GO
