/* =====================================================================================
   IT Call Monitoring (Option B) - PROD Deploy Script
   - Run in SSMS
   - Requires base Call* schema already installed:
       dbo.CallTicket, dbo.CallTicketHistory, dbo.CallTicketNote, and dependencies.
   - Option B adds Employee-based assignment + backend ops + escalation automation/overrides.
   ===================================================================================== */

USE [YIMS];
GO

/* ---- Preflight: required base objects ---- */
IF OBJECT_ID('dbo.CallTicket', 'U') IS NULL
    THROW 51001, 'Missing dbo.CallTicket. Install base Call Monitoring schema first.', 1;
IF OBJECT_ID('dbo.CallTicketHistory', 'U') IS NULL
    THROW 51002, 'Missing dbo.CallTicketHistory. Install base Call Monitoring schema first.', 1;
IF OBJECT_ID('dbo.CallTicketNote', 'U') IS NULL
    THROW 51003, 'Missing dbo.CallTicketNote. Install base Call Monitoring schema first.', 1;
IF OBJECT_ID('dbo.Department', 'U') IS NULL
    THROW 51004, 'Missing dbo.Department. Required for vw_Call_TicketList.', 1;
IF OBJECT_ID('dbo.Employee', 'U') IS NULL
    THROW 51005, 'Missing dbo.Employee. Required for Option B employee assignment.', 1;
GO

/* =====================================================================================
   1) Option B Migration: Assign tickets to Employees (not Users)
   Source: Yakult.Inventory.App/Database/CallMonitoring.OptionB.EmployeeAssignment.sql
   ===================================================================================== */

/* 1) Add AssignedToEmpId to CallTicket */
IF COL_LENGTH('dbo.CallTicket', 'AssignedToEmpId') IS NULL
BEGIN
    ALTER TABLE dbo.CallTicket
    ADD AssignedToEmpId INT NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_AssignedToEmp')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_AssignedToEmp
    FOREIGN KEY (AssignedToEmpId) REFERENCES dbo.Employee(EmpId);
END
GO

/* 3) Update vw_Call_TicketList to use Employee assignment */
CREATE OR ALTER VIEW dbo.vw_Call_TicketList
AS
SELECT
    t.TicketId,
    t.TicketCode,
    t.Issue,
    d.Name AS Department,
    eAssignee.Name AS ResponsiblePerson,
    DATEDIFF(DAY, t.CreatedAt, COALESCE(t.SolvedAt, SYSUTCDATETIME())) AS TicketAgeDays,
    COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt) AS LastActivityAt,
    DATEDIFF(DAY, COALESCE(t.LastContactAt, t.UpdatedAt, t.CreatedAt), SYSUTCDATETIME()) AS IdleDays,
    t.LastContactAt,
    t.Status,
    t.Priority,
    t.CallerName,
    t.IssueType,
    t.CreatedAt,
    t.UpdatedAt,
    t.SolvedAt
FROM dbo.CallTicket t
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId;
GO

/* 4) sp_Call_CreateTicket: accept @AssignedToEmpId and insert into CallTicket.AssignedToEmpId */
CREATE OR ALTER PROCEDURE dbo.sp_Call_CreateTicket
    @ComId            INT            = NULL,
    @DeptId           INT            = NULL,
    @CallerName       NVARCHAR(150),
    @Issue            NVARCHAR(2000),
    @ProvidedSolution NVARCHAR(2000) = NULL,
    @IssueType        NVARCHAR(20)   = NULL,
    @Priority         NVARCHAR(20)   = NULL,
    @AssignedToEmpId  INT            = NULL,
    @CreatedByUserId  INT            = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @CallerName IS NULL OR LTRIM(RTRIM(@CallerName)) = '' THROW 50001, 'CallerName is required.', 1;
    IF @Issue IS NULL OR LTRIM(RTRIM(@Issue)) = '' THROW 50002, 'Issue is required.', 1;

    IF @Priority IS NULL OR LTRIM(RTRIM(@Priority)) = ''
        SET @Priority = 'Medium';

    SET @Priority = LTRIM(RTRIM(@Priority));
    DECLARE @PriorityCanonical NVARCHAR(20) =
        CASE UPPER(@Priority)
            WHEN 'LOW' THEN 'Low'
            WHEN 'MEDIUM' THEN 'Medium'
            WHEN 'HIGH' THEN 'High'
            WHEN 'CRITICAL' THEN 'Critical'
            ELSE NULL
        END;

    IF @PriorityCanonical IS NULL
        THROW 50014, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    IF @AssignedToEmpId IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId = @AssignedToEmpId)
        THROW 50016, 'AssignedToEmpId is invalid (employee not found).', 1;

    -- Validate org combination via BranchDepartmentCompany (Department no longer owns ComId).
    -- This deploy script version does not capture BranchId, so validate Company/Department only.
    IF @ComId IS NOT NULL
       AND @DeptId IS NOT NULL
       AND OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NOT NULL
    BEGIN
        IF NOT EXISTS (
            SELECT 1
            FROM dbo.BranchDepartmentCompany bdc
            WHERE bdc.CompanyID = @ComId
              AND bdc.DepartmentID = @DeptId
        )
            THROW 50032, 'Invalid Company/Department combination.', 1;
    END

    DECLARE @TicketId INT;

    BEGIN TRAN;

    INSERT dbo.CallTicket
    (
        ComId, DeptId, CallerName, Issue, ProvidedSolution,
        IssueType, Priority, Status,
        AssignedToEmpId,
        CreatedByUserId,
        LastContactAt
    )
    VALUES
    (
        @ComId, @DeptId, @CallerName, @Issue, @ProvidedSolution,
        @IssueType, @PriorityCanonical, 'Pending',
        @AssignedToEmpId,
        @CreatedByUserId,
        SYSUTCDATETIME()
    );

    SET @TicketId = SCOPE_IDENTITY();

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @CreatedByUserId, 'Created', NULL, NULL, 'Ticket created');

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES
        (@TicketId, @CreatedByUserId, 'Status',   NULL, 'Pending'),
        (@TicketId, @CreatedByUserId, 'Priority', NULL, @PriorityCanonical);

    IF @AssignedToEmpId IS NOT NULL
        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
        VALUES (@TicketId, @CreatedByUserId, 'AssignedToEmpId', NULL, CONVERT(NVARCHAR(50), @AssignedToEmpId));

    COMMIT;

    SELECT
        t.TicketId,
        t.TicketCode,
        t.Status,
        t.Priority,
        t.CreatedAt
    FROM dbo.CallTicket t
    WHERE t.TicketId = @TicketId;
