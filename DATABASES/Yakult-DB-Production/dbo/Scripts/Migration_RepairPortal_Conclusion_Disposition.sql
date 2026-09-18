-- Migration_RepairPortal_Conclusion_Disposition.sql
-- Adds "Unrepairable" disposition tracking to the Repair Conclusion workflow: once a ticket's
-- Status = 'Unrepairable', the technician picks Discard (item retired) or Replace (item retired
-- + an existing in-stock item auto-issued to the original requester via a spawned Request/Set).
-- The choice is always changeable — RepairTicketRepository.Disposition.cs reverses the prior
-- choice's side effects (ItemLifecycleDecisionRepository.ReverseDecisionAsync, and for Replace,
-- SetRepository.DeleteSetAndRestoreStock + RequestRepository.DeleteRequest) before re-recording
-- here, so these columns always reflect the currently-executed state, not just the latest click.
-- Idempotent (guarded), safe to re-run.

-- ── 1. dbo.RepairConclusion disposition columns ───────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'Disposition'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD Disposition NVARCHAR(20) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_RepairConclusion_Disposition'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion
        ADD CONSTRAINT CK_RepairConclusion_Disposition CHECK (Disposition IS NULL OR Disposition IN ('Discard', 'Replace'));
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'DispositionItemId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD DispositionItemId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RepairConclusion_DispositionItem'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion
        ADD CONSTRAINT FK_RepairConclusion_DispositionItem FOREIGN KEY (DispositionItemId) REFERENCES dbo.Item (ItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'DispositionDecidedByUserId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD DispositionDecidedByUserId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RepairConclusion_DispositionDecidedByUser'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion
        ADD CONSTRAINT FK_RepairConclusion_DispositionDecidedByUser FOREIGN KEY (DispositionDecidedByUserId) REFERENCES dbo.[User] (UserId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'DispositionDecidedAt'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD DispositionDecidedAt DATETIME2(2) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'DispositionExecutedAt'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD DispositionExecutedAt DATETIME2(2) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'ReplacementItemId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD ReplacementItemId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_RepairConclusion_ReplacementItem'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion
        ADD CONSTRAINT FK_RepairConclusion_ReplacementItem FOREIGN KEY (ReplacementItemId) REFERENCES dbo.Item (ItemId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'ReplacementRequestId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD ReplacementRequestId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.RepairConclusion') AND name = 'ReplacementSetId'
)
BEGIN
    ALTER TABLE dbo.RepairConclusion ADD ReplacementSetId INT NULL;
END
GO

-- ── 2. sp_RepairPortal_SetDisposition — upsert the recorded disposition state ─────────────────
-- Only records state (RepairConclusion row + RepairTicketHistory audit entry) inside one
-- transaction. The actual side effects (archiving/reactivating the Item, spawning/deleting the
-- replacement Request+Set) are orchestrated in C# (RepairTicketRepository.Disposition.cs) because
-- they span ItemLifecycleDecisionRepository/RequestRepository/SetRepository, each with their own
-- connections/transactions — same separation of concerns as LinkNewItemToRequestedBySetAsync.
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetDisposition
    @RepairTicketId        INT,
    @Disposition            NVARCHAR(20),
    @DispositionItemId      INT,
    @DecidedByUserId        INT = NULL,
    @ReplacementItemId      INT = NULL,
    @ReplacementRequestId   INT = NULL,
    @ReplacementSetId       INT = NULL,
    @Note                   NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @Disposition NOT IN ('Discard', 'Replace')
        THROW 51180, 'Invalid disposition. Must be Discard or Replace.', 1;

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
            DispositionExecutedAt = SYSUTCDATETIME(),
            ReplacementItemId = @ReplacementItemId,
            ReplacementRequestId = @ReplacementRequestId,
            ReplacementSetId = @ReplacementSetId,
            UpdatedAt = SYSUTCDATETIME()
        WHERE RepairTicketId = @RepairTicketId;
    END
    ELSE
    BEGIN
        INSERT dbo.RepairConclusion
            (RepairTicketId, Disposition, DispositionItemId, DispositionDecidedByUserId, DispositionDecidedAt,
             DispositionExecutedAt, ReplacementItemId, ReplacementRequestId, ReplacementSetId)
        VALUES
            (@RepairTicketId, @Disposition, @DispositionItemId, @DecidedByUserId, SYSUTCDATETIME(),
             SYSUTCDATETIME(), @ReplacementItemId, @ReplacementRequestId, @ReplacementSetId);
    END

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @DecidedByUserId, 'Disposition', @OldDisposition, @Disposition, @Note);

    COMMIT;
END
GO
