-- Migration: Create schema for the Repair Technician Portal
-- Tables: RepairTicket, RepairTicketHistory, RepairTicketNote, RepairTicketAttachment,
--         RepairTechnicianAttendance
-- Modeled on dbo.CallTicket / CallTicketHistory / CallTicketNote and
-- dbo.ReceiptSetDocumentImage (see Migration_ReceiptSetDocumentImage_CreateTable.sql).
-- All statements are idempotent (guarded by IF NOT EXISTS) so this script is safe to re-run.

-- ── 1. dbo.RepairTicket ──────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairTicket')
)
BEGIN
    CREATE TABLE dbo.RepairTicket (
        RepairTicketId     INT             IDENTITY (1, 1) NOT NULL,
        TicketCode         AS              ('RPR-' + RIGHT('000000' + CONVERT(VARCHAR(6), RepairTicketId), 6)) PERSISTED,
        ItemId              INT             NOT NULL,
        SetId               INT             NULL,
        SetCode             NVARCHAR (20)   NULL,
        ComId               INT             NULL,
        BranchId            INT             NULL,
        DeptId              INT             NULL,
        ItemNameSnapshot    NVARCHAR (200)  NOT NULL,
        ItemSerialSnapshot  VARCHAR (255)   NULL,
        Problem             NVARCHAR (2000) NOT NULL,
        Diagnosis           NVARCHAR (2000) NULL,
        Resolution          NVARCHAR (2000) NULL,
        PartsUsed           NVARCHAR (1000) NULL,
        Priority            NVARCHAR (20)   CONSTRAINT DF_RepairTicket_Priority DEFAULT ('Low') NOT NULL,
        Status              NVARCHAR (20)   CONSTRAINT DF_RepairTicket_Status DEFAULT ('Waiting') NOT NULL,
        SubmittedByEmpId    INT             NULL,
        SubmittedByUserId   INT             NULL,
        AssignedTechEmpId   INT             NULL,
        DateReceived        DATETIME2 (2)   CONSTRAINT DF_RepairTicket_DateReceived DEFAULT (sysutcdatetime()) NOT NULL,
        CreatedAt           DATETIME2 (2)   CONSTRAINT DF_RepairTicket_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt           DATETIME2 (2)   CONSTRAINT DF_RepairTicket_UpdatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CompletedAt         DATETIME2 (2)   NULL,
        RowVer              ROWVERSION      NOT NULL,
        CONSTRAINT PK_RepairTicket PRIMARY KEY CLUSTERED (RepairTicketId ASC),
        CONSTRAINT CK_RepairTicket_Status CHECK (Status IN ('Waiting', 'Diagnosing', 'Repairing', 'AwaitingParts', 'Testing', 'Completed', 'Unrepairable')),
        CONSTRAINT CK_RepairTicket_Priority CHECK (Priority IN ('Low', 'Medium', 'High', 'Critical')),
        CONSTRAINT FK_RepairTicket_Item FOREIGN KEY (ItemId) REFERENCES dbo.Item (ItemId),
        CONSTRAINT FK_RepairTicket_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT FK_RepairTicket_Company FOREIGN KEY (ComId) REFERENCES dbo.Company (ComId),
        CONSTRAINT FK_RepairTicket_Branch FOREIGN KEY (BranchId) REFERENCES dbo.Branch (BranchId),
        CONSTRAINT FK_RepairTicket_Department FOREIGN KEY (DeptId) REFERENCES dbo.Department (DeptId),
        CONSTRAINT FK_RepairTicket_SubmittedByEmp FOREIGN KEY (SubmittedByEmpId) REFERENCES dbo.Employee (EmpId),
        CONSTRAINT FK_RepairTicket_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES dbo.[User] (UserId),
        CONSTRAINT FK_RepairTicket_AssignedTechEmp FOREIGN KEY (AssignedTechEmpId) REFERENCES dbo.Employee (EmpId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicket_Status_Priority' AND object_id = OBJECT_ID('dbo.RepairTicket')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicket_Status_Priority
        ON dbo.RepairTicket (Status ASC, Priority ASC)
        INCLUDE (CreatedAt, AssignedTechEmpId);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicket_ComBranchDept' AND object_id = OBJECT_ID('dbo.RepairTicket')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicket_ComBranchDept
        ON dbo.RepairTicket (ComId ASC, BranchId ASC, DeptId ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicket_ItemId' AND object_id = OBJECT_ID('dbo.RepairTicket')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicket_ItemId
        ON dbo.RepairTicket (ItemId ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.triggers WHERE name = 'trg_RepairTicket_SetUpdatedAt' AND parent_id = OBJECT_ID('dbo.RepairTicket')
)
EXEC ('
CREATE TRIGGER dbo.trg_RepairTicket_SetUpdatedAt
ON dbo.RepairTicket
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE t
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.RepairTicket t
    INNER JOIN inserted i ON i.RepairTicketId = t.RepairTicketId;
END
');
GO

-- ── 2. dbo.RepairTicketHistory ───────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairTicketHistory')
)
BEGIN
    CREATE TABLE dbo.RepairTicketHistory (
        HistoryId        BIGINT          IDENTITY (1, 1) NOT NULL,
        RepairTicketId   INT             NOT NULL,
        ChangedAt         DATETIME2 (2)   CONSTRAINT DF_RepairTicketHistory_ChangedAt DEFAULT (sysutcdatetime()) NOT NULL,
        ChangedByUserId   INT             NULL,
        FieldName         NVARCHAR (50)   NOT NULL,
        OldValue          NVARCHAR (2000) NULL,
        NewValue          NVARCHAR (2000) NULL,
        Note              NVARCHAR (2000) NULL,
        CONSTRAINT PK_RepairTicketHistory PRIMARY KEY CLUSTERED (HistoryId ASC),
        CONSTRAINT FK_RepairTicketHistory_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairTicketHistory_ChangedByUser FOREIGN KEY (ChangedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicketHistory_Ticket_ChangedAt' AND object_id = OBJECT_ID('dbo.RepairTicketHistory')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicketHistory_Ticket_ChangedAt
        ON dbo.RepairTicketHistory (RepairTicketId ASC, ChangedAt DESC);
GO

-- ── 3. dbo.RepairTicketNote ──────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairTicketNote')
)
BEGIN
    CREATE TABLE dbo.RepairTicketNote (
        NoteId            BIGINT          IDENTITY (1, 1) NOT NULL,
        RepairTicketId    INT             NOT NULL,
        NoteType           NVARCHAR (30)   CONSTRAINT DF_RepairTicketNote_NoteType DEFAULT ('Note') NOT NULL,
        NoteText           NVARCHAR (4000) NOT NULL,
        CreatedAt          DATETIME2 (2)   CONSTRAINT DF_RepairTicketNote_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CreatedByUserId    INT             NULL,
        CreatedByEmpId     INT             NULL,
        CONSTRAINT PK_RepairTicketNote PRIMARY KEY CLUSTERED (NoteId ASC),
        CONSTRAINT FK_RepairTicketNote_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairTicketNote_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User] (UserId),
        CONSTRAINT FK_RepairTicketNote_CreatedByEmp FOREIGN KEY (CreatedByEmpId) REFERENCES dbo.Employee (EmpId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicketNote_Ticket_CreatedAt' AND object_id = OBJECT_ID('dbo.RepairTicketNote')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicketNote_Ticket_CreatedAt
        ON dbo.RepairTicketNote (RepairTicketId ASC, CreatedAt DESC);
GO

-- ── 4. dbo.RepairTicketAttachment ────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairTicketAttachment')
)
BEGIN
    CREATE TABLE dbo.RepairTicketAttachment (
        AttachmentId       INT             IDENTITY (1, 1) NOT NULL,
        RepairTicketId     INT             NOT NULL,
        AttachmentType      VARCHAR (10)    NOT NULL,
        FileName            NVARCHAR (260)  NULL,
        MimeType            NVARCHAR (100)  NULL,
        FileBytes           VARBINARY (MAX) NOT NULL,
        FileSizeBytes       INT             NULL,
        SortOrder           INT             CONSTRAINT DF_RepairTicketAttachment_SortOrder DEFAULT (0) NOT NULL,
        UploadedAt          DATETIME2 (2)   CONSTRAINT DF_RepairTicketAttachment_UploadedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UploadedByUserId    INT             NULL,
        CONSTRAINT PK_RepairTicketAttachment PRIMARY KEY CLUSTERED (AttachmentId ASC),
        CONSTRAINT CK_RepairTicketAttachment_Type CHECK (AttachmentType IN ('Image', 'Video', 'Document')),
        CONSTRAINT FK_RepairTicketAttachment_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairTicketAttachment_UploadedByUser FOREIGN KEY (UploadedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTicketAttachment_Ticket_Type_Sort' AND object_id = OBJECT_ID('dbo.RepairTicketAttachment')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicketAttachment_Ticket_Type_Sort
        ON dbo.RepairTicketAttachment (RepairTicketId ASC, AttachmentType ASC, SortOrder ASC);
GO

-- ── 5. dbo.RepairTechnicianAttendance ────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairTechnicianAttendance')
)
BEGIN
    CREATE TABLE dbo.RepairTechnicianAttendance (
        AttendanceId    INT             IDENTITY (1, 1) NOT NULL,
        EmployeeId       INT             NOT NULL,
        WorkDate         DATE            NOT NULL,
        TimeIn           DATETIME2 (2)   CONSTRAINT DF_RepairTechnicianAttendance_TimeIn DEFAULT (sysutcdatetime()) NOT NULL,
        TimeOut          DATETIME2 (2)   NULL,
        CreatedAt        DATETIME2 (2)   CONSTRAINT DF_RepairTechnicianAttendance_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_RepairTechnicianAttendance PRIMARY KEY CLUSTERED (AttendanceId ASC),
        CONSTRAINT UQ_RepairTechnicianAttendance_OneRowPerDay UNIQUE (EmployeeId, WorkDate),
        CONSTRAINT FK_RepairTechnicianAttendance_Employee FOREIGN KEY (EmployeeId) REFERENCES dbo.Employee (EmpId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairTechnicianAttendance_Employee_WorkDate' AND object_id = OBJECT_ID('dbo.RepairTechnicianAttendance')
)
    CREATE NONCLUSTERED INDEX IX_RepairTechnicianAttendance_Employee_WorkDate
        ON dbo.RepairTechnicianAttendance (EmployeeId ASC, WorkDate DESC);
GO
