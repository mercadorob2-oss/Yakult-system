-- Migration: undoes Migration_RepairPortal_SetTicketStatus_RepairedCondition.sql.
--
-- "Repaired" turned out to be a bad fit for dbo.Item.ConditionID: Condition is a shared lookup
-- that several independent features hardcode as a strict Good(1)/Damaged(2) binary —
-- DashboardService's GoodCount/DamagedCount CASE statements, the View Items grid's LatestStatus
-- CASE, and CartridgeRepository's refill-eligibility filter (WHERE ConditionId = 1) all silently
-- mis-handle any other ConditionId. A repaired item would vanish from Dashboard counts, show a
-- blank Status in View Items, and stay permanently ineligible for cartridge refill.
--
-- "Repaired" as a concept now lives entirely in the Repair Portal's own tables
-- (RepairTicket.Status = 'Completed' + CompletedAt, RepairConclusion) — it does not need to be
-- duplicated into the shared Condition lookup. This migration:
--   1. Restores sp_RepairPortal_SetTicketStatus's Completed branch to flip Condition back to
--      "Good" (the pre-"Repaired" behavior).
--   2. Reassigns any item currently sitting on the "Repaired" condition back to "Good".
--   3. Drops the "Repaired" row from dbo.Condition now that nothing references it.
--
-- Idempotent — safe to re-run.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetTicketStatus
    @RepairTicketId       INT,
    @NewStatus             NVARCHAR(20),
    @ChangedByUserId       INT            = NULL,
    @Note                  NVARCHAR(2000) = NULL,
    @CompletedAtOverride   DATETIME2      = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewStatus = LTRIM(RTRIM(@NewStatus));
    IF @NewStatus IS NULL OR @NewStatus = '' THROW 51010, 'NewStatus is required.', 1;

    DECLARE @NewStatusCanonical NVARCHAR(20) =
        CASE UPPER(@NewStatus)
            WHEN 'WAITING' THEN 'Waiting'
            WHEN 'DIAGNOSING' THEN 'Diagnosing'
            WHEN 'REPAIRING' THEN 'Repairing'
            WHEN 'AWAITINGPARTS' THEN 'AwaitingParts'
            WHEN 'AWAITING PARTS' THEN 'AwaitingParts'
            WHEN 'TESTING' THEN 'Testing'
            WHEN 'COMPLETED' THEN 'Completed'
            WHEN 'UNREPAIRABLE' THEN 'Unrepairable'
            ELSE NULL
        END;

    IF @NewStatusCanonical IS NULL
        THROW 51011, 'Invalid status. Allowed: Waiting, Diagnosing, Repairing, AwaitingParts, Testing, Completed, Unrepairable.', 1;

    DECLARE @OldStatus NVARCHAR(20);
    DECLARE @ItemId INT;

    BEGIN TRAN;

    SELECT @OldStatus = Status, @ItemId = ItemId
    FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairTicketId = @RepairTicketId;

    IF @@ROWCOUNT = 0 THROW 51012, 'Repair ticket not found.', 1;

    IF @OldStatus = @NewStatusCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.RepairTicket
    SET Status = @NewStatusCanonical,
        CompletedAt = CASE
            WHEN @NewStatusCanonical IN ('Completed', 'Unrepairable') THEN COALESCE(@CompletedAtOverride, CompletedAt, SYSUTCDATETIME())
            ELSE NULL
        END
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    -- Mirror of the auto-Damaged flip in sp_RepairPortal_CreateTicket: "Completed" (surfaced to
    -- technicians as "Mark as Repaired") means the item was successfully repaired, so it goes back
    -- into service as Good. The fact that it was specifically *repaired* (vs. never having been
    -- damaged) lives in RepairTicket/RepairConclusion, not in this shared Condition value. Only
    -- touches items that aren't already Good, so completing an already-Good item's ticket doesn't
    -- spam Item history.
    IF @NewStatusCanonical = 'Completed'
    BEGIN
        DECLARE @GoodConditionId INT = (SELECT TOP (1) ConditionId FROM dbo.Condition WHERE ConditionName = 'Good');
        IF @GoodConditionId IS NOT NULL
        BEGIN
            UPDATE dbo.Item
            SET ConditionID = @GoodConditionId,
                DateModified = SYSUTCDATETIME(),
                ModifiedBy = ISNULL(@ChangedByUserId, ModifiedBy)
            WHERE ItemId = @ItemId AND ISNULL(ConditionID, 0) <> @GoodConditionId;
        END
    END

    COMMIT;
END
GO

-- Reassign any item currently sitting on "Repaired" back to "Good".
DECLARE @RepairedConditionId INT = (SELECT TOP (1) ConditionId FROM dbo.Condition WHERE ConditionName = 'Repaired');
DECLARE @GoodConditionId2 INT = (SELECT TOP (1) ConditionId FROM dbo.Condition WHERE ConditionName = 'Good');

IF @RepairedConditionId IS NOT NULL AND @GoodConditionId2 IS NOT NULL
BEGIN
    UPDATE dbo.Item
    SET ConditionID = @GoodConditionId2,
        DateModified = SYSUTCDATETIME()
    WHERE ConditionID = @RepairedConditionId;
END

-- Drop the now-unused "Repaired" row (nothing should reference it after the update above).
IF @RepairedConditionId IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.Item WHERE ConditionID = @RepairedConditionId)
BEGIN
    DELETE FROM dbo.Condition WHERE ConditionID = @RepairedConditionId;
END
