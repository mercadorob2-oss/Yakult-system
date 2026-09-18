
/* Assign ticket to employee (Option B) */
CREATE   PROCEDURE dbo.sp_Call_AssignTicket
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
    IF @Status IN ('Solved', 'Resolved (Temporary)') THROW 50017, 'Cannot change assignment after ticket is solved.', 1;

    IF (ISNULL(@OldAssigned, -1) = ISNULL(@AssignedToEmpId, -1))
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.CallTicket
    SET AssignedToEmpId = @AssignedToEmpId,
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
