-- ============================================================
-- Find and Fix the 3rd Feb 23 record:
--   DISPOSE | SellNonRefill123 | Damaged | No Recipient | BatchId = NULL
--
-- This record was NOT caught by the first fix because it was
-- either missing from ItemLifecycleDecisionCartridge entirely,
-- or its EmptyCartridge row has no VendorBatchId set.
-- ============================================================


-- ──────────────────────────────────────────────────────────────
-- STEP 0: Query the view directly — reveals all 3 rows and
--         their actual DecisionIds so we know exactly what to fix
-- ──────────────────────────────────────────────────────────────

SELECT
    v.DecisionId,
    v.DecisionTypeName                                              AS [Type],
    CAST(
        CAST(v.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE)                                                        AS Date_SGT,
    v.BatchId,
    v.EmptyCartridgeId,
    v.CartridgeModelNumber,
    v.ItemModelNumber,
    v.ConditionName,
    v.RecipientName,
    v.VendorName
FROM dbo.vw_DisposedSoldItems v
WHERE
    CAST(
        CAST(v.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE) = '2026-02-23'
ORDER BY v.DecisionId;


-- ──────────────────────────────────────────────────────────────
-- STEP 1: Find the missing decision
-- Look for all DISPOSE decisions on 02/23/2026 that still
-- return NULL for BatchId in the view, excluding the ones
-- we already fixed (DecisionId 1 and 2)
-- ──────────────────────────────────────────────────────────────

SELECT
    d.DecisionId,
    dt.DecisionTypeName                                             AS [Type],
    CAST(
        CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE)                                                        AS DecidedAt_SGT,
    d.DecisionStatus,
    c.ConditionName,
    d.RecipientName,

    -- EmptyCartridge linkage (will be NULL if no row in DecisionCartridge)
    ldc.DecisionCartridgeId,
    ldc.EmptyCartridgeId,
    ec.VendorBatchId                                                AS EC_VendorBatchId,
    ec.CartridgeModelId,
    cm.ModelNumber                                                  AS CartridgeModel,
    ec.ConditionStatus,
    ec.Status                                                       AS EC_Status,

    -- Item
    i.ItemId,
    i.Name                                                          AS ItemName,

    CASE
        WHEN ldc.DecisionCartridgeId IS NULL
            THEN 'No ItemLifecycleDecisionCartridge row — EmptyCartridge never linked'
        WHEN ec.VendorBatchId IS NULL
            THEN 'EmptyCartridge exists but VendorBatchId is NULL'
        ELSE 'Has BatchId — should not appear here'
    END                                                             AS Diagnosis

FROM dbo.ItemLifecycleDecision              d
JOIN  dbo.ItemDecisionType                  dt  ON d.DecisionTypeId    = dt.DecisionTypeId
JOIN  dbo.Item                              i   ON d.ItemId            = i.ItemId
LEFT  JOIN dbo.Condition                    c   ON d.ConditionId       = c.ConditionID
LEFT  JOIN dbo.ItemLifecycleDecisionCartridge ldc ON d.DecisionId      = ldc.DecisionId
LEFT  JOIN dbo.EmptyCartridge               ec  ON ldc.EmptyCartridgeId = ec.EmptyCartridgeId
LEFT  JOIN dbo.CartridgeModel               cm  ON ec.CartridgeModelId = cm.CartridgeModelId

WHERE
    d.DecisionStatus    = 'Executed'
    AND dt.DecisionTypeName = 'DISPOSE'
    AND d.DecisionId    NOT IN (1, 2)       -- exclude the two we already fixed
    AND CAST(
            CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
            AT TIME ZONE 'Singapore Standard Time'
        AS DATE) = '2026-02-23';


-- ──────────────────────────────────────────────────────────────
-- STEP 2: Fix — run after confirming the DecisionId above
--
-- Case A: No EmptyCartridge row linked (ldc is NULL)
--   → Create an EmptyCartridge, then create a DISPOSE batch,
--     then link both.
--
-- Case B: EmptyCartridge exists but VendorBatchId is NULL
--   → Just create a DISPOSE batch and update VendorBatchId.
--
-- Replace @DecisionId with the value found in STEP 1.
-- ──────────────────────────────────────────────────────────────

DECLARE @DecisionId       INT     = 3;    -- confirmed from view result
DECLARE @EmptyCartridgeId INT     = 188;  -- confirmed from view result
DECLARE @CartridgeModelId INT     = 40;   -- SellNonRefill123
DECLARE @CreatedBy        INT     = 8;    -- UserId who made the decision
DECLARE @DecisionDate     DATETIME = '2026-02-23 16:47:33'; -- original UTC DecidedAt
DECLARE @VendorId         INT;

-- Resolve VendorId from existing batches for CartridgeModelId 40
SELECT TOP 1
    @VendorId = vcb.VendorId
FROM dbo.VendorCartridgeBatch vcb
WHERE vcb.CartridgeModelId = @CartridgeModelId
ORDER BY vcb.BatchId DESC;

SELECT
    '@DecisionId'       = @DecisionId,
    '@EmptyCartridgeId' = @EmptyCartridgeId,
    '@CartridgeModelId' = @CartridgeModelId,
    '@VendorId resolved'= @VendorId,
    '@CreatedBy'        = @CreatedBy;


-- ──────────────────────────────────────────────────────────────
-- STEP 3: Apply the fix (fill in @DecisionId from STEP 1, then run)
-- ──────────────────────────────────────────────────────────────

BEGIN TRANSACTION;
BEGIN TRY

    DECLARE @NewBatchId INT;

    -- EmptyCartridgeId 188 already exists and is already linked to DecisionId 3
    -- via ItemLifecycleDecisionCartridge — just needs a batch created and VendorBatchId set

    -- Create the retroactive DISPOSE batch
    INSERT INTO dbo.VendorCartridgeBatch
        (VendorId, CartridgeModelId, OriginalQty, ReturnedQty,
         BatchPurpose, Status, DateReceived, ClosedDate,
         CreatedBy, CreatedDate, Remarks)
    VALUES
        (@VendorId, @CartridgeModelId, 1, 1,
         'DISPOSE', 'Disposed',
         @DecisionDate, @DecisionDate,
         @CreatedBy, @DecisionDate,
         'Retroactive batch - Damaged DISPOSE on 2026-02-23 before batch was created.');

    SET @NewBatchId = SCOPE_IDENTITY();

    -- Link EmptyCartridgeId 188 to the new batch
    UPDATE dbo.EmptyCartridge
    SET    VendorBatchId = @NewBatchId,
           DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
           ModifiedBy    = @CreatedBy
    WHERE  EmptyCartridgeId = @EmptyCartridgeId
      AND  VendorBatchId IS NULL;

    -- Verify
    SELECT v.BatchId, v.DecisionTypeName, v.ConditionName, v.CartridgeModelNumber
    FROM dbo.vw_DisposedSoldItems v
    WHERE v.DecisionId = @DecisionId;

    COMMIT TRANSACTION;
    PRINT 'Success. New DISPOSE BatchId = ' + CAST(@NewBatchId AS VARCHAR)
        + ', EmptyCartridgeId = '           + CAST(@EmptyCartridgeId AS VARCHAR);

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
END CATCH;
