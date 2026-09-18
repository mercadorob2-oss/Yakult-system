-- Migration_RepairPortal_SetTicketStatus_SkipConditionResetParam.sql
-- Adds @SkipConditionReset to sp_RepairPortal_SetTicketStatus so RepairTicketRepository.
-- Disposition.cs can auto-transition an Unrepairable ticket to Completed once its disposition
-- (Discard/Replace) resolves, WITHOUT triggering the existing Completed branch's "reset
-- Item.ConditionID back to Good" side effect — that side effect is correct for a genuine repair
-- (Testing -> Mark as Repaired) but wrong here: a disposition-resolved item is archived
-- (Discard) or replaced-and-archived (Replace), and flipping its Condition to "Good" would
-- corrupt the record and hide that it was ever damaged/unrepairable if later reactivated via
-- Unlink (ClearDispositionAsync).
-- Carries forward the full current body of sp_RepairPortal_SetTicketStatus from
-- Migration_RepairPortal_SetTicketStatus_RevertRepairedToGoodCondition.sql, unchanged except for
-- the new parameter and its guard on the existing Completed branch.
-- Idempotent (CREATE OR ALTER), safe to re-run.

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetTicketStatus
    @RepairTicketId       INT,
    @NewStatus             NVARCHAR(20),
    @ChangedByUserId       INT            = NULL,
    @Note                  NVARCHAR(2000) = NULL,
    @CompletedAtOverride   DATETIME2      = NULL,
    @SkipConditionReset    BIT            = 0
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
    -- into service as Good. Skipped when @SkipConditionReset = 1 (disposition-driven
    -- auto-completion — see RepairTicketRepository.Disposition.cs's SetDispositionAsync).
    IF @NewStatusCanonical = 'Completed' AND @SkipConditionReset = 0
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
