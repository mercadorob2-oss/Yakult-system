-- Migration: sp_RepairPortal_SetTicketStatus now reverts the item's Condition back to "Good" when
-- a ticket is marked Completed — the mirror image of sp_RepairPortal_CreateTicket auto-marking the
-- item Damaged when the ticket was first created (see Migration_RepairPortal_CreateTicket_
-- AutoMarkDamaged.sql). "Mark Completed" IS this system's "Mark as Repaired" — there's no separate
-- button for it. Marking Unrepairable deliberately does NOT touch Condition — the item is still
-- broken, just not fixable, so it should stay Damaged.
-- CREATE OR ALTER is idempotent — safe to re-run. Carries forward the full current body of the
-- proc from Migration_RepairPortal_SetTicketStatus_AddCompletedAtOverride.sql.

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

    -- Mirror of the auto-Damaged flip in sp_RepairPortal_CreateTicket: "Completed" means the item
    -- was successfully repaired, so it goes back into service as Good. Only touches items that
    -- aren't already Good, so completing an already-Good item's ticket doesn't spam Item history.
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
