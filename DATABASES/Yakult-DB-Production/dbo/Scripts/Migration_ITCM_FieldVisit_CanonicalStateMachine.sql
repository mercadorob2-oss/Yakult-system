-- Migration: Canonical field-visit state machine (merges Simplify + Reschedule).
-- The Simplify migration (Scheduled -> Completed/Cancelled only) and the
-- Reschedule migration (plus Cancelled -> Scheduled with tech/date update)
-- each redefined sp_Call_FieldVisit_SetStatus, so deploy order decided
-- whether Reschedule worked. This file is the single canonical definition:
--   Scheduled -> Completed / Cancelled, Cancelled -> Scheduled (reschedule),
--   Completed terminal.
-- Also included:
--   * sp_Call_FieldVisit_Schedule takes UPDLOCK/HOLDLOCK on the one-per-ticket
--     check (kills the double-schedule race that surfaced raw UQ violations),
--     and points Cancelled rows at Reschedule with a dedicated message.
--   * Status-change notes APPEND to visit notes instead of replacing them, so
--     completing a visit no longer destroys the original scheduling reason
--     (each change is still mirrored separately to CallTicketHistory).
-- Idempotent (CREATE OR ALTER, no table change) - safe to re-run.
GO
CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_Schedule
    @TicketId INT,
    @TechnicianEmpId INT = NULL,
    @ScheduledAt DATETIME2(2) = NULL,
    @Notes NVARCHAR(2000) = NULL,
    @CreatedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @TicketId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.CallTicket WHERE TicketId=@TicketId)
        THROW 51010, 'Call ticket not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.CallFieldVisit WITH (UPDLOCK, HOLDLOCK) WHERE TicketId=@TicketId)
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.CallFieldVisit WHERE TicketId=@TicketId AND Status='Cancelled')
            THROW 51011, 'This ticket already has a field visit (Cancelled). Use Reschedule instead of Schedule.', 1;
        THROW 51011, 'This ticket already has a field visit (one per ticket).', 1;
    END
    IF @TechnicianEmpId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId=@TechnicianEmpId AND ISNULL(Active,1)=1)
        THROW 51012, 'Technician not found or inactive.', 1;

    INSERT dbo.CallFieldVisit (TicketId, TechnicianEmpId, Status, ScheduledAt, Notes, CreatedByUserId)
    VALUES (@TicketId, @TechnicianEmpId, 'Scheduled', @ScheduledAt, @Notes, @CreatedByUserId);

    DECLARE @Id INT = SCOPE_IDENTITY();

    -- Mirror to history for timeline in call-ticket-detail
    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @CreatedByUserId, 'FieldVisitStatus', NULL, 'Scheduled', @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CompletedAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id;
END
GO
PRINT 'Canonical sp_Call_FieldVisit_Schedule deployed (race-safe, reschedule hint).';
GO
CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_SetStatus
    @FieldVisitId INT,
    @NewStatus NVARCHAR(20),
    @ChangedByUserId INT = NULL,
    @Notes NVARCHAR(2000) = NULL,
    @TechnicianEmpId INT = NULL,
    @ScheduledAt DATETIME2(2) = NULL
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

    UPDATE dbo.CallFieldVisit
    SET Status=@NewStatus,
        TechnicianEmpId = COALESCE(@TechnicianEmpId, TechnicianEmpId),
        ScheduledAt = COALESCE(@ScheduledAt, ScheduledAt),
        CompletedAt = CASE
            WHEN @NewStatus='Completed' THEN sysutcdatetime()
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
PRINT 'Canonical sp_Call_FieldVisit_SetStatus deployed (reschedule support, append notes).';
GO
