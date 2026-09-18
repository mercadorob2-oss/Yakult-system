
/* Status update */
CREATE   PROCEDURE dbo.sp_Call_SetTicketStatus
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

    DECLARE @NewStatusCanonical NVARCHAR(20) =
        CASE UPPER(@NewStatus)
            WHEN 'PENDING' THEN 'Pending'
            WHEN 'IN PROGRESS' THEN 'In Progress'
            WHEN 'ESCALATED' THEN 'Escalated'
            WHEN 'RESOLVED (TEMPORARY)' THEN 'Resolved (Temporary)'
            WHEN 'RESOLVED TEMPORARY' THEN 'Resolved (Temporary)'
            WHEN 'FORWARDED TO REPAIR' THEN 'Forwarded to Repair'
            WHEN 'SOLVED' THEN 'Solved'
            WHEN 'CLOSED' THEN 'Closed'
            WHEN 'REOPENED' THEN 'Reopened'
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

    IF @OldStatus IN ('Solved', 'Resolved (Temporary)', 'Closed')
       AND @NewStatusCanonical NOT IN ('Solved', 'Resolved (Temporary)', 'Closed', 'Reopened')
        THROW 50013, 'Cannot change status after ticket is resolved (use Reopened).', 1;

    UPDATE dbo.CallTicket
    SET Status = @NewStatusCanonical,
        LastContactAt = SYSUTCDATETIME(),
        SolvedAt = CASE WHEN @NewStatusCanonical IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN SYSUTCDATETIME() ELSE NULL END
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    COMMIT;
END
