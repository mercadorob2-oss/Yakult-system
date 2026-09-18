
/* Priority update */
CREATE   PROCEDURE dbo.sp_Call_SetTicketPriority
    @TicketId        INT,
    @NewPriority     NVARCHAR(20),
    @ChangedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewPriority = LTRIM(RTRIM(@NewPriority));
    IF @NewPriority IS NULL OR @NewPriority = '' THROW 50006, 'NewPriority is required.', 1;

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
    IF @Status IN ('Solved', 'Resolved (Temporary)') THROW 50015, 'Cannot change priority after ticket is solved.', 1;

    IF ISNULL(@OldPriority, '') = @NewPriorityCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.CallTicket
    SET Priority = @NewPriorityCanonical,
        LastContactAt = SYSUTCDATETIME()
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@TicketId, @ChangedByUserId, 'Priority', @OldPriority, @NewPriorityCanonical);

    COMMIT;
END
