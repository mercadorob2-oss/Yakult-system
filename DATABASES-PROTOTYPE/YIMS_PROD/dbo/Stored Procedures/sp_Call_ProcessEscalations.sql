
CREATE   PROCEDURE dbo.sp_Call_ProcessEscalations
    @NowUtc DATETIME2(2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NowUtc IS NULL SET @NowUtc = SYSUTCDATETIME();

    DECLARE
        @DefaultSup INT,
        @DefaultMgr INT,
        @SupervisorPosition NVARCHAR(50),
        @ManagerPosition NVARCHAR(50);

    SELECT TOP 1
        @DefaultSup = DaysToSupervisor,
        @DefaultMgr = DaysToManager,
        @SupervisorPosition = SupervisorPosition,
        @ManagerPosition = ManagerPosition
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;

    IF @DefaultSup IS NULL SET @DefaultSup = 2;
    IF @DefaultMgr IS NULL SET @DefaultMgr = 3;
    IF @SupervisorPosition IS NULL SET @SupervisorPosition = 'IT Supervisor';
    IF @ManagerPosition IS NULL SET @ManagerPosition = 'IT Manager';

    DECLARE @SupervisorEmpId INT =
    (
        SELECT MIN(EmpId)
        FROM dbo.Employee
        WHERE Active = 1
          AND UPPER(LTRIM(RTRIM(Position))) = UPPER(@SupervisorPosition)
    );

    DECLARE @ManagerEmpId INT =
    (
        SELECT MIN(EmpId)
        FROM dbo.Employee
        WHERE Active = 1
          AND UPPER(LTRIM(RTRIM(Position))) = UPPER(@ManagerPosition)
    );

    IF @SupervisorEmpId IS NULL RETURN;
    IF @ManagerEmpId IS NULL RETURN;

    DECLARE @Eligible TABLE
    (
        TicketId INT NOT NULL PRIMARY KEY,
        AgeDays INT NOT NULL,
        DaysToSupervisor INT NOT NULL,
        DaysToManager INT NOT NULL
    );

    INSERT @Eligible (TicketId, AgeDays, DaysToSupervisor, DaysToManager)
    SELECT
        t.TicketId,
        DATEDIFF(DAY, t.CreatedAt, @NowUtc) AS AgeDays,
        COALESCE(o.DaysToSupervisor, @DefaultSup) AS DaysToSupervisor,
        COALESCE(o.DaysToManager, @DefaultMgr) AS DaysToManager
    FROM dbo.CallTicket t
    LEFT JOIN dbo.CallTicketEscalationOverride o ON o.TicketId = t.TicketId
    WHERE t.Status NOT IN ('Solved', 'Resolved (Temporary)');

    DECLARE @MgrChanged TABLE
    (
        TicketId INT NOT NULL,
        OldAssignedToEmpId INT NULL,
        OldStatus NVARCHAR(20) NULL
    );

    UPDATE t
    SET
        AssignedToEmpId = @ManagerEmpId,
        Status = CASE WHEN t.Status = 'Escalated' THEN t.Status ELSE 'Escalated' END,
        LastContactAt = SYSUTCDATETIME()
    OUTPUT
        INSERTED.TicketId,
        DELETED.AssignedToEmpId,
        DELETED.Status
    INTO @MgrChanged (TicketId, OldAssignedToEmpId, OldStatus)
    FROM dbo.CallTicket t
    JOIN @Eligible e ON e.TicketId = t.TicketId
    WHERE e.AgeDays >= e.DaysToManager
      AND (ISNULL(t.AssignedToEmpId, -1) <> @ManagerEmpId OR t.Status <> 'Escalated');

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    SELECT
        c.TicketId,
        NULL,
        'AutoEscalation',
        CONCAT('AssignedToEmpId=', COALESCE(CONVERT(NVARCHAR(50), c.OldAssignedToEmpId), 'NULL'), ';Status=', COALESCE(c.OldStatus, 'NULL')),
        CONCAT('AssignedToEmpId=', @ManagerEmpId, ';Status=Escalated'),
        'Auto escalation to IT Manager'
    FROM @MgrChanged c;

    DECLARE @SupChanged TABLE
    (
        TicketId INT NOT NULL,
        OldAssignedToEmpId INT NULL
    );

    UPDATE t
    SET
        AssignedToEmpId = @SupervisorEmpId,
        LastContactAt = SYSUTCDATETIME()
    OUTPUT
        INSERTED.TicketId,
        DELETED.AssignedToEmpId
    INTO @SupChanged (TicketId, OldAssignedToEmpId)
    FROM dbo.CallTicket t
    JOIN @Eligible e ON e.TicketId = t.TicketId
    WHERE e.AgeDays >= e.DaysToSupervisor
      AND e.AgeDays < e.DaysToManager
      AND ISNULL(t.AssignedToEmpId, -1) <> @SupervisorEmpId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    SELECT
        c.TicketId,
        NULL,
        'AutoEscalation',
        CONCAT('AssignedToEmpId=', COALESCE(CONVERT(NVARCHAR(50), c.OldAssignedToEmpId), 'NULL')),
        CONCAT('AssignedToEmpId=', @SupervisorEmpId),
        'Auto escalation to IT Supervisor'
    FROM @SupChanged c;
END
