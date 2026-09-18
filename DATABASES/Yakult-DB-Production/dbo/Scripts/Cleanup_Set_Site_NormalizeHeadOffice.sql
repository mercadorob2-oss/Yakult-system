-- ============================================================
-- Cleanup_Set_Site_NormalizeHeadOffice.sql
--
-- 1. Removes the literal word "None" left by the old site
--    builder when a field was left unselected.
-- 2. Normalises branch name variants to "MANILA LIAISON OFFICE":
--      "Head Office"   → "MANILA LIAISON OFFICE"
--      "Manila Office" → "MANILA LIAISON OFFICE"
-- ============================================================

-- ── Preview (run this first) ─────────────────────────────────
SELECT
    SetId,
    SetCode,
    Site AS Site_Before,
    -- Step 1: strip "None" fragments
    -- Step 2: normalise branch name variants
    REPLACE(
        REPLACE(
            LTRIM(RTRIM(
                REPLACE(
                    REPLACE(
                        REPLACE(Site, 'None - ', ''),
                    ' - None', ''),
                'None', '')
            )),
        'Head Office',   'MANILA LIAISON OFFICE'),
    'Manila Office', 'MANILA LIAISON OFFICE')
    AS Site_After
FROM dbo.[Set]
WHERE
    Site LIKE '%None%'
    OR Site LIKE '%Head Office%'
    OR Site LIKE '%Manila Office%'
ORDER BY SetId;

-- ── Apply ────────────────────────────────────────────────────
BEGIN TRANSACTION;

UPDATE dbo.[Set]
SET Site =
    REPLACE(
        REPLACE(
            LTRIM(RTRIM(
                REPLACE(
                    REPLACE(
                        REPLACE(Site, 'None - ', ''),
                    ' - None', ''),
                'None', '')
            )),
        'Head Office',   'MANILA LIAISON OFFICE'),
    'Manila Office', 'MANILA LIAISON OFFICE')
WHERE
    Site LIKE '%None%'
    OR Site LIKE '%Head Office%'
    OR Site LIKE '%Manila Office%';

SELECT @@ROWCOUNT AS RowsUpdated;

-- Spot-check the updated rows
SELECT SetId, SetCode, Site
FROM dbo.[Set]
WHERE SetId IN (
    166, 165, 167, 168, 1210, 1214, 1216, 1218,
    1219, 1221, 1222, 1223, 1225, 1226, 1227,
    1231, 1234, 1236, 1237
)
ORDER BY SetId;

-- COMMIT TRANSACTION;
ROLLBACK TRANSACTION;
