-- Migration: Simplify Field Work State Machine
-- Remove InTransit, CheckedIn, CheckedOut intermediate states
-- Keep only: Scheduled → Completed / Cancelled
-- Remove time tracking fields (CheckedInAt, CheckedOutAt)
-- Keep ScheduledAt and add CompletedAt for simple date tracking
-- Date: 2026-09-01
-- Idempotent — safe to re-run

-- Step 1: Add CompletedAt column if it doesn't exist
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CallFieldVisit') AND name = 'CompletedAt')
BEGIN
    ALTER TABLE dbo.CallFieldVisit ADD CompletedAt DATETIME2(2) NULL;
    PRINT 'Added CompletedAt column to CallFieldVisit';
END
GO

-- Step 2: Backfill CompletedAt from CheckedOutAt for existing completed visits
UPDATE dbo.CallFieldVisit
SET CompletedAt = CheckedOutAt
WHERE Status = 'Completed' 
  AND CompletedAt IS NULL 
  AND CheckedOutAt IS NOT NULL;
PRINT 'Backfilled CompletedAt from CheckedOutAt';
GO

-- Step 3: Migrate any visits in intermediate states to appropriate final states
-- InTransit or CheckedIn → back to Scheduled
-- CheckedOut → Completed
UPDATE dbo.CallFieldVisit
SET Status = 'Scheduled'
WHERE Status IN ('InTransit', 'CheckedIn');

UPDATE dbo.CallFieldVisit
SET Status = 'Completed',
    CompletedAt = ISNULL(CompletedAt, CheckedOutAt)
WHERE Status = 'CheckedOut';
PRINT 'Migrated intermediate states to Scheduled or Completed';
GO

-- Step 4: Update check constraint to only allow Scheduled, Completed, Cancelled
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_CallFieldVisit_Status' AND parent_object_id = OBJECT_ID('dbo.CallFieldVisit'))
BEGIN
    ALTER TABLE dbo.CallFieldVisit DROP CONSTRAINT CK_CallFieldVisit_Status;
    PRINT 'Dropped old status check constraint';
END
GO

ALTER TABLE dbo.CallFieldVisit 
ADD CONSTRAINT CK_CallFieldVisit_Status CHECK (Status IN ('Scheduled','Completed','Cancelled'));
PRINT 'Added simplified status check constraint';
GO

-- Step 5: Recreate stored procedure with simplified logic
CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_SetStatus
    @FieldVisitId INT,
    @NewStatus NVARCHAR(20),
    @ChangedByUserId INT = NULL,
    @Notes NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @TicketId INT, @OldStatus NVARCHAR(20);
    SELECT @TicketId=TicketId, @OldStatus=Status FROM dbo.CallFieldVisit WITH (UPDLOCK, HOLDLOCK) WHERE FieldVisitId=@FieldVisitId;
    IF @TicketId IS NULL THROW 51013, 'Field visit not found.', 1;

    -- Simplified state machine: Scheduled → Completed / Cancelled
    -- Terminal states cannot be changed
    IF @OldStatus IN ('Completed','Cancelled') THROW 51014, 'Completed/Cancelled visits cannot be changed.', 1;
    IF @NewStatus NOT IN ('Completed','Cancelled') THROW 51015, 'Invalid status. Only Completed or Cancelled allowed.', 1;

    -- Allow direct Scheduled → Completed or Scheduled → Cancelled
    UPDATE dbo.CallFieldVisit
    SET Status=@NewStatus,
        CompletedAt = CASE WHEN @NewStatus='Completed' THEN sysutcdatetime() ELSE CompletedAt END,
        Notes = COALESCE(@Notes, Notes)
    WHERE FieldVisitId=@FieldVisitId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'FieldVisitStatus', @OldStatus, @NewStatus, @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CompletedAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId=@FieldVisitId;
END
GO

PRINT 'Updated sp_Call_FieldVisit_SetStatus with simplified state machine';
GO

-- Step 6: Update schedule proc to return CompletedAt instead of CheckedInAt/CheckedOutAt
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
    IF EXISTS (SELECT 1 FROM dbo.CallFieldVisit WHERE TicketId=@TicketId)
        THROW 51011, 'This ticket already has a field visit (one per ticket).', 1;
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

PRINT 'Updated sp_Call_FieldVisit_Schedule to return CompletedAt';
GO

-- Step 7: Drop old time tracking columns (optional - can be deferred to later cleanup)
-- Commented out for now to preserve data during transition
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CallFieldVisit') AND name = 'CheckedInAt')
-- BEGIN
--     ALTER TABLE dbo.CallFieldVisit DROP COLUMN CheckedInAt;
--     PRINT 'Dropped CheckedInAt column';
-- END
-- GO

-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.CallFieldVisit') AND name = 'CheckedOutAt')
-- BEGIN
--     ALTER TABLE dbo.CallFieldVisit DROP COLUMN CheckedOutAt;
--     PRINT 'Dropped CheckedOutAt column';
-- END
-- GO

PRINT 'Field Work state machine simplification complete!';
PRINT 'Next steps:';
PRINT '  1. Update API handlers to use Completed instead of CheckIn/CheckOut actions';
PRINT '  2. Update Desktop UI to remove time tracking buttons';
PRINT '  3. Update Mobile UI to remove time tracking buttons';
PRINT '  4. Update Reports to show ScheduledAt and CompletedAt instead of CheckedIn/CheckedOut times';
GO
