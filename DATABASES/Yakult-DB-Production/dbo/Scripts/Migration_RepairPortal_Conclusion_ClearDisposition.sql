-- Migration_RepairPortal_Conclusion_ClearDisposition.sql
-- Adds "Unlink" support to the Unrepairable disposition workflow: RepairTicketRepository.
-- ClearDispositionAsync reverses an already-executed Discard/Replace's side effects (same
-- reversal path SetDispositionAsync already used when switching choices), then calls this proc
-- with @Disposition = NULL to clear the recorded state back to "no disposition recorded yet."
-- Carries forward the full current body of sp_RepairPortal_SetDisposition from
-- Migration_RepairPortal_Conclusion_Disposition.sql, only relaxing the @Disposition validation
-- and NULL-clear branch. No schema changes — same RepairConclusion columns as before.
-- Idempotent (CREATE OR ALTER), safe to re-run. Must run AFTER Conclusion_Disposition.

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetDisposition
    @RepairTicketId        INT,
    @Disposition            NVARCHAR(20) = NULL,
    @DispositionItemId      INT = NULL,
    @DecidedByUserId        INT = NULL,
    @ReplacementItemId      INT = NULL,
    @ReplacementRequestId   INT = NULL,
    @ReplacementSetId       INT = NULL,
    @Note                   NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Disposition IS NOT NULL AND @Disposition NOT IN ('Discard', 'Replace')
        THROW 51180, 'Invalid disposition. Must be Discard, Replace, or NULL (clear).', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51181, 'Repair ticket not found.', 1;

    DECLARE @OldDisposition NVARCHAR(20);

    BEGIN TRAN;

    SELECT @OldDisposition = Disposition FROM dbo.RepairConclusion WHERE RepairTicketId = @RepairTicketId;

    IF EXISTS (SELECT 1 FROM dbo.RepairConclusion WHERE RepairTicketId = @RepairTicketId)
    BEGIN
        UPDATE dbo.RepairConclusion
        SET Disposition = @Disposition,
            DispositionItemId = @DispositionItemId,
            DispositionDecidedByUserId = @DecidedByUserId,
            DispositionDecidedAt = SYSUTCDATETIME(),
            DispositionExecutedAt = CASE WHEN @Disposition IS NULL THEN NULL ELSE SYSUTCDATETIME() END,
            ReplacementItemId = @ReplacementItemId,
            ReplacementRequestId = @ReplacementRequestId,
            ReplacementSetId = @ReplacementSetId,
            UpdatedAt = SYSUTCDATETIME()
        WHERE RepairTicketId = @RepairTicketId;
    END
    ELSE IF @Disposition IS NOT NULL
    BEGIN
        INSERT dbo.RepairConclusion
            (RepairTicketId, Disposition, DispositionItemId, DispositionDecidedByUserId, DispositionDecidedAt,
             DispositionExecutedAt, ReplacementItemId, ReplacementRequestId, ReplacementSetId)
        VALUES
            (@RepairTicketId, @Disposition, @DispositionItemId, @DecidedByUserId, SYSUTCDATETIME(),
             SYSUTCDATETIME(), @ReplacementItemId, @ReplacementRequestId, @ReplacementSetId);
    END
    -- ELSE: clearing a disposition that was never recorded (no RepairConclusion row) — nothing to do.

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @DecidedByUserId, 'Disposition', @OldDisposition, @Disposition, @Note);

    COMMIT;
END
GO
