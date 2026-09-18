-- ============================================================
-- Identify BatchId for Sold/Disposed Cartridges on 02/23/2026
-- Report: Disposed & Sold Items Report (Report1.rdlc)
-- Issue : BatchId shows NULL for 02/23/2026 records because
--         EmptyCartridge.VendorBatchId is not set for those rows
-- ============================================================

-- Helper: Convert stored UTC DATETIME2 to Singapore Standard Time (UTC+8)
-- CAST(CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
--      AT TIME ZONE 'Singapore Standard Time' AS DATE)


-- ==============================================================
-- SECTION 1: The Affected Records (02/23/2026 decisions)
-- Shows exactly which EmptyCartridge rows have NULL VendorBatchId
-- ==============================================================

SELECT
    d.DecisionId,
    dt.DecisionTypeName                                          AS [Type],
    CAST(
        CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE)                                                     AS [DecidedAt_SGT],
    d.DecisionStatus,

    -- Item / Cartridge
    i.ItemId,
    i.Name                                                       AS ItemName,
    cm.ModelNumber                                               AS CartridgeModel,
    cm.Brand                                                     AS CartridgeBrand,

    -- Empty cartridge row
    ec.EmptyCartridgeId,
    ec.ConditionStatus,
    ec.Status                                                    AS EC_Status,
    ec.ReturnedAt,

    -- The missing link
    ec.VendorBatchId                                             AS CurrentVendorBatchId,   -- NULL = the problem
    vcb.BatchId                                                  AS ResolvedBatchId,        -- NULL when VendorBatchId is unset
    vcb.BatchPurpose,
    vcb.Status                                                   AS BatchStatus,

    -- Condition at decision time
    c.ConditionName,
    d.RecipientName,
    d.Remarks

FROM dbo.ItemLifecycleDecision              d
JOIN  dbo.ItemDecisionType                  dt  ON d.DecisionTypeId    = dt.DecisionTypeId
JOIN  dbo.Item                              i   ON d.ItemId            = i.ItemId
LEFT  JOIN dbo.Condition                    c   ON d.ConditionId       = c.ConditionID
LEFT  JOIN dbo.ItemLifecycleDecisionCartridge ldc ON d.DecisionId      = ldc.DecisionId
LEFT  JOIN dbo.EmptyCartridge               ec  ON ldc.EmptyCartridgeId = ec.EmptyCartridgeId
LEFT  JOIN dbo.CartridgeModel               cm  ON ec.CartridgeModelId = cm.CartridgeModelId
LEFT  JOIN dbo.VendorCartridgeBatch         vcb ON ec.VendorBatchId    = vcb.BatchId    -- LEFT: will be NULL

WHERE
    d.DecisionStatus = 'Executed'
    AND CAST(
            CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
            AT TIME ZONE 'Singapore Standard Time'
        AS DATE) = '2026-02-23'
    AND dt.DecisionTypeName IN ('SELL', 'DISPOSE')

ORDER BY d.DecisionId;


-- ==============================================================
-- SECTION 2: Candidate Batches
-- For each CartridgeModel in the affected records, find all
-- VendorCartridgeBatch rows with purpose SELL or DISPOSE
-- These are the batches the EmptyCartridges should be linked to
-- ==============================================================

SELECT
    vcb.BatchId,
    vcb.BatchPurpose,
    vcb.Status                                                   AS BatchStatus,
    cm.ModelNumber                                               AS CartridgeModel,
    v.VendorName,
    vcb.OriginalQty,
    vcb.ReturnedQty,
    vcb.DateReceived,
    vcb.ClosedDate,
    vcb.Remarks,

    -- How many EmptyCartridges are already linked to this batch
    COUNT(ec.EmptyCartridgeId)                                   AS LinkedEmptyCartridges

FROM dbo.VendorCartridgeBatch           vcb
JOIN  dbo.CartridgeModel                cm  ON vcb.CartridgeModelId = cm.CartridgeModelId
JOIN  dbo.Vendor                        v   ON vcb.VendorId         = v.VendorID
LEFT  JOIN dbo.EmptyCartridge           ec  ON ec.VendorBatchId     = vcb.BatchId

