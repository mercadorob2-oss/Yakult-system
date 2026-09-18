

-- Log an email send attempt
CREATE   PROCEDURE dbo.sp_Call_LogEmail
    @TicketId        INT = NULL,
    @EmailType       NVARCHAR(30),
    @Recipient       NVARCHAR(255),
    @Subject         NVARCHAR(255) = NULL,
    @Status          NVARCHAR(20),      -- Sent/Failed
    @ErrorMessage    NVARCHAR(2000) = NULL,
    @CreatedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT dbo.CallEmailLog (TicketId, EmailType, Recipient, Subject, Status, ErrorMessage, CreatedByUserId)
    VALUES (@TicketId, @EmailType, @Recipient, @Subject, @Status, @ErrorMessage, @CreatedByUserId);
END
