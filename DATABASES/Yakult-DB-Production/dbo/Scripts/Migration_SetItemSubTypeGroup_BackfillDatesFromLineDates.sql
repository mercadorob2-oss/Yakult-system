-- Corrective follow-up to Migration_SetItemSubTypeGroup_Backfill.sql. That backfill only
-- copied whatever was already sitting in dbo.SetItem.BeginDate/EndDate into the new group
-- rows -- but for any SetItem row inserted BEFORE the ServiceSetRepository fix that started
-- writing BeginDate/EndDate, those columns were NULL (only LineStartDate/LineEndDate were
-- ever populated). Result: pre-existing invoices' Sub-Type Group cards show "No date range"
-- even though the correct dates were sitting right there in LineStartDate/LineEndDate the
-- whole time.
--
-- This script:
--   1. Fills dbo.SetItem.BeginDate/EndDate from LineStartDate/LineEndDate wherever still NULL
--      on a grouped row (SubType + ReferenceCode set) -- mirrors exactly what
--      InsertSoftwareServiceSet/UpdateSoftwareServiceSet write for new/edited invoices today.
--   2. Propagates the corrected dates onto any dbo.SetItemSubTypeGroup row that still has
--      NULL BeginDate/EndDate, using its linked SetItem rows.
-- Idempotent: only touches rows still at NULL, safe to re-run.
-- Run after Migration_SetItemSubTypeGroup_Backfill.sql.

UPDATE si
SET
    si.BeginDate = ISNULL(si.BeginDate, si.LineStartDate),
    si.EndDate   = ISNULL(si.EndDate, si.LineEndDate)
FROM dbo.SetItem si
WHERE si.SubType IS NOT NULL
  AND si.ReferenceCode IS NOT NULL
  AND (si.BeginDate IS NULL OR si.EndDate IS NULL);
GO

UPDATE g
SET
    g.BeginDate = ISNULL(g.BeginDate, x.BeginDate),
    g.EndDate   = ISNULL(g.EndDate, x.EndDate)
FROM dbo.SetItemSubTypeGroup g
CROSS APPLY (
    SELECT TOP 1 si.BeginDate, si.EndDate
    FROM dbo.SetItem si
    WHERE si.GroupId = g.GroupId
      AND (si.BeginDate IS NOT NULL OR si.EndDate IS NOT NULL)
    ORDER BY si.SetItemId
) x
WHERE g.BeginDate IS NULL OR g.EndDate IS NULL;
GO