WHERE
    vcb.BatchPurpose IN ('SELL', 'DISPOSE')
    AND cm.ModelNumber IN (
        -- Cartridge models from the affected 02/23/2026 decisions
        SELECT DISTINCT cm2.ModelNumber
        FROM dbo.ItemLifecycleDecision              d2
        JOIN  dbo.ItemDecisionType                  dt2  ON d2.DecisionTypeId      = dt2.DecisionTypeId
        JOIN  dbo.ItemLifecycleDecisionCartridge    ldc2 ON d2.DecisionId          = ldc2.DecisionId
        JOIN  dbo.EmptyCartridge                    ec2  ON ldc2.EmptyCartridgeId  = ec2.EmptyCartridgeId
        JOIN  dbo.CartridgeModel                    cm2  ON ec2.CartridgeModelId   = cm2.CartridgeModelId
        WHERE
            d2.DecisionStatus = 'Executed'
            AND CAST(
                    CAST(d2.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
                    AT TIME ZONE 'Singapore Standard Time'
                AS DATE) = '2026-02-23'
            AND dt2.DecisionTypeName IN ('SELL', 'DISPOSE')
    )

GROUP BY
    vcb.BatchId, vcb.BatchPurpose, vcb.Status,
    cm.ModelNumber, v.VendorName,
    vcb.OriginalQty, vcb.ReturnedQty,
    vcb.DateReceived, vcb.ClosedDate, vcb.Remarks

ORDER BY vcb.BatchId;


-- ==============================================================
-- SECTION 3: Cross-Reference
-- Match each affected EmptyCartridge to the most likely batch
-- by same CartridgeModelId + BatchPurpose matching decision type
-- + DateReceived on or before the decision date
-- ==============================================================

SELECT
    d.DecisionId,
    dt.DecisionTypeName                                          AS [Type],
    ec.EmptyCartridgeId,
    cm.ModelNumber                                               AS CartridgeModel,
    ec.ConditionStatus,

    -- Best-matching candidate batch
    vcb_cand.BatchId                                             AS CandidateBatchId,
    vcb_cand.BatchPurpose                                        AS CandidateBatchPurpose,
    vcb_cand.Status                                              AS CandidateBatchStatus,
    v_cand.VendorName                                            AS CandidateVendor,
    vcb_cand.DateReceived                                        AS CandidateBatchReceived,
    vcb_cand.OriginalQty,
    vcb_cand.ReturnedQty,

    CASE
        WHEN ec.VendorBatchId IS NULL
        THEN 'VendorBatchId IS NULL — needs to be set to CandidateBatchId'
        ELSE 'Already linked'
    END                                                          AS [Diagnosis]

FROM dbo.ItemLifecycleDecision              d
JOIN  dbo.ItemDecisionType                  dt   ON d.DecisionTypeId      = dt.DecisionTypeId
JOIN  dbo.ItemLifecycleDecisionCartridge    ldc  ON d.DecisionId          = ldc.DecisionId
JOIN  dbo.EmptyCartridge                    ec   ON ldc.EmptyCartridgeId  = ec.EmptyCartridgeId
JOIN  dbo.CartridgeModel                    cm   ON ec.CartridgeModelId   = cm.CartridgeModelId

-- Match to candidate batch: same model, same-type purpose, received before or on decision date
LEFT  JOIN dbo.VendorCartridgeBatch         vcb_cand
        ON  vcb_cand.CartridgeModelId = ec.CartridgeModelId
        AND vcb_cand.BatchPurpose     = dt.DecisionTypeName   -- 'SELL' or 'DISPOSE'
        AND vcb_cand.DateReceived    <= CAST(d.DecidedAt AS DATETIME)

LEFT  JOIN dbo.Vendor                       v_cand ON vcb_cand.VendorId = v_cand.VendorID

WHERE
    d.DecisionStatus = 'Executed'
    AND CAST(
            CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
            AT TIME ZONE 'Singapore Standard Time'
        AS DATE) = '2026-02-23'
    AND dt.DecisionTypeName IN ('SELL', 'DISPOSE')

ORDER BY
    d.DecisionId,
    vcb_cand.DateReceived DESC;   -- Most recent matching batch first
