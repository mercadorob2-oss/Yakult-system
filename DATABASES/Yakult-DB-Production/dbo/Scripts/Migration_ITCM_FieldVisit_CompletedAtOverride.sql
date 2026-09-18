-- Migration: Allow backdated completion times on field visits.
-- Completing a visit previously always stamped CompletedAt = SYSUTCDATETIME(),
-- so work finished yesterday but logged today recorded the wrong date.
-- Adds optional @CompletedAtOverride (UTC): when supplied with a Completed
-- transition it is stored instead, after validation (not in the future).
-- NULL (all existing callers) preserves current behavior exactly.
-- Idempotent (CREATE OR ALTER, no table change) - safe to re-run.
GO
CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_SetStatus
    @FieldVisitId INT,
    @NewStatus NVARCHAR(20),
    @ChangedByUserId INT = NULL,
    @Notes NVARCHAR(2000) = NULL,
    @TechnicianEmpId INT = NULL,
    @ScheduledAt DATETIME2(2) = NULL,
    @CompletedAtOverride DATETIME2(2) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @TicketId INT, @OldStatus NVARCHAR(20);
    SELECT @TicketId=TicketId, @OldStatus=Status FROM dbo.CallFieldVisit WITH (UPDLOCK, HOLDLOCK) WHERE FieldVisitId=@FieldVisitId;
    IF @TicketId IS NULL THROW 51013, 'Field visit not found.', 1;

    -- Completed stays terminal. Cancelled is reschedulable.
    IF @OldStatus = 'Completed' THROW 51014, 'Completed visits cannot be changed.', 1;
    IF @OldStatus = 'Cancelled' AND @NewStatus <> 'Scheduled' THROW 51014, 'Cancelled visits can only be rescheduled to Scheduled.', 1;
    IF NOT (
        (@OldStatus = 'Scheduled' AND @NewStatus IN ('Completed', 'Cancelled'))
        OR (@OldStatus = 'Cancelled' AND @NewStatus = 'Scheduled')
    ) THROW 51015, 'Invalid status transition.', 1;

    IF @TechnicianEmpId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId=@TechnicianEmpId AND ISNULL(Active,1)=1)
        THROW 51012, 'Technician not found or inactive.', 1;

    IF @NewStatus = 'Completed' AND @CompletedAtOverride IS NOT NULL AND @CompletedAtOverride > SYSUTCDATETIME()
        THROW 51016, 'Completed date cannot be in the future.', 1;

    UPDATE dbo.CallFieldVisit
    SET Status=@NewStatus,
        TechnicianEmpId = COALESCE(@TechnicianEmpId, TechnicianEmpId),
        ScheduledAt = COALESCE(@ScheduledAt, ScheduledAt),
        CompletedAt = CASE
            WHEN @NewStatus='Completed' THEN COALESCE(@CompletedAtOverride, sysutcdatetime())
            WHEN @NewStatus='Scheduled' THEN NULL
            ELSE CompletedAt
        END,
        Notes = CASE
            WHEN @Notes IS NULL THEN Notes
            WHEN Notes IS NULL OR LTRIM(RTRIM(Notes)) = '' THEN @Notes
            ELSE Notes + CHAR(13) + CHAR(10) + @Notes
        END
    WHERE FieldVisitId=@FieldVisitId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'FieldVisitStatus', @OldStatus, @NewStatus, @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CompletedAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId=@FieldVisitId;
END
GO
PRINT 'sp_Call_FieldVisit_SetStatus updated (@CompletedAtOverride for backdated completion).';
GO
