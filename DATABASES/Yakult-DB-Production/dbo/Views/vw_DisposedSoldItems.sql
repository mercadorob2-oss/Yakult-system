-- ============================================================
-- ALTER VIEW: vw_DisposedSoldItems
-- Purpose   : Add VendorName column (from dbo.Vendor via
--             VendorCartridgeBatch) for Disposed & Sold report.
-- ============================================================
CREATE VIEW [dbo].[vw_DisposedSoldItems]
AS
SELECT
    -- ── Decision ────────────────────────────────────────────
    d.DecisionId,
    d.DecidedAt,
    dt.DecisionTypeName,                        -- 'DISPOSE' | 'SELL'
    d.DecisionStatus,                           -- always 'Executed' via WHERE
    d.Quantity,
    d.RecipientName,
    d.SaleAmount,
    d.Remarks,

    -- ── Item ────────────────────────────────────────────────
    i.ItemId,
    i.Name              AS ItemName,
    i.ModelNumber       AS ItemModelNumber,
    i.SerialNumber,

    -- ── Category ────────────────────────────────────────────
    cat.CategoryId,
    cat.Name            AS CategoryName,

    -- ── Condition at time of decision (snapshot) ────────────
    c.ConditionName,

    -- ── Who executed the decision ───────────────────────────
    u.Name              AS DecidedByName,

    -- ── Cartridge extension (NULL for non-cartridge items) ──
    ldc.EmptyCartridgeId,
    cm.CartridgeModelId,
    cm.ModelNumber      AS CartridgeModelNumber,
    cm.Brand            AS CartridgeBrand,
    ec.DisposalCompanyName,
    ec.ReturnedAt       AS CartridgeReturnedAt,

    -- ── Batch Information (from VendorCartridgeBatch) ────────
    vcb.BatchId,

    -- ── Vendor / Company (who received the disposed cartridges) ─
    v.VendorName

FROM dbo.ItemLifecycleDecision d
JOIN  dbo.ItemDecisionType              dt  ON d.DecisionTypeId     = dt.DecisionTypeId
JOIN  dbo.Item                          i   ON d.ItemId              = i.ItemId
LEFT JOIN dbo.ItemCategory              cat ON i.CategoryId          = cat.CategoryId
LEFT JOIN dbo.Condition                 c   ON d.ConditionId         = c.ConditionID
LEFT JOIN dbo.[User]                    u   ON d.DecidedBy           = u.UserId
LEFT JOIN dbo.ItemLifecycleDecisionCartridge ldc ON d.DecisionId     = ldc.DecisionId
LEFT JOIN dbo.EmptyCartridge            ec  ON ldc.EmptyCartridgeId  = ec.EmptyCartridgeId
LEFT JOIN dbo.CartridgeModel            cm  ON ec.CartridgeModelId   = cm.CartridgeModelId
LEFT JOIN dbo.VendorCartridgeBatch      vcb ON ec.VendorBatchId      = vcb.BatchId
LEFT JOIN dbo.Vendor                    v   ON vcb.VendorId          = v.VendorID

WHERE d.DecisionStatus = 'Executed';