-- Option B Backend Ops: Procedures for updating tickets (Employee assignment)
-- Run this after the base Call* schema + Option B migration.

USE [YIMS];
GO

/* Status update */
CREATE OR ALTER PROCEDURE dbo.sp_Call_SetTicketStatus
    @TicketId        INT,
    @NewStatus       NVARCHAR(20),
    @ChangedByUserId INT           = NULL,
    @Note            NVARCHAR(2000)= NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewStatus = LTRIM(RTRIM(@NewStatus));
    IF @NewStatus IS NULL OR @NewStatus = '' THROW 50004, 'NewStatus is required.', 1;

    -- Canonicalize + validate allowed statuses (matches desktop UI)
    DECLARE @NewStatusCanonical NVARCHAR(20) =
        CASE UPPER(@NewStatus)
            WHEN 'PENDING' THEN 'Pending'
            WHEN 'IN PROGRESS' THEN 'In Progress'
            WHEN 'ESCALATED' THEN 'Escalated'
            WHEN 'RESOLVED (TEMPORARY)' THEN 'Resolved (Temporary)'
            WHEN 'RESOLVED TEMPORARY' THEN 'Resolved (Temporary)'
            WHEN 'FORWARDED TO REPAIR' THEN 'Forwarded to Repair'
            WHEN 'SOLVED' THEN 'Solved'
            WHEN 'REOPENED' THEN 'Reopened'
            WHEN 'CLOSED' THEN 'Closed'
            ELSE NULL
        END;

    IF @NewStatusCanonical IS NULL
        THROW 50012, 'Invalid status. Allowed: Pending, In Progress, Escalated, Resolved (Temporary), Forwarded to Repair, Solved, Closed, Reopened.', 1;

    DECLARE @OldStatus NVARCHAR(20);

    BEGIN TRAN;

    SELECT @OldStatus = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0 THROW 50003, 'Ticket not found.', 1;

    IF @OldStatus = @NewStatusCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    -- Prevent reopening from the database side too (UI already disables this).
    IF @OldStatus IN ('Solved', 'Resolved (Temporary)', 'Closed') AND @NewStatusCanonical NOT IN ('Solved', 'Resolved (Temporary)', 'Closed', 'Reopened')
        THROW 50013, 'Cannot change status after ticket is resolved (use Reopened).', 1;

    UPDATE dbo.CallTicket
    SET Status = @NewStatusCanonical,
        UpdatedAt = SYSUTCDATETIME(),
        LastContactAt = SYSUTCDATETIME(),
        SolvedAt = CASE WHEN @NewStatusCanonical IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN SYSUTCDATETIME() ELSE NULL END
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    COMMIT;
END
GO

/* Priority update */
CREATE OR ALTER PROCEDURE dbo.sp_Call_SetTicketPriority
    @TicketId        INT,
    @NewPriority     NVARCHAR(20),
    @ChangedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewPriority = LTRIM(RTRIM(@NewPriority));
    IF @NewPriority IS NULL OR @NewPriority = '' THROW 50006, 'NewPriority is required.', 1;

    -- Canonicalize + validate allowed priorities (matches desktop UI)
    DECLARE @NewPriorityCanonical NVARCHAR(20) =
        CASE UPPER(@NewPriority)
            WHEN 'LOW' THEN 'Low'
            WHEN 'MEDIUM' THEN 'Medium'
            WHEN 'HIGH' THEN 'High'
            WHEN 'CRITICAL' THEN 'Critical'
            ELSE NULL
        END;

    IF @NewPriorityCanonical IS NULL
        THROW 50014, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    DECLARE @OldPriority NVARCHAR(20);
    DECLARE @Status NVARCHAR(20);

    BEGIN TRAN;

    SELECT
        @OldPriority = Priority,
        @Status = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0 THROW 50005, 'Ticket not found.', 1;
    IF @Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THROW 50015, 'Cannot change priority after ticket is solved.', 1;

    IF ISNULL(@OldPriority, '') = @NewPriorityCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.CallTicket
    SET Priority = @NewPriorityCanonical,
        UpdatedAt = SYSUTCDATETIME(),
        LastContactAt = SYSUTCDATETIME()
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@TicketId, @ChangedByUserId, 'Priority', @OldPriority, @NewPriorityCanonical);

    COMMIT;
END
GO

/* Note add */
CREATE OR ALTER PROCEDURE dbo.sp_Call_AddTicketNote
    @TicketId        INT,
    @NoteType        NVARCHAR(30) = 'Note',
    @NoteText        NVARCHAR(4000),
    @CreatedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NoteType = COALESCE(NULLIF(LTRIM(RTRIM(@NoteType)), ''), 'Note');
    SET @NoteText = LTRIM(RTRIM(@NoteText));

    IF @NoteText IS NULL OR @NoteText = ''
        THROW 50009, 'NoteText is required.', 1;

    BEGIN TRAN;

    DECLARE @Status NVARCHAR(20);

    SELECT @Status = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0
        THROW 50008, 'Ticket not found.', 1;

    IF @Status IN ('Solved', 'Resolved (Temporary)', 'Closed') AND @NoteType NOT IN ('TemporaryReturn')
        THROW 50018, 'Cannot add notes after ticket is solved.', 1;

    INSERT dbo.CallTicketNote (TicketId, NoteType, NoteText, CreatedByUserId)
    VALUES (@TicketId, @NoteType, @NoteText, @CreatedByUserId);

    UPDATE dbo.CallTicket
    SET UpdatedAt = SYSUTCDATETIME(),
        LastContactAt = SYSUTCDATETIME()
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@TicketId, @CreatedByUserId, 'NoteAdded', NULL, @NoteType);

    COMMIT;
END
GO

/* Assign ticket to employee (Option B) */
CREATE OR ALTER PROCEDURE dbo.sp_Call_AssignTicket
    @TicketId            INT,
    @AssignedToEmpId     INT = NULL,
    @ChangedByUserId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @OldAssigned INT;
    DECLARE @Status NVARCHAR(20);

    IF @AssignedToEmpId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @AssignedToEmpId)
        THROW 50016, 'AssignedToEmpId is invalid (employee not found).', 1;

    BEGIN TRAN;

    SELECT
        @OldAssigned = AssignedToEmpId,
        @Status = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0 THROW 50007, 'Ticket not found.', 1;
    IF @Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THROW 50017, 'Cannot change assignment after ticket is solved.', 1;

    IF (ISNULL(@OldAssigned, -1) = ISNULL(@AssignedToEmpId, -1))
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.CallTicket
    SET AssignedToEmpId = @AssignedToEmpId,
        UpdatedAt = SYSUTCDATETIME(),
        LastContactAt = SYSUTCDATETIME()
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES
    (
        @TicketId,
        @ChangedByUserId,
        'AssignedToEmpId',
        CASE WHEN @OldAssigned IS NULL THEN NULL ELSE CONVERT(NVARCHAR(50), @OldAssigned) END,
        CASE WHEN @AssignedToEmpId IS NULL THEN NULL ELSE CONVERT(NVARCHAR(50), @AssignedToEmpId) END
    );

    COMMIT;
END
GO