END
GO

/* 5) Requires attention view stays the same but now reads ResponsiblePerson from Employee */
CREATE OR ALTER VIEW dbo.vw_Call_RequiresAttention
AS
SELECT *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)')
  AND (Priority IN ('High','Critical') OR ResponsiblePerson IS NULL);
GO

/* =====================================================================================
   2) Option B Backend Ops: Procedures for updating tickets (assignment/status/priority/notes)
   Source: Yakult.Inventory.App/Database/CallMonitoring.OptionB.BackendOps.sql
   ===================================================================================== */

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

/* =====================================================================================
   3) Option B Escalation Automation + Overrides (defaults 2/3 days)
   Source: Yakult.Inventory.App/Database/CallMonitoring.OptionB.Escalation.sql
   ===================================================================================== */

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

/* Auto-escalation assignee (App-driven auto escalation) */
IF OBJECT_ID('dbo.CallAutoEscalationAssigneeSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallAutoEscalationAssigneeSettings
    (
        SettingsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallAutoEscalationAssigneeSettings PRIMARY KEY,
        AssigneeEmpId INT NULL,
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallAutoEscAssignee_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAutoEscalationAssigneeSettings_Emp')
BEGIN
    ALTER TABLE dbo.CallAutoEscalationAssigneeSettings WITH CHECK
    ADD CONSTRAINT FK_CallAutoEscalationAssigneeSettings_Emp FOREIGN KEY (AssigneeEmpId) REFERENCES dbo.Employee(EmpId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallAutoEscalationAssigneeSettings_User')
BEGIN
    ALTER TABLE dbo.CallAutoEscalationAssigneeSettings WITH CHECK
    ADD CONSTRAINT FK_CallAutoEscalationAssigneeSettings_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
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

/* 6) Scheduled escalation processing */
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

/* ---- Postflight reminders ----
   - Ensure there is at least one active employee with Position='IT Supervisor' and one with Position='IT Manager'.
   - On SQL Express, schedule EXEC dbo.sp_Call_ProcessEscalations via Task Scheduler + sqlcmd.
*/

/* =====================================================================================
   Recommended indexes (optional, safe to run repeatedly)
   ===================================================================================== */

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CallTicketHistory_Ticket_Field_ChangedAt'
      AND object_id = OBJECT_ID('dbo.CallTicketHistory')
)
BEGIN
    CREATE INDEX IX_CallTicketHistory_Ticket_Field_ChangedAt
    ON dbo.CallTicketHistory (TicketId, FieldName, ChangedAt DESC)
    INCLUDE (NewValue, ChangedByUserId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CallTicket_Status_Assigned_Updated'
      AND object_id = OBJECT_ID('dbo.CallTicket')
)
BEGIN
    CREATE INDEX IX_CallTicket_Status_Assigned_Updated
    ON dbo.CallTicket (Status, AssignedToEmpId, UpdatedAt DESC)
    INCLUDE (TicketCode, Priority, CreatedAt, DeptId);
END
GO
