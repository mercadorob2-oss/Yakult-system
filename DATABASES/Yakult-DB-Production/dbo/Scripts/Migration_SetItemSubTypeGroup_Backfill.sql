-- One-time backfill: for existing dbo.SetItem rows that already have SubType + ReferenceCode
-- populated (from before dbo.SetItemSubTypeGroup existed), create one SetItemSubTypeGroup row
-- per distinct (SetId, SubType, ReferenceCode, BeginDate, EndDate) combination and point the
-- matching SetItem rows' GroupId at it. Safe to re-run: only touches SetItem rows still at
-- GroupId IS NULL, and only creates a SetItemSubTypeGroup row when one matching doesn't
-- already exist (guards against a partial prior run).
-- Run AFTER Migration_SetItem_AddGroupId.sql.

INSERT INTO dbo.SetItemSubTypeGroup (SetId, SubType, ReferenceCode, BeginDate, EndDate, CreatedAt)
SELECT DISTINCT si.SetId, si.SubType, si.ReferenceCode, si.BeginDate, si.EndDate, sysutcdatetime()
FROM dbo.SetItem si
WHERE si.SubType IS NOT NULL
  AND si.ReferenceCode IS NOT NULL
  AND si.GroupId IS NULL
  AND NOT EXISTS (
        SELECT 1 FROM dbo.SetItemSubTypeGroup g
        WHERE g.SetId = si.SetId
          AND g.SubType = si.SubType
          AND g.ReferenceCode = si.ReferenceCode
          AND ISNULL(g.BeginDate, '1900-01-01') = ISNULL(si.BeginDate, '1900-01-01')
          AND ISNULL(g.EndDate, '1900-01-01') = ISNULL(si.EndDate, '1900-01-01')
      );
GO

UPDATE si
SET si.GroupId = g.GroupId
FROM dbo.SetItem si
JOIN dbo.SetItemSubTypeGroup g
    ON g.SetId = si.SetId
   AND g.SubType = si.SubType
   AND g.ReferenceCode = si.ReferenceCode
   AND ISNULL(g.BeginDate, '1900-01-01') = ISNULL(si.BeginDate, '1900-01-01')
   AND ISNULL(g.EndDate, '1900-01-01') = ISNULL(si.EndDate, '1900-01-01')
WHERE si.GroupId IS NULL
  AND si.SubType IS NOT NULL
  AND si.ReferenceCode IS NOT NULL;
GO
