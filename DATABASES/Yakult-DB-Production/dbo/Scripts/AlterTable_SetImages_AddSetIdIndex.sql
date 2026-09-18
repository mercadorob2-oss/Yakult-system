-- SetId has a FK to dbo.Set but was never indexed, so every per-Set image lookup
-- (GetSetImagesAsync, ImageCount subqueries on the Set list/detail pages) scans the
-- whole table. Now that rows carry full image bytes instead of short paths, that scan
-- is more expensive to read past.

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_SetImages_SetId'
      AND object_id = OBJECT_ID('dbo.SetImages')
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.stats
    WHERE name = 'IX_SetImages_SetId'
      AND object_id = OBJECT_ID('dbo.SetImages')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_SetImages_SetId
        ON dbo.SetImages (SetId);
END
GO
