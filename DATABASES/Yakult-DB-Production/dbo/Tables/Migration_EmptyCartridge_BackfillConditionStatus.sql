-- ============================================================
-- Migration: EmptyCartridge — backfill ConditionStatus
-- Date:      2026-02-24
-- Reason:    The original AddConditionStatus migration only set
--            'GOOD'/'DAMAGED' for rows whose ConditionId matched
--            a 'Good' or 'Damaged' Condition row.  Rows with an
--            EMPTY ConditionId or a NULL ConditionId were left as
--            NULL and therefore invisible to the Damaged Empty
--            Cartridges page and excluded from refill-queue
--            queries that filter ISNULL(ConditionStatus,'GOOD').
--
--            Run this script on any environment where
--            Migration_EmptyCartridge_AddConditionStatus.sql has
--            already been applied.
-- ============================================================

-- Step 1: Rows with an explicit ConditionId.
--   Damaged condition  → 'DAMAGED'
--   Good, EMPTY, other → 'GOOD'  (only Damaged is ever harmful)
UPDATE ec
SET ec.ConditionStatus = CASE c.ConditionName
                             WHEN 'Damaged' THEN 'DAMAGED'
                             ELSE               'GOOD'
                         END
FROM dbo.EmptyCartridge ec
INNER JOIN dbo.Condition c ON c.ConditionID = ec.ConditionId
WHERE ec.ConditionStatus IS NULL;
GO

-- Step 2: Rows with no ConditionId at all (inserted before the
--         Condition FK was in use).  Never explicitly damaged → 'GOOD'.
UPDATE dbo.EmptyCartridge
SET ConditionStatus = 'GOOD'
WHERE ConditionStatus IS NULL;
GO

-- Verify — should return 0 after this script completes.
SELECT COUNT(*) AS RemainingNullConditionStatus
FROM   dbo.EmptyCartridge
WHERE  ConditionStatus IS NULL;
GO
