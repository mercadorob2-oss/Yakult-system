-- ============================================================
-- Cleanup_Set_Site_RemoveNoneLiteral.sql
--
-- Removes the literal word "None" from the Site column in
-- dbo.[Set] that was written by the old site builder when
-- a company, branch, or department was left unselected.
--
-- Patterns handled:
--   "None - Branch - Dept"    → "Branch - Dept"
--   "Company - None - Dept"   → "Company - Dept"
--   "Company - Branch - None" → "Company - Branch"
--   "None - Branch"           → "Branch"
--   "Company - None"          → "Company"
--   "None"                    → ""
--   "None - None"             → ""
--   "None - None - None"      → ""
-- ============================================================

-- ── Preview (run this first to see what will be changed) ─────
SELECT
    SetId,
    Site AS Site_Before,
    -- Strip all "None" fragments in order:
    --   1. "None - " at start or mid
    --   2. " - None" at end or mid
    --   3. bare "None" (safety net)
    NULLIF(
        LTRIM(RTRIM(
            REPLACE(
                REPLACE(
                    REPLACE(Site, 'None - ', ''),
                ' - None', ''),
            'None', '')
        )),
    '') AS Site_After
FROM dbo.[Set]
WHERE Site LIKE '%None%'
ORDER BY SetId;

-- ── Apply the fix ────────────────────────────────────────────
BEGIN TRANSACTION;

UPDATE dbo.[Set]
SET Site = NULLIF(
    LTRIM(RTRIM(
        REPLACE(
            REPLACE(
                REPLACE(Site, 'None - ', ''),
            ' - None', ''),
        'None', '')
    )),
'')
WHERE Site LIKE '%None%';

-- Verify
SELECT @@ROWCOUNT AS RowsUpdated;

-- Review the updated rows before committing
SELECT SetId, Site FROM dbo.[Set] WHERE SetId IN (
    SELECT SetId FROM dbo.[Set] WHERE Site NOT LIKE '%None%'
);

-- If everything looks correct, uncomment COMMIT. Otherwise ROLLBACK.
-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
