-- Migration: CompletedAt is now always stamped fresh on every transition INTO a terminal status
-- (Completed/Unrepairable), instead of preserving a stale value from an earlier terminal visit.
--
-- Bug: both sp_RepairPortal_SetTicketStatus and sp_RepairPortal_RollUpTicketStatus used
-- `COALESCE(CompletedAt, SYSUTCDATETIME())` — this only stamps a fresh timestamp if CompletedAt was
-- NULL. But CompletedAt is only cleared when the ticket leaves BOTH terminal statuses for a
-- non-terminal one; bouncing directly between Completed and Unrepairable (a real transition, since
-- old != new, so the guard above it does not short-circuit) hits the UPDATE with a NON-NULL
-- CompletedAt already set from whenever the ticket first became terminal, and COALESCE then just
-- keeps that old value forever. A ticket that was auto-completed by the parts roll-up at some early
-- point, then later actually clicked "Mark as Repaired" by a technician at a real, different time,
-- would keep showing the ORIGINAL stale timestamp on every report generated since.
--
-- Fix: drop the "keep existing CompletedAt" fallback entirely — every transition into a terminal
-- status (there is always a real OldStatus != NewStatus by the time either proc reaches this
-- UPDATE, both already guard on that) now stamps SYSUTCDATETIME() unconditionally. The manual proc
-- still honors an explicit @CompletedAtOverride if the caller ever supplies one.
--
-- CREATE OR ALTER is idempotent — safe to re-run.

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
            WHEN @NewStatusCanonical IN ('Completed', 'Unrepairable') THEN COALESCE(@CompletedAtOverride, SYSUTCDATETIME())
            ELSE NULL
        END
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    -- Mirror of the auto-Damaged flip in sp_RepairPortal_CreateTicket: "Completed" (surfaced to
    -- technicians as "Mark as Repaired") means the item was successfully repaired, so it goes back
    -- into service as Good. Only touches items that aren't already Good, so completing an
    -- already-Good item's ticket doesn't spam Item history.
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

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_RollUpTicketStatus
    @RepairTicketId   INT,
    @ChangedByUserId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairPart WHERE RepairTicketId = @RepairTicketId)
        RETURN; -- no parts yet: ticket Status stays whatever it was (manual)

    DECLARE @PartCount INT, @RepairedCount INT, @CannotRepairCount INT, @TerminalCount INT;

    SELECT
        @PartCount = COUNT(*),
        @RepairedCount = SUM(CASE WHEN Status = 'Repaired' THEN 1 ELSE 0 END),
        @CannotRepairCount = SUM(CASE WHEN Status = 'CannotRepair' THEN 1 ELSE 0 END)
    FROM dbo.RepairPart
    WHERE RepairTicketId = @RepairTicketId;

    SET @TerminalCount = @RepairedCount + @CannotRepairCount;

    DECLARE @NewStatus NVARCHAR(20);

    IF @TerminalCount = @PartCount
    BEGIN
        -- All parts are terminal: Completed if any part was actually repaired, else every
        -- part was unrepairable.
        SET @NewStatus = CASE WHEN @RepairedCount > 0 THEN 'Completed' ELSE 'Unrepairable' END;
    END
    ELSE
    BEGIN
        -- >= 1 active part: show the least-advanced one (lowest rank wins).
        SELECT TOP (1) @NewStatus =
            CASE Status
                WHEN 'WaitingDiagnosis' THEN 'Waiting'
                WHEN 'Diagnosing'       THEN 'Diagnosing'
                WHEN 'Repairing'        THEN 'Repairing'
                WHEN 'WaitingParts'     THEN 'AwaitingParts'
                WHEN 'Testing'          THEN 'Testing'
            END
        FROM dbo.RepairPart
        WHERE RepairTicketId = @RepairTicketId
          AND Status NOT IN ('Repaired', 'CannotRepair')
        ORDER BY
            CASE Status
                WHEN 'WaitingDiagnosis' THEN 0
                WHEN 'Diagnosing'       THEN 1
                WHEN 'Repairing'        THEN 2
                WHEN 'WaitingParts'     THEN 3
                WHEN 'Testing'          THEN 4
                ELSE 5
            END ASC;
    END

    DECLARE @OldStatus NVARCHAR(20);
    SELECT @OldStatus = Status FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId;

    IF @NewStatus IS NULL OR @NewStatus = @OldStatus
        RETURN;

    UPDATE dbo.RepairTicket
    SET Status = @NewStatus,
        CompletedAt = CASE WHEN @NewStatus IN ('Completed', 'Unrepairable') THEN SYSUTCDATETIME() ELSE NULL END
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatus, 'Auto (parts roll-up)');
END
GO

-- Deliberately NO blanket data fix here — we don't know which existing tickets' CompletedAt is
-- genuinely stale vs. genuinely correct, and overwriting every terminal ticket's CompletedAt to
-- "now" would corrupt the ones that are already accurate. For a specific ticket known to be wrong
-- (e.g. RPR-000004), the fix is to flip its status away from Completed/Unrepairable and back —
-- with this migration applied, that re-stamps CompletedAt correctly at the moment of the click.
