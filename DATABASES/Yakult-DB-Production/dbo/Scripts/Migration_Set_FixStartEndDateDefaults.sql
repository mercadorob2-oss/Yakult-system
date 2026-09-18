/* =====================================================================================
   Migration_Set_FixStartEndDateDefaults.sql
   =====================================================================================
   Problem:
     dbo.[Set].StartDate and EndDate both had DEFAULT constraints set to the current
     Singapore timestamp. Since CreateSetAsync never explicitly inserts these columns,
     SQL Server auto-filled them with the same timestamp as DispatchDate on every new
     row — making all three columns appear identical and meaningless.

   Fix (2 parts):
     1. Drop the DEFAULT constraints so new sets get NULL instead of a spurious timestamp.
     2. Back-fill existing sets whose StartDate/EndDate are clearly still the
        auto-defaulted value (detectable when both equal each other within 1 second),
        using MIN(Item.WarrantyStartDate) and MAX(Item.WarrantyEndDate) from the
        items associated with that set via Request.

        Item.WarrantyEndDate is the computed column (WarrantyStartDate + WarrantyYears)
        shown as "Warranty End" in the Edit Item dialog. Only items with WarrantyYears > 0
        and a non-NULL WarrantyStartDate contribute.

   Safe to re-run:
     The DEFAULT-drop is guarded by an existence check.
     The UPDATE only touches rows that still carry the spurious default values
     (StartDate ≈ EndDate within 1 second) AND whose linked items have explicit dates.
   ===================================================================================== */

-- ── Part 1: Drop spurious DEFAULT constraints ────────────────────────────────────────

IF EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_Set_StartDate'
      AND parent_object_id = OBJECT_ID('dbo.[Set]')
)
BEGIN
    ALTER TABLE dbo.[Set] DROP CONSTRAINT [DF_Set_StartDate];
    PRINT 'Dropped DF_Set_StartDate';
END
ELSE
    PRINT 'DF_Set_StartDate already removed — skipping';

IF EXISTS (
    SELECT 1 FROM sys.default_constraints
    WHERE name = 'DF_Set_EndDate'
      AND parent_object_id = OBJECT_ID('dbo.[Set]')
)
BEGIN
    ALTER TABLE dbo.[Set] DROP CONSTRAINT [DF_Set_EndDate];
    PRINT 'Dropped DF_Set_EndDate';
END
ELSE
    PRINT 'DF_Set_EndDate already removed — skipping';

-- ── Part 2: Back-fill wrongly-defaulted rows ─────────────────────────────────────────
--
-- A set is considered "wrongly defaulted" when StartDate and EndDate are within
-- 1 second of each other, which is the signature of both being auto-filled by the
-- DEFAULT constraint at INSERT time.
--
-- Sets whose dates were intentionally set (e.g. via the Renewal flow) will have
-- StartDate and EndDate far apart (years), so they are not touched.

UPDATE s
SET
    s.StartDate = derived.MinWarrantyStart,
    s.EndDate   = derived.MaxWarrantyEnd
FROM dbo.[Set] s
JOIN (
    SELECT
        r.SetId,
        MIN(i.WarrantyStartDate) AS MinWarrantyStart,
        MAX(i.WarrantyEndDate)   AS MaxWarrantyEnd
    FROM dbo.Request r
    JOIN dbo.Item i ON i.ItemId = r.ItemId
    WHERE r.SetId IS NOT NULL
      AND i.WarrantyStartDate IS NOT NULL  -- only items with an explicit warranty start
      AND i.WarrantyYears > 0              -- and a non-zero warranty period
    GROUP BY r.SetId
) derived ON derived.SetId = s.SetId
WHERE
    -- Target sets that have not yet been given meaningful dates, i.e.:
    --   (a) both NULL  — DEFAULT was already removed or never existed in this environment
    --   (b) both equal within 1 second — DEFAULT auto-filled same timestamp at INSERT time
    (
        (s.StartDate IS NULL AND s.EndDate IS NULL)
        OR (
            s.StartDate IS NOT NULL
            AND s.EndDate IS NOT NULL
            AND ABS(DATEDIFF(SECOND, s.StartDate, s.EndDate)) < 2
        )
    )
    -- And there are actual warranty dates to use
    AND derived.MaxWarrantyEnd IS NOT NULL;

PRINT CONCAT('Updated ', @@ROWCOUNT, ' set(s) with item warranty start/end dates.');

-- Sets where linked items have no warranty (WarrantyYears = 0 or WarrantyStartDate NULL)
-- will be left with NULL after the DEFAULT is dropped.
-- The application handles NULL gracefully — EffectiveExpiry falls back to CreatedAt + 5 years.
