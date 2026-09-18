-- ============================================================
-- Migration: SetItem.ItemCode — guard against empty-string values
-- Date:      2026-02-19
-- Reason:    Cartridge exchanges previously persisted placeholder
--            ItemCode values (empty string / 'UNKNOWN') when the
--            cartridge model could not be resolved at fulfillment time.
--            This caused the "Assign Returns to Refill Batches"
--            feature to fail because returns could not be grouped
--            by model when SetItem.ItemCode was meaningless.
--
-- What this adds:
--   CK_SetItem_ItemCode_NoEmpty — prevents empty-string ('') ItemCode.
--   NULL is still permitted for non-cartridge Set types that may
--   legitimately omit ItemCode.
--
-- Safe to run on live data:
--   Step 1 normalises any existing '' values to NULL before the
--   constraint is added, so the ALTER TABLE will not fail.
-- ============================================================

-- Step 1: Normalise existing empty-string ItemCode rows to NULL
--         (empty string and NULL are semantically equivalent here;
--          the constraint only blocks empty string going forward).
UPDATE dbo.SetItem
SET    ItemCode = NULL
WHERE  LTRIM(RTRIM(ISNULL(ItemCode, 'ok'))) = '';
GO

-- Step 2: Add the CHECK constraint (idempotent — skipped if already present)
IF NOT EXISTS (
    SELECT 1
    FROM   sys.check_constraints
    WHERE  name              = 'CK_SetItem_ItemCode_NoEmpty'
      AND  parent_object_id = OBJECT_ID('dbo.SetItem')
)
BEGIN
    ALTER TABLE dbo.SetItem
    ADD CONSTRAINT CK_SetItem_ItemCode_NoEmpty
        CHECK (ItemCode IS NULL OR LTRIM(RTRIM(ItemCode)) <> '');

    PRINT 'CK_SetItem_ItemCode_NoEmpty added successfully.';
END
ELSE
BEGIN
    PRINT 'CK_SetItem_ItemCode_NoEmpty already exists — skipped.';
END
GO
