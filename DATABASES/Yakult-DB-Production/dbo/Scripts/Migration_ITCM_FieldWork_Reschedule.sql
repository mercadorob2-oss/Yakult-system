-- Migration: Field Work Cancelled to Scheduled reschedule (F1) with tech/date update (F4 support)
-- Allows: Scheduled to Completed/Cancelled, Cancelled to Scheduled (reopen with new tech/date)
-- Keeps Completed terminal. Clears CompletedAt on reschedule (Q1 option A).
-- Date: 2026-09-03
-- Idempotent, safe to re-run (CREATE OR ALTER, no table change)

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
    SELECT @TicketId = TicketId, @OldStatus = Status
    FROM dbo.CallFieldVisit WITH (UPDLOCK, HOLDLOCK)
    WHERE FieldVisitId = @FieldVisitId;
    IF @TicketId IS NULL THROW 51013, 'Field visit not found.', 1;

    -- Completed stays terminal. Cancelled is reschedulable.
    IF @OldStatus = 'Completed' THROW 51014, 'Completed visits cannot be changed.', 1;
    IF @OldStatus = 'Cancelled' AND @NewStatus <> 'Scheduled' THROW 51014, 'Cancelled visits can only be rescheduled to Scheduled.', 1;
    IF NOT (
        (@OldStatus = 'Scheduled' AND @NewStatus IN ('Completed', 'Cancelled'))
        OR (@OldStatus = 'Cancelled' AND @NewStatus = 'Scheduled')
    ) THROW 51015, 'Invalid status transition.', 1;

    IF @TechnicianEmpId IS NOT NULL
        AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @TechnicianEmpId AND ISNULL(Active, 1) = 1)
        THROW 51012, 'Technician not found or inactive.', 1;

    UPDATE dbo.CallFieldVisit
    SET Status = @NewStatus,
        TechnicianEmpId = COALESCE(@TechnicianEmpId, TechnicianEmpId),
        ScheduledAt = COALESCE(@ScheduledAt, ScheduledAt),
        CompletedAt = CASE
            WHEN @NewStatus = 'Completed' THEN sysutcdatetime()
            WHEN @NewStatus = 'Scheduled' THEN NULL
            ELSE CompletedAt
        END,
        Notes = COALESCE(@Notes, Notes)
    WHERE FieldVisitId = @FieldVisitId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'FieldVisitStatus', @OldStatus, @NewStatus, @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CompletedAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId = @FieldVisitId;
END
GO

PRINT 'Updated sp_Call_FieldVisit_SetStatus with Cancelled to Scheduled reschedule support';
GO
