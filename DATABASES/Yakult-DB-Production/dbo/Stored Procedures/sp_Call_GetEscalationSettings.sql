
CREATE   PROCEDURE dbo.sp_Call_GetEscalationSettings
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 DaysToSupervisor, DaysToManager, SupervisorPosition, ManagerPosition
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;
END
