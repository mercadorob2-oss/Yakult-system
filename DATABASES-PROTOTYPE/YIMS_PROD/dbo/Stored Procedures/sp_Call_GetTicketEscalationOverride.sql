
CREATE   PROCEDURE dbo.sp_Call_GetTicketEscalationOverride
    @TicketId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TicketId, DaysToSupervisor, DaysToManager, Reason, OverriddenByUserId, OverriddenAt
    FROM dbo.CallTicketEscalationOverride
    WHERE TicketId = @TicketId;
END
