/* =====================================================================================
   IT Call Monitoring - PROD Install Script (Base + Option B)

   Run in SSMS against your existing YIMS database.
   Safe to run multiple times:
   - Creates missing tables
   - Uses CREATE OR ALTER for views/procs

   Includes:
   - Base Call Monitoring tables + views
   - Email support tables (schema only)
   - Option B employee assignment + backend ops procs
   - Escalation defaults + per-ticket overrides + processor proc
   ===================================================================================== */

USE [YIMS];
GO

/* ---- Preflight: referenced core tables must already exist ---- */
IF OBJECT_ID('dbo.Company', 'U') IS NULL THROW 52001, 'Missing dbo.Company.', 1;
IF OBJECT_ID('dbo.Department', 'U') IS NULL THROW 52002, 'Missing dbo.Department.', 1;
IF OBJECT_ID('dbo.Employee', 'U') IS NULL THROW 52003, 'Missing dbo.Employee.', 1;
IF OBJECT_ID('dbo.[User]', 'U') IS NULL THROW 52004, 'Missing dbo.[User].', 1;
GO

/* =====================================================================================
   1) Base Call Monitoring tables
   ===================================================================================== */

IF OBJECT_ID('dbo.CallTicket', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallTicket
    (
        TicketId INT IDENTITY(1,1) NOT NULL,
        TicketCode  AS ('TCK-'+RIGHT('000000'+CONVERT(VARCHAR(6),TicketId),(6))) PERSISTED NOT NULL,
        ComId INT NULL,
        DeptId INT NULL,
        BranchId INT NULL,
        CallerName NVARCHAR(150) NOT NULL,
        Issue NVARCHAR(2000) NOT NULL,
        ProvidedSolution NVARCHAR(2000) NULL,
        Priority NVARCHAR(20) NOT NULL CONSTRAINT DF_CallTicket_Priority DEFAULT ('Medium'),
        Status NVARCHAR(20) NOT NULL CONSTRAINT DF_CallTicket_Status DEFAULT ('Pending'),
        AssignedToUserId INT NULL,
        CreatedByUserId INT NULL,
        CreatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallTicket_CreatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallTicket_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        SolvedAt DATETIME2(2) NULL,
        RowVer ROWVERSION NOT NULL,
        IssueType NVARCHAR(20) NULL,
        LastContactAt DATETIME2(2) NULL,
        LastReminderSentAt DATETIME2(2) NULL,
        AssignedToEmpId INT NULL,
        CONSTRAINT PK_CallTicket PRIMARY KEY CLUSTERED (TicketId ASC)
    );
END
GO

IF COL_LENGTH('dbo.CallTicket', 'BranchId') IS NULL
BEGIN
    ALTER TABLE dbo.CallTicket ADD BranchId INT NULL;
END
GO

IF OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_Branch')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch(BranchId);
END
GO

/* =====================================================================================
   6) Recommended indexes (optional, safe to run repeatedly)
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

IF OBJECT_ID('dbo.CallTicketHistory', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallTicketHistory
    (
        HistoryId BIGINT IDENTITY(1,1) NOT NULL,
        TicketId INT NOT NULL,
        ChangedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallTicketHistory_ChangedAt DEFAULT (SYSUTCDATETIME()),
        ChangedByUserId INT NULL,
        FieldName NVARCHAR(50) NOT NULL,
        OldValue NVARCHAR(2000) NULL,
        NewValue NVARCHAR(2000) NULL,
        Note NVARCHAR(2000) NULL,
        CONSTRAINT PK_CallTicketHistory PRIMARY KEY CLUSTERED (HistoryId ASC)
    );
END
GO

IF OBJECT_ID('dbo.CallTicketNote', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallTicketNote
    (
        NoteId BIGINT IDENTITY(1,1) NOT NULL,
        TicketId INT NOT NULL,
        NoteType NVARCHAR(30) NOT NULL CONSTRAINT DF_CallTicketNote_Type DEFAULT ('Note'),
        NoteText NVARCHAR(4000) NOT NULL,
        CreatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallTicketNote_CreatedAt DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL,
        CONSTRAINT PK_CallTicketNote PRIMARY KEY CLUSTERED (NoteId ASC)
    );
END
GO

/* Ensure Option B column exists (if CallTicket came from older schema) */
IF COL_LENGTH('dbo.CallTicket', 'AssignedToEmpId') IS NULL
BEGIN
    ALTER TABLE dbo.CallTicket ADD AssignedToEmpId INT NULL;
END
GO

/* Base foreign keys (idempotent by name) */
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_Company')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_Company FOREIGN KEY (ComId) REFERENCES dbo.Company(ComId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_Department')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department(DeptId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_AssignedToUser')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_AssignedToUser FOREIGN KEY (AssignedToUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_CreatedByUser')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicket_AssignedToEmp')
BEGIN
    ALTER TABLE dbo.CallTicket WITH CHECK
    ADD CONSTRAINT FK_CallTicket_AssignedToEmp FOREIGN KEY (AssignedToEmpId) REFERENCES dbo.Employee(EmpId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicketHistory_Ticket')
BEGIN
    ALTER TABLE dbo.CallTicketHistory WITH CHECK
    ADD CONSTRAINT FK_CallTicketHistory_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicketHistory_User')
BEGIN
    ALTER TABLE dbo.CallTicketHistory WITH CHECK
    ADD CONSTRAINT FK_CallTicketHistory_User FOREIGN KEY (ChangedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicketNote_Ticket')
BEGIN
    ALTER TABLE dbo.CallTicketNote WITH CHECK
    ADD CONSTRAINT FK_CallTicketNote_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicketNote_User')
BEGIN
    ALTER TABLE dbo.CallTicketNote WITH CHECK
    ADD CONSTRAINT FK_CallTicketNote_User FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

/* =====================================================================================
   2) Email config/log tables (schema only; sending is handled elsewhere)
   ===================================================================================== */

IF OBJECT_ID('dbo.CallNotificationRules', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallNotificationRules
    (
        RulesId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallNotificationRules PRIMARY KEY,
        NotifyOnNewTicket BIT NOT NULL CONSTRAINT DF_CallRules_New DEFAULT (1),
        NotifyOnStatusChange BIT NOT NULL CONSTRAINT DF_CallRules_Status DEFAULT (1),
        NotifyOnEscalation BIT NOT NULL CONSTRAINT DF_CallRules_Esc DEFAULT (1),
        NotifyOnReminder BIT NOT NULL CONSTRAINT DF_CallRules_Rem DEFAULT (0),
        GroupEmail NVARCHAR(255) NULL,
        EscalationEmail NVARCHAR(255) NULL,
        ReminderDays INT NOT NULL CONSTRAINT DF_CallRules_RemDays DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallRules_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallNotificationRules_User')
BEGIN
    ALTER TABLE dbo.CallNotificationRules WITH CHECK
    ADD CONSTRAINT FK_CallNotificationRules_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF OBJECT_ID('dbo.CallEmailSettings', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallEmailSettings
    (
        SettingsId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallEmailSettings PRIMARY KEY,
        SmtpServer NVARCHAR(255) NOT NULL,
        SmtpPort INT NOT NULL CONSTRAINT DF_CallEmailSettings_Port DEFAULT (587),
        UseSsl BIT NOT NULL CONSTRAINT DF_CallEmailSettings_UseSsl DEFAULT (1),
        SmtpUsername NVARCHAR(255) NULL,
        SmtpPasswordEnc VARBINARY(512) NULL,
        FromName NVARCHAR(150) NOT NULL,
        FromEmail NVARCHAR(255) NOT NULL,
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallEmailSettings_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallEmailSettings_User')
BEGIN
    ALTER TABLE dbo.CallEmailSettings WITH CHECK
    ADD CONSTRAINT FK_CallEmailSettings_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF OBJECT_ID('dbo.CallEmailTemplate', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallEmailTemplate
    (
        TemplateId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallEmailTemplate PRIMARY KEY,
        TemplateType NVARCHAR(30) NOT NULL,
        Subject NVARCHAR(255) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallEmailTemplate_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallEmailTemplate_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL,
        CONSTRAINT UQ_CallEmailTemplate_Type UNIQUE (TemplateType)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallEmailTemplate_User')
BEGIN
    ALTER TABLE dbo.CallEmailTemplate WITH CHECK
    ADD CONSTRAINT FK_CallEmailTemplate_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF OBJECT_ID('dbo.CallEmailLog', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallEmailLog
    (
        EmailLogId BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallEmailLog PRIMARY KEY,
        TicketId INT NULL,
        EmailType NVARCHAR(30) NOT NULL,
        Recipient NVARCHAR(255) NOT NULL,
        Subject NVARCHAR(255) NULL,
        Status NVARCHAR(20) NOT NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        DateSent DATETIME2(2) NOT NULL CONSTRAINT DF_CallEmailLog_DateSent DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL
    );
END
GO

/* =====================================================================================
   2b) Department SMTP (Option B: reusable profiles + Dept->Profile mapping)
   ===================================================================================== */

IF OBJECT_ID('dbo.CallSmtpProfile', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallSmtpProfile
    (
        ProfileId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallSmtpProfile PRIMARY KEY,
        ProfileName NVARCHAR(150) NOT NULL,
        SmtpServer NVARCHAR(255) NOT NULL,
        SmtpPort INT NOT NULL CONSTRAINT DF_CallSmtpProfile_Port DEFAULT (587),
        UseSsl BIT NOT NULL CONSTRAINT DF_CallSmtpProfile_UseSsl DEFAULT (1),
        SmtpUsername NVARCHAR(255) NULL,
        SmtpPasswordEnc VARBINARY(512) NULL,
        FromName NVARCHAR(150) NULL,
        FromEmail NVARCHAR(255) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallSmtpProfile_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallSmtpProfile_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL,
        CONSTRAINT UQ_CallSmtpProfile_ProfileName UNIQUE (ProfileName)
    );
END
GO

IF OBJECT_ID('dbo.CallDepartmentSmtpProfileLink', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallDepartmentSmtpProfileLink
    (
        DeptId INT NOT NULL CONSTRAINT PK_CallDepartmentSmtpProfileLink PRIMARY KEY,
        ProfileId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallDeptSmtpLink_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallDeptSmtpLink_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF OBJECT_ID('dbo.CallDepartmentNotificationRecipient', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallDepartmentNotificationRecipient
    (
        DeptId INT NOT NULL CONSTRAINT PK_CallDepartmentNotificationRecipient PRIMARY KEY,
        RecipientEmails NVARCHAR(2000) NULL,
        EscalationEmails NVARCHAR(2000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallDeptNotif_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallDeptNotif_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF OBJECT_ID('dbo.CallDepartmentSmtpProfile', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallDepartmentSmtpProfile
    (
        ProfileId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_CallDepartmentSmtpProfile PRIMARY KEY,
        DeptId INT NULL,
        DepartmentName NVARCHAR(150) NOT NULL,
        SmtpServer NVARCHAR(255) NOT NULL,
        SmtpPort INT NOT NULL CONSTRAINT DF_CallDeptSmtp_Port DEFAULT (587),
        UseSsl BIT NOT NULL CONSTRAINT DF_CallDeptSmtp_UseSsl DEFAULT (1),
        SmtpUsername NVARCHAR(255) NULL,
        SmtpPasswordEnc VARBINARY(512) NULL,
        FromName NVARCHAR(150) NULL,
        FromEmail NVARCHAR(255) NULL,
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallDeptSmtp_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL,
        CONSTRAINT UQ_CallDepartmentSmtpProfile_DepartmentName UNIQUE (DepartmentName)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallEmailLog_Ticket')
BEGIN
    ALTER TABLE dbo.CallEmailLog WITH CHECK
    ADD CONSTRAINT FK_CallEmailLog_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallEmailLog_User')
BEGIN
    ALTER TABLE dbo.CallEmailLog WITH CHECK
    ADD CONSTRAINT FK_CallEmailLog_User FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallSmtpProfile_User')
BEGIN
    ALTER TABLE dbo.CallSmtpProfile WITH CHECK
    ADD CONSTRAINT FK_CallSmtpProfile_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptSmtpLink_Profile')
BEGIN
    ALTER TABLE dbo.CallDepartmentSmtpProfileLink WITH CHECK
    ADD CONSTRAINT FK_CallDeptSmtpLink_Profile FOREIGN KEY (ProfileId) REFERENCES dbo.CallSmtpProfile(ProfileId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptSmtpLink_Department')
BEGIN
    IF OBJECT_ID('dbo.Department', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallDepartmentSmtpProfileLink WITH CHECK
        ADD CONSTRAINT FK_CallDeptSmtpLink_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department(DeptId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptNotif_Department')
BEGIN
    IF OBJECT_ID('dbo.Department', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallDepartmentNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallDeptNotif_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department(DeptId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptNotif_User')
BEGIN
    ALTER TABLE dbo.CallDepartmentNotificationRecipient WITH CHECK
    ADD CONSTRAINT FK_CallDeptNotif_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptSmtpLink_User')
BEGIN
    ALTER TABLE dbo.CallDepartmentSmtpProfileLink WITH CHECK
    ADD CONSTRAINT FK_CallDeptSmtpLink_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptSmtp_Department')
BEGIN
    IF OBJECT_ID('dbo.Department', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallDepartmentSmtpProfile WITH CHECK
        ADD CONSTRAINT FK_CallDeptSmtp_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department(DeptId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallDeptSmtp_User')
BEGIN
    ALTER TABLE dbo.CallDepartmentSmtpProfile WITH CHECK
    ADD CONSTRAINT FK_CallDeptSmtp_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallSmtpProfile_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallSmtpProfile_IsActive ON dbo.CallSmtpProfile (IsActive) INCLUDE(ProfileName);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallDeptSmtpLink_ProfileId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallDeptSmtpLink_ProfileId ON dbo.CallDepartmentSmtpProfileLink (ProfileId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallDeptNotif_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallDeptNotif_IsActive ON dbo.CallDepartmentNotificationRecipient (IsActive) INCLUDE (DeptId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallDeptSmtp_DepartmentName')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallDeptSmtp_DepartmentName ON dbo.CallDepartmentSmtpProfile (DepartmentName);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallDeptSmtp_DeptId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallDeptSmtp_DeptId ON dbo.CallDepartmentSmtpProfile (DeptId);
END
GO

/* =====================================================================================
   2c) Branch SMTP + recipients (Option B: reusable profiles + Branch->Profile mapping)
   ===================================================================================== */

IF OBJECT_ID('dbo.CallBranchSmtpProfileLink', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallBranchSmtpProfileLink
    (
        BranchId INT NOT NULL CONSTRAINT PK_CallBranchSmtpProfileLink PRIMARY KEY,
        ProfileId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallBranchSmtpLink_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallBranchSmtpLink_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF OBJECT_ID('dbo.CallBranchNotificationRecipient', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.CallBranchNotificationRecipient
    (
        BranchId INT NOT NULL CONSTRAINT PK_CallBranchNotificationRecipient PRIMARY KEY,
        RecipientEmails NVARCHAR(2000) NULL,
        EscalationEmails NVARCHAR(2000) NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_CallBranchNotif_IsActive DEFAULT (1),
        UpdatedAt DATETIME2(2) NOT NULL CONSTRAINT DF_CallBranchNotif_UpdatedAt DEFAULT (SYSUTCDATETIME()),
        UpdatedByUserId INT NULL
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_Profile')
BEGIN
    ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
    ADD CONSTRAINT FK_CallBranchSmtpLink_Profile FOREIGN KEY (ProfileId) REFERENCES dbo.CallSmtpProfile(ProfileId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_Branch')
BEGIN
    IF OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
        ADD CONSTRAINT FK_CallBranchSmtpLink_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch(BranchId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchSmtpLink_User')
BEGIN
    ALTER TABLE dbo.CallBranchSmtpProfileLink WITH CHECK
    ADD CONSTRAINT FK_CallBranchSmtpLink_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchNotif_Branch')
BEGIN
    IF OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
    BEGIN
        ALTER TABLE dbo.CallBranchNotificationRecipient WITH CHECK
        ADD CONSTRAINT FK_CallBranchNotif_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch(BranchId);
    END
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallBranchNotif_User')
BEGIN
    ALTER TABLE dbo.CallBranchNotificationRecipient WITH CHECK
    ADD CONSTRAINT FK_CallBranchNotif_User FOREIGN KEY (UpdatedByUserId) REFERENCES dbo.[User](UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallBranchSmtpLink_ProfileId')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallBranchSmtpLink_ProfileId ON dbo.CallBranchSmtpProfileLink (ProfileId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallBranchNotif_IsActive')
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallBranchNotif_IsActive ON dbo.CallBranchNotificationRecipient (IsActive) INCLUDE (BranchId);
END
GO

/* =====================================================================================
   3) Views
   ===================================================================================== */

CREATE OR ALTER VIEW dbo.vw_Call_TicketList
AS
SELECT
    t.TicketId,
    t.TicketCode,
    t.ComId,
    c.Name AS Company,
    t.Issue,
    d.Name AS Department,
    t.BranchId,
    CASE
        WHEN b.BranchId IS NULL THEN NULL
        WHEN ISNULL(b.IsCenter, 0) = 1 THEN b.Name + ' (Center)'
        WHEN ISNULL(b.IsDepot, 0) = 1 THEN b.Name + ' (Depot)'
        WHEN ISNULL(b.IsFactory, 0) = 1 THEN b.Name + ' (Factory)'
        WHEN ISNULL(b.IsDistributor, 0) = 1 THEN b.Name + ' (Distributor)'
        ELSE b.Name
    END AS Branch,
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
LEFT JOIN dbo.Company c ON c.ComId = t.ComId
LEFT JOIN dbo.Department d ON d.DeptId = t.DeptId
LEFT JOIN dbo.Employee eAssignee ON eAssignee.EmpId = t.AssignedToEmpId
LEFT JOIN dbo.Branch b ON b.BranchId = t.BranchId;
GO

CREATE OR ALTER VIEW dbo.vw_Call_RequiresAttention
AS
SELECT *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
  AND (Priority IN ('High','Critical') OR ResponsiblePerson IS NULL);
GO

CREATE OR ALTER VIEW dbo.vw_Call_DashboardMetrics
AS
SELECT
    SUM(CASE WHEN t.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed') THEN 1 ELSE 0 END) AS OpenTickets,
    SUM(CASE WHEN t.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed') AND t.Priority = 'Critical' THEN 1 ELSE 0 END) AS CriticalTickets,
    AVG(CASE WHEN t.SolvedAt IS NOT NULL THEN DATEDIFF(MINUTE, t.CreatedAt, t.SolvedAt) END) AS AvgResolutionMinutes,
    SUM(CASE WHEN CONVERT(date, t.CreatedAt) = CONVERT(date, SYSUTCDATETIME()) THEN 1 ELSE 0 END) AS TodaysVolume
FROM dbo.CallTicket t;
GO

CREATE OR ALTER VIEW dbo.vw_Call_TicketVolumeLast7Days
AS
SELECT
    CONVERT(date, t.CreatedAt) AS [Day],
    COUNT(*) AS TicketCount
FROM dbo.CallTicket t
WHERE t.CreatedAt >= DATEADD(DAY, -6, CONVERT(date, SYSUTCDATETIME()))
GROUP BY CONVERT(date, t.CreatedAt);
GO

CREATE OR ALTER VIEW dbo.vw_Call_IssueTypeDistribution
AS
SELECT
    COALESCE(NULLIF(LTRIM(RTRIM(t.IssueType)), ''), 'Unspecified') AS IssueType,
    COUNT(*) AS TicketCount
FROM dbo.CallTicket t
WHERE t.Status NOT IN ('Solved', 'Resolved (Temporary)', 'Closed')
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(t.IssueType)), ''), 'Unspecified');
GO

/* =====================================================================================
   4) Option B Create + Backend Ops procs
   ===================================================================================== */

CREATE OR ALTER PROCEDURE dbo.sp_Call_CreateTicket
    @ComId            INT            = NULL,
    @DeptId           INT            = NULL,
    @BranchId         INT            = NULL,
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

    IF @BranchId IS NOT NULL
       AND OBJECT_ID('dbo.Branch', 'U') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM dbo.Branch WHERE BranchId = @BranchId)
        THROW 50017, 'BranchId is invalid (branch not found).', 1;

    -- Validate org combination via BranchDepartmentCompany (Branch/Department no longer own ComId).
    IF @ComId IS NOT NULL
       AND OBJECT_ID('dbo.BranchDepartmentCompany', 'U') IS NOT NULL
    BEGIN
        IF @BranchId IS NOT NULL
        BEGIN
            IF @DeptId IS NOT NULL
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.BranchDepartmentCompany bdc
                    WHERE bdc.CompanyID = @ComId
                      AND bdc.BranchID = @BranchId
                      AND bdc.DepartmentID = @DeptId
                )
                    THROW 50030, 'Invalid Company/Branch/Department combination.', 1;
            END
            ELSE
            BEGIN
                IF NOT EXISTS (
                    SELECT 1
                    FROM dbo.BranchDepartmentCompany bdc
                    WHERE bdc.CompanyID = @ComId
                      AND bdc.BranchID = @BranchId
                )
                    THROW 50031, 'Invalid Company/Branch combination.', 1;
            END
        END
        ELSE IF @DeptId IS NOT NULL
        BEGIN
            IF NOT EXISTS (
                SELECT 1
                FROM dbo.BranchDepartmentCompany bdc
                WHERE bdc.CompanyID = @ComId
                  AND bdc.DepartmentID = @DeptId
            )
                THROW 50032, 'Invalid Company/Department combination.', 1;
        END
    END

    DECLARE @TicketId INT;

    BEGIN TRAN;

    INSERT dbo.CallTicket
    (
        ComId, DeptId, BranchId, CallerName, Issue, ProvidedSolution,
        IssueType, Priority, Status,
        AssignedToEmpId,
        CreatedByUserId,
        LastContactAt
    )
    VALUES
    (
        @ComId, @DeptId, @BranchId, @CallerName, @Issue, @ProvidedSolution,
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

    IF @BranchId IS NOT NULL
        INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue)
        VALUES (@TicketId, @CreatedByUserId, 'BranchId', NULL, CONVERT(NVARCHAR(50), @BranchId));

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

    IF @OldStatus IN ('Solved', 'Resolved (Temporary)', 'Closed')
       AND @NewStatusCanonical NOT IN ('Solved', 'Resolved (Temporary)', 'Closed', 'Reopened')
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
   5) Escalation defaults + overrides + processor
   ===================================================================================== */

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
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_CallTicketEscalationOverride_Ticket')
BEGIN
    ALTER TABLE dbo.CallTicketEscalationOverride WITH CHECK
    ADD CONSTRAINT FK_CallTicketEscalationOverride_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId);
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Call_GetEscalationSettings
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP 1 DaysToSupervisor, DaysToManager, SupervisorPosition, ManagerPosition
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Call_GetTicketEscalationOverride
    @TicketId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TicketId, DaysToSupervisor, DaysToManager, Reason, OverriddenByUserId, OverriddenAt
    FROM dbo.CallTicketEscalationOverride
    WHERE TicketId = @TicketId;
END
GO

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

    DECLARE @DefaultSup INT, @DefaultMgr INT;
    SELECT TOP 1 @DefaultSup = DaysToSupervisor, @DefaultMgr = DaysToManager
    FROM dbo.CallEscalationSettings
    ORDER BY SettingsId DESC;

    IF @DefaultSup IS NULL SET @DefaultSup = 2;
    IF @DefaultMgr IS NULL SET @DefaultMgr = 3;

    DECLARE @OldSup INT = NULL, @OldMgr INT = NULL;
    SELECT @OldSup = DaysToSupervisor, @OldMgr = DaysToManager
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

CREATE OR ALTER PROCEDURE dbo.sp_Call_ProcessEscalations
    @NowUtc DATETIME2(2) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @NowUtc IS NULL SET @NowUtc = SYSUTCDATETIME();

    -- Optional server-side runner (e.g., SQL Agent / Task Scheduler).
    -- The WinForms app also runs auto-escalation in-process; use the same global app lock to avoid overlap.
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
