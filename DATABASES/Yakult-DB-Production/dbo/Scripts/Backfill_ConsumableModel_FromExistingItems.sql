-- Backfill: Create ConsumableModel rows from existing Ink/Toner/Print Head Items
-- Purpose : Retroactively groups every existing Ink/Toner/Print Head dbo.Item row by
--           (Name, canonical category) into a single dbo.ConsumableModel, then links
--           each Item row to it. This is what actually fixes today's data — e.g. the
--           four EPSON 001 color variants and the two duplicate HP CF276X rows — so
--           their stock is summed through an explicit relationship going forward
--           instead of accidental Name/Category string matching.
-- Note    : Safe to re-run. Category is normalized to one of the three canonical
--           labels (Ink / Toner / Print Head) regardless of how it was originally
--           typed/spaced/cased, matching the fuzzy filter used everywhere else
--           (RequestRepository.FulfillmentTrackedRequestsCte).

;WITH ScopedItems AS (
    SELECT
        i.ItemId,
        i.Name,
        CASE
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'       THEN 'Ink'
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'     THEN 'Toner'
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%' THEN 'Print Head'
        END AS CanonicalCategory
    FROM dbo.Item i
    WHERE i.Active = 1
      AND i.ConsumableModelId IS NULL
      AND (
            REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
          )
)
INSERT INTO dbo.ConsumableModel (ModelNumber, Category, CreatedBy)
SELECT DISTINCT si.Name, si.CanonicalCategory, 1 -- system/backfill user
FROM ScopedItems si
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ConsumableModel cm
    WHERE cm.ModelNumber = si.Name AND cm.Category = si.CanonicalCategory
);

;WITH ScopedItems AS (
    SELECT
        i.ItemId,
        i.Name,
        CASE
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'       THEN 'Ink'
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'     THEN 'Toner'
            WHEN REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%' THEN 'Print Head'
        END AS CanonicalCategory
    FROM dbo.Item i
    WHERE i.Active = 1
      AND i.ConsumableModelId IS NULL
      AND (
            REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
         OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
          )
)
UPDATE i
SET i.ConsumableModelId = cm.ConsumableModelId
FROM dbo.Item i
INNER JOIN ScopedItems si ON si.ItemId = i.ItemId
INNER JOIN dbo.ConsumableModel cm ON cm.ModelNumber = si.Name AND cm.Category = si.CanonicalCategory;

PRINT 'Backfilled ConsumableModel rows and linked existing Ink/Toner/Print Head Items.';
GO
