-- ============================================================
-- Migration: EmptyCartridge — add ConditionStatus column
-- Date:      2026-02-24
-- Reason:    Damaged empty cartridges must be excluded from
--            refill-batch assignment.  A denormalised VARCHAR
--            column ('GOOD' | 'DAMAGED' | NULL) avoids a JOIN
--            to dbo.Condition in every refill-queue query and
--            mirrors the pattern used by RefillStatus.
--
--            NULL means "condition not recorded" (legacy rows
--            inserted before this migration).  The refill-
--            assignment query treats NULL as non-damaged so
--            that existing unassigned empties continue to flow
--            through the batch-assignment wizard unchanged.
-- ============================================================

-- 1. Add the column (nullable, no default — backfill below)
ALTER TABLE [dbo].[EmptyCartridge]
    ADD [ConditionStatus] VARCHAR(10) NULL;
GO

-- 2. Enforce allowed values
ALTER TABLE [dbo].[EmptyCartridge]
    ADD CONSTRAINT [CK_EmptyCartridge_ConditionStatus]
    CHECK ([ConditionStatus] = 'GOOD' OR [ConditionStatus] = 'DAMAGED' OR [ConditionStatus] IS NULL);
GO

-- 3a. Backfill rows that have an explicit Damaged or Good ConditionId.
UPDATE ec
SET ec.ConditionStatus = CASE c.ConditionName
                             WHEN 'Damaged' THEN 'DAMAGED'
                             ELSE               'GOOD'
                         END
FROM dbo.EmptyCartridge ec
INNER JOIN dbo.Condition c ON c.ConditionID = ec.ConditionId
WHERE ec.ConditionStatus IS NULL;
GO

-- 3b. Any remaining NULL rows have no ConditionId (legacy records inserted
--     before the Condition lookup was used).  They were never flagged as
--     damaged, so they are safe to treat as GOOD.
UPDATE dbo.EmptyCartridge
SET ConditionStatus = 'GOOD'
WHERE ConditionStatus IS NULL;
GO

-- 4. Partial index — accelerates the refill-queue query that filters
--    WHERE ConditionStatus IS NOT NULL (i.e. explicitly GOOD or DAMAGED)
CREATE NONCLUSTERED INDEX [IX_EmptyCartridge_ConditionStatus]
    ON [dbo].[EmptyCartridge]([ConditionStatus] ASC)
    WHERE ([ConditionStatus] IS NOT NULL);
GO
