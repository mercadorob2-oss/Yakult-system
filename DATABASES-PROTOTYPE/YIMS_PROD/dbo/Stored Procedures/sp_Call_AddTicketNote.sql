
/* Note add */
CREATE   PROCEDURE dbo.sp_Call_AddTicketNote
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

    IF @Status IN ('Solved', 'Resolved (Temporary)') AND @NoteType NOT IN ('TemporaryReturn')
        THROW 50018, 'Cannot add notes after ticket is solved.', 1;

    INSERT dbo.CallTicketNote (TicketId, NoteType, NoteText, CreatedByUserId)
    VALUES (@TicketId, @NoteType, @NoteText, @CreatedByUserId);

    UPDATE dbo.CallTicket
    SET LastContactAt = SYSUTCDATETIME()
    WHERE TicketId = @TicketId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@TicketId, @CreatedByUserId, 'NoteAdded', NULL, @NoteType);

    COMMIT;
END
