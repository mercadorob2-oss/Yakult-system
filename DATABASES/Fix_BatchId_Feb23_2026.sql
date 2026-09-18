-- ============================================================
-- FIX: Assign BatchId to Sold/Disposed Cartridges on 02/23/2026
--
-- Root Cause:
--   EmptyCartridge rows 185 (DISPOSE) and 186 (SELL) have
--   VendorBatchId = NULL because no VendorCartridgeBatch existed
--   for SellNonRefill123 on 02/23/2026. Batches were only created
--   starting 2026-03-11, so the view returns NULL for BatchId.
--
-- Fix:
--   1. Preview the affected EmptyCartridge rows (VendorId, model)
--   2. Create one SELL batch and one DISPOSE batch dated 02/23/2026
--   3. Link the affected EmptyCartridge rows to the new batches
-- ============================================================


-- ──────────────────────────────────────────────────────────────
-- STEP 0: Preview before making any changes
-- Confirm EmptyCartridgeIds, their VendorId, and CartridgeModelId
-- ──────────────────────────────────────────────────────────────

SELECT
    ec.EmptyCartridgeId,
    ec.VendorBatchId       AS CurrentBatchId,     -- should be NULL
    ec.VendorId,
    ec.CartridgeModelId,
    cm.ModelNumber         AS CartridgeModel,
    ec.ConditionStatus,
    ec.Status              AS EC_Status,
    dt.DecisionTypeName    AS DecisionType,
    d.DecisionId,
    CAST(
        CAST(d.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE)               AS DecidedAt_SGT
FROM dbo.EmptyCartridge                         ec
JOIN  dbo.CartridgeModel                        cm  ON ec.CartridgeModelId  = cm.CartridgeModelId
JOIN  dbo.ItemLifecycleDecisionCartridge        ldc ON ldc.EmptyCartridgeId = ec.EmptyCartridgeId
JOIN  dbo.ItemLifecycleDecision                 d   ON d.DecisionId         = ldc.DecisionId
JOIN  dbo.ItemDecisionType                      dt  ON dt.DecisionTypeId    = d.DecisionTypeId
WHERE
    ec.EmptyCartridgeId IN (185, 186)   -- the two affected rows from Section 1
    AND ec.VendorBatchId IS NULL;


-- ──────────────────────────────────────────────────────────────
-- STEP 1–3: Create batches and link EmptyCartridges
--
-- Run STEP 0 first and confirm:
--   (a) VendorId is correct for both rows
--   (b) CartridgeModelId is the same for both
--
-- Replace @VendorId and @CreatedBy below with the actual values
-- from STEP 0 before executing.
-- ──────────────────────────────────────────────────────────────

BEGIN TRANSACTION;

BEGIN TRY

    -- ── Variables ──────────────────────────────────────────────
    DECLARE @CartridgeModelId  INT;
    DECLARE @VendorId          INT;
    DECLARE @CreatedBy         INT  = 1;      -- ← replace with the UserId who made the original decisions
    DECLARE @DecisionDate      DATETIME = '2026-02-23 00:00:00';

    -- Step A: Get CartridgeModelId from the affected EmptyCartridge rows
    SELECT TOP 1
        @CartridgeModelId = ec.CartridgeModelId
    FROM dbo.EmptyCartridge ec
    WHERE ec.EmptyCartridgeId IN (185, 186);

    -- Step B: Try VendorId from EmptyCartridge first
    SELECT TOP 1
        @VendorId = ec.VendorId
    FROM dbo.EmptyCartridge ec
    WHERE ec.EmptyCartridgeId IN (185, 186)
      AND ec.VendorId IS NOT NULL;

    -- Step C: VendorId is NULL on both rows — fall back to the vendor
    --         already used in other batches for the same CartridgeModelId
    IF @VendorId IS NULL
        SELECT TOP 1
            @VendorId = vcb.VendorId
        FROM dbo.VendorCartridgeBatch vcb
        WHERE vcb.CartridgeModelId = @CartridgeModelId
        ORDER BY vcb.BatchId DESC;   -- most recent batch for this model

    -- Safety: abort only if we still cannot resolve either value
    IF @CartridgeModelId IS NULL OR @VendorId IS NULL
        RAISERROR('Could not resolve CartridgeModelId or VendorId. Set them manually before running.', 16, 1);

    -- ── STEP 1: Create the retroactive SELL batch ───────────────
    DECLARE @SellBatchId INT;

    INSERT INTO dbo.VendorCartridgeBatch
        (VendorId, CartridgeModelId, OriginalQty, ReturnedQty,
         BatchPurpose, Status, DateReceived, ClosedDate,
         CreatedBy, CreatedDate, Remarks)
    VALUES
        (@VendorId, @CartridgeModelId,
         1,    -- OriginalQty: 1 cartridge sold on 02/23/2026
         1,    -- ReturnedQty: already executed
         'SELL',
         'Sold',
         @DecisionDate,
         @DecisionDate,
         @CreatedBy,
         @DecisionDate,
         'Retroactive batch - SELL decision made on 2026-02-23 before batch was created.');

    SET @SellBatchId = SCOPE_IDENTITY();

    -- ── STEP 2: Create the retroactive DISPOSE batch ────────────
    DECLARE @DisposeBatchId INT;

    INSERT INTO dbo.VendorCartridgeBatch
        (VendorId, CartridgeModelId, OriginalQty, ReturnedQty,
         BatchPurpose, Status, DateReceived, ClosedDate,
         CreatedBy, CreatedDate, Remarks)
    VALUES
        (@VendorId, @CartridgeModelId,
         1,    -- OriginalQty: 1 cartridge disposed on 02/23/2026
         1,
         'DISPOSE',
         'Disposed',
         @DecisionDate,
         @DecisionDate,
         @CreatedBy,
         @DecisionDate,
         'Retroactive batch - DISPOSE decision made on 2026-02-23 before batch was created.');

    SET @DisposeBatchId = SCOPE_IDENTITY();

    -- ── STEP 3: Link EmptyCartridge rows to their new batches ───
    -- EmptyCartridgeId 186 = SELL decision (DecisionId 1)
    UPDATE dbo.EmptyCartridge
    SET    VendorBatchId = @SellBatchId,
           DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
           ModifiedBy    = @CreatedBy
    WHERE  EmptyCartridgeId = 186
      AND  VendorBatchId IS NULL;     -- safety: only update if still NULL

    -- EmptyCartridgeId 185 = DISPOSE decision (DecisionId 2)
    UPDATE dbo.EmptyCartridge
    SET    VendorBatchId = @DisposeBatchId,
           DateModified  = SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time',
           ModifiedBy    = @CreatedBy
    WHERE  EmptyCartridgeId = 185
      AND  VendorBatchId IS NULL;

    -- ── Verify before committing ────────────────────────────────
    SELECT
        ec.EmptyCartridgeId,
        ec.VendorBatchId       AS NewBatchId,
        vcb.BatchPurpose,
        vcb.Status             AS BatchStatus,
        vcb.DateReceived,
        cm.ModelNumber         AS CartridgeModel
    FROM dbo.EmptyCartridge         ec
    JOIN dbo.VendorCartridgeBatch   vcb ON vcb.BatchId         = ec.VendorBatchId
    JOIN dbo.CartridgeModel         cm  ON cm.CartridgeModelId = ec.CartridgeModelId
    WHERE ec.EmptyCartridgeId IN (185, 186);

    COMMIT TRANSACTION;
    PRINT 'Success. SELL BatchId = ' + CAST(@SellBatchId AS VARCHAR)
        + ', DISPOSE BatchId = '     + CAST(@DisposeBatchId AS VARCHAR);

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'ERROR: ' + ERROR_MESSAGE();
END CATCH;


-- ──────────────────────────────────────────────────────────────
-- STEP 4: Confirm the report view now returns BatchId
-- Run this after the fix to verify vw_DisposedSoldItems shows
-- the new BatchId for the 02/23/2026 records.
-- ──────────────────────────────────────────────────────────────

SELECT
    v.BatchId,
    v.DecisionTypeName     AS [Type],
    CAST(
        CAST(v.DecidedAt AS DATETIMEOFFSET) AT TIME ZONE 'UTC'
        AT TIME ZONE 'Singapore Standard Time'
    AS DATE)               AS [Date_SGT],
    v.CartridgeModelNumber AS CartridgeModel,
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
