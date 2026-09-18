-- Option B Escalation Automation + Overrides
-- Run this after the base Call* schema + Option B migration.
-- This adds per-ticket escalation-day overrides, and a stored procedure for scheduled escalation processing.

USE [YIMS];
GO

/* 1) Escalation settings (single-row defaults) */
IF OBJECT_ID('dbo.CallEscalationSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallEscalationSettings
    (
        SettingsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallEscalationSettings PRIMARY KEY,
        DaysToSupervisor INT NOT NULL,
        DaysToManager INT NOT NULL,
        SupervisorPosition NVARCHAR(50) NOT NULL,
        ManagerPosition NVARCHAR(50) NOT NULL,
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallEscalationSettings_UpdatedAt DEFAULT SYSUTCDATETIME()
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM dbo.CallEscalationSettings)
BEGIN
    INSERT dbo.CallEscalationSettings (DaysToSupervisor, DaysToManager, SupervisorPosition, ManagerPosition)
    VALUES (2, 3, 'IT Supervisor', 'IT Manager');
END
GO

/* 2) Per-ticket escalation-day override */
IF OBJECT_ID('dbo.CallTicketEscalationOverride', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallTicketEscalationOverride
    (
        TicketId INT NOT NULL CONSTRAINT PK_CallTicketEscalationOverride PRIMARY KEY,
        DaysToSupervisor INT NOT NULL,
        DaysToManager INT NOT NULL,
        Reason NVARCHAR(400) NOT NULL,
        OverriddenByUserId INT NULL,
        OverriddenAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallTicketEscalationOverride_OverriddenAt DEFAULT SYSUTCDATETIME()
    );

    ALTER TABLE dbo.CallTicketEscalationOverride WITH CHECK
    ADD CONSTRAINT FK_CallTicketEscalationOverride_Ticket
    FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId);
END
GO

/* 3) Read settings */
CREATE OR ALTER PROCEDURE dbo.sp_Call_GetEscalationSettings
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP 1
        DaysToSupervisor,
        DaysToManager,
        SupervisorPosition,
        ManagerPosition
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;
END
GO

/* 4) Read per-ticket override */
CREATE OR ALTER PROCEDURE dbo.sp_Call_GetTicketEscalationOverride
    @TicketId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        TicketId,
        DaysToSupervisor,
        DaysToManager,
        Reason,
        OverriddenByUserId,
        OverriddenAt
    FROM dbo.CallTicketEscalationOverride
    WHERE TicketId = @TicketId;
END
GO

/* 5) Set/clear per-ticket override (increases only) */
CREATE OR ALTER PROCEDURE dbo.sp_Call_SetTicketEscalationOverride
    @TicketId INT,
    @DaysToSupervisor INT = NULL,
    @DaysToManager INT = NULL,
    @Reason NVARCHAR(400) = NULL,
    @ChangedByUserId INT = NULL,
    @Clear BIT = 0
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Reason = LTRIM(RTRIM(@Reason));
    IF @Reason IS NULL OR @Reason = '' THROW 50021, 'Reason is required.', 1;

    DECLARE @Status NVARCHAR(20);
    SELECT @Status = Status
    FROM dbo.CallTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE TicketId = @TicketId;

    IF @@ROWCOUNT = 0 THROW 50022, 'Ticket not found.', 1;
    IF @Status IN ('Solved', 'Resolved (Temporary)', 'Closed') THROW 50023, 'Cannot change escalation override after ticket is solved.', 1;

    DECLARE
        @DefaultSup INT,
        @DefaultMgr INT;

    SELECT TOP 1
        @DefaultSup = DaysToSupervisor,
        @DefaultMgr = DaysToManager
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;

    IF @DefaultSup IS NULL SET @DefaultSup = 2;
    IF @DefaultMgr IS NULL SET @DefaultMgr = 3;

    DECLARE
        @OldSup INT = NULL,
        @OldMgr INT = NULL;

    SELECT
        @OldSup = DaysToSupervisor,
        @OldMgr = DaysToManager
    FROM dbo.CallTicketEscalationOverride
    WHERE TicketId = @TicketId;

    IF @Clear = 1
    BEGIN
        IF EXISTS (SELECT 1 FROM dbo.CallTicketEscalationOverride WHERE TicketId = @TicketId)
            DELETE dbo.CallTicketEscalationOverride WHERE TicketId = @TicketId;

        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
        VALUES
        (
            @TicketId,
            @ChangedByUserId,
            'EscalationOverride',
            CASE WHEN @OldSup IS NULL AND @OldMgr IS NULL THEN NULL ELSE CONCAT('Sup=', @OldSup, ',Mgr=', @OldMgr) END,
            NULL,
            CONCAT('Cleared: ', @Reason)
        );

        RETURN;
    END

    IF @DaysToSupervisor IS NULL OR @DaysToSupervisor < 1 THROW 50024, 'DaysToSupervisor is required.', 1;
    IF @DaysToManager IS NULL OR @DaysToManager < 1 THROW 50025, 'DaysToManager is required.', 1;

    IF @DaysToSupervisor < @DefaultSup THROW 50026, 'DaysToSupervisor cannot be less than default.', 1;
    IF @DaysToManager < @DefaultMgr THROW 50027, 'DaysToManager cannot be less than default.', 1;
    IF @DaysToManager < @DaysToSupervisor THROW 50028, 'DaysToManager must be >= DaysToSupervisor.', 1;

    MERGE dbo.CallTicketEscalationOverride AS tgt
    USING (SELECT @TicketId AS TicketId) AS src
        ON tgt.TicketId = src.TicketId
    WHEN MATCHED THEN
        UPDATE SET
            DaysToSupervisor = @DaysToSupervisor,
            DaysToManager = @DaysToManager,
            Reason = @Reason,
            OverriddenByUserId = @ChangedByUserId,
            OverriddenAt = SYSUTCDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (TicketId, DaysToSupervisor, DaysToManager, Reason, OverriddenByUserId)
        VALUES (@TicketId, @DaysToSupervisor, @DaysToManager, @Reason, @ChangedByUserId);

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES
    (
        @TicketId,
        @ChangedByUserId,
        'EscalationOverride',
        CASE WHEN @OldSup IS NULL AND @OldMgr IS NULL THEN NULL ELSE CONCAT('Sup=', @OldSup, ',Mgr=', @OldMgr) END,
        CONCAT('Sup=', @DaysToSupervisor, ',Mgr=', @DaysToManager),
        @Reason
    );
END
GO

/* 6) Scheduled escalation processing
      - Uses inactivity (LastContactAt/UpdatedAt/CreatedAt) to match the WinForms app's auto escalation behavior.
      - At DaysToSupervisor: auto-assign to IT Supervisor and set Status='Escalated'.
      - At DaysToManager: auto-assign to IT Manager and set Status='Escalated'.
*/
CREATE OR ALTER PROCEDURE dbo.sp_Call_ProcessEscalations
    @NowUtc DATETIME2(2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NowUtc IS NULL SET @NowUtc = SYSUTCDATETIME();

    -- Use the same global app lock as the WinForms in-process runner to avoid overlap.
    DECLARE @LockResult INT;
    EXEC @LockResult = sp_getapplock
        @Resource = N'Yakult.Inventory.App|CallMonitoring|BackgroundJobs',
        @LockMode = 'Exclusive',
        @LockOwner = 'Session',
        @LockTimeout = 0;
    IF @LockResult < 0 RETURN;

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
        IdleDays INT NOT NULL,
        DaysToSupervisor INT NOT NULL,
        DaysToManager INT NOT NULL
    );

    INSERT @Eligible (TicketId, IdleDays, DaysToSupervisor, DaysToManager)
    SELECT
        t.TicketId,
        DATEDIFF(DAY, COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt), @NowUtc) AS IdleDays,
        COALESCE(o.DaysToSupervisor, @DefaultSup) AS DaysToSupervisor,
        COALESCE(o.DaysToManager, @DefaultMgr) AS DaysToManager
    FROM dbo.CallTicket t
    LEFT JOIN dbo.CallTicketEscalationOverride o ON o.TicketId = t.TicketId
    WHERE t.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed');

    -- Stage 2: manager escalation (>= DaysToManager)
    DECLARE @MgrChanged TABLE
    (
        TicketId INT NOT NULL,
        OldAssignedToEmpId INT NULL,
        OldStatus NVARCHAR(20) NULL
    );

    UPDATE t
    SET
        AssignedToEmpId = @ManagerEmpId,
        UpdatedAt = SYSUTCDATETIME(),
        Status = CASE WHEN t.Status = 'Escalated' THEN t.Status ELSE 'Escalated' END,
        LastContactAt = SYSUTCDATETIME()
    OUTPUT
        INSERTED.TicketId,
        DELETED.AssignedToEmpId,
        DELETED.Status
    INTO @MgrChanged (TicketId, OldAssignedToEmpId, OldStatus)
    FROM dbo.CallTicket t
    JOIN @Eligible e ON e.TicketId = t.TicketId
    WHERE e.IdleDays >= e.DaysToManager
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

    -- Stage 1: supervisor reassignment (>= DaysToSupervisor and < DaysToManager)
    DECLARE @SupChanged TABLE
    (
        TicketId INT NOT NULL,
        OldAssignedToEmpId INT NULL
    );

    UPDATE t
    SET
        AssignedToEmpId = @SupervisorEmpId,
        UpdatedAt = SYSUTCDATETIME(),
        Status = CASE WHEN t.Status = 'Escalated' THEN t.Status ELSE 'Escalated' END,
        LastContactAt = SYSUTCDATETIME()
    OUTPUT
        INSERTED.TicketId,
        DELETED.AssignedToEmpId
    INTO @SupChanged (TicketId, OldAssignedToEmpId)
    FROM dbo.CallTicket t
    JOIN @Eligible e ON e.TicketId = t.TicketId
    WHERE e.IdleDays >= e.DaysToSupervisor
      AND e.IdleDays < e.DaysToManager
      AND ISNULL(t.AssignedToEmpId, -1) <> @SupervisorEmpId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    SELECT
        c.TicketId,
        NULL,
        'AutoEscalation',
        CONCAT('AssignedToEmpId=', COALESCE(CONVERT(NVARCHAR(50), c.OldAssignedToEmpId), 'NULL')),
        CONCAT('AssignedToEmpId=', @SupervisorEmpId, ';Status=Escalated'),
        'Auto escalation to IT Supervisor'
    FROM @SupChanged c;
END
GO
