-- Migration: Part-based repair workflow schema.
-- Adds: RepairItemObservation, RepairPart, RepairPartNote, RepairPartAttachment,
--       RepairPartHistory, RepairConclusion. Purely additive — no changes to existing
-- RepairTicket/RepairTicketHistory/RepairTicketNote/RepairTicketAttachment tables, which
-- stay scoped to whole-equipment evidence/general notes/ticket-wide timeline.
-- All statements are idempotent (guarded by IF NOT EXISTS) so this script is safe to re-run.

-- ── 1. dbo.RepairItemObservation ─────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairItemObservation')
)
BEGIN
    CREATE TABLE dbo.RepairItemObservation (
        ObservationId    INT             IDENTITY (1, 1) NOT NULL,
        RepairTicketId   INT             NOT NULL,
        SortOrder        INT             CONSTRAINT DF_RepairItemObservation_SortOrder DEFAULT (0) NOT NULL,
        ObservationText  NVARCHAR (2000) NOT NULL,
        CreatedAt        DATETIME2 (2)   CONSTRAINT DF_RepairItemObservation_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt        DATETIME2 (2)   CONSTRAINT DF_RepairItemObservation_UpdatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CreatedByUserId  INT             NULL,
        CONSTRAINT PK_RepairItemObservation PRIMARY KEY CLUSTERED (ObservationId ASC),
        CONSTRAINT FK_RepairItemObservation_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairItemObservation_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairItemObservation_Ticket_Sort' AND object_id = OBJECT_ID('dbo.RepairItemObservation')
)
    CREATE NONCLUSTERED INDEX IX_RepairItemObservation_Ticket_Sort
        ON dbo.RepairItemObservation (RepairTicketId ASC, SortOrder ASC);
GO

-- ── 2. dbo.RepairPart ─────────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairPart')
)
BEGIN
    CREATE TABLE dbo.RepairPart (
        RepairPartId        INT             IDENTITY (1, 1) NOT NULL,
        RepairTicketId       INT             NOT NULL,
        PartNumber           INT             NOT NULL,
        CustomLabel          NVARCHAR (200)  NULL,
        PartDisplayName      AS              ('Part #' + CONVERT(VARCHAR(10), PartNumber) +
                                               CASE WHEN CustomLabel IS NULL OR LTRIM(RTRIM(CustomLabel)) = '' THEN ''
                                                    ELSE ' - ' + CustomLabel END) PERSISTED,
        ProblemDescription   NVARCHAR (2000) NULL,
        Status               NVARCHAR (20)   CONSTRAINT DF_RepairPart_Status DEFAULT ('WaitingDiagnosis') NOT NULL,
        Severity             NVARCHAR (20)   CONSTRAINT DF_RepairPart_Severity DEFAULT ('Medium') NOT NULL,
        CreatedAt            DATETIME2 (2)   CONSTRAINT DF_RepairPart_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt            DATETIME2 (2)   CONSTRAINT DF_RepairPart_UpdatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CreatedByUserId      INT             NULL,
        RowVer               ROWVERSION      NOT NULL,
        CONSTRAINT PK_RepairPart PRIMARY KEY CLUSTERED (RepairPartId ASC),
        CONSTRAINT UQ_RepairPart_TicketPartNumber UNIQUE (RepairTicketId, PartNumber),
        CONSTRAINT CK_RepairPart_Status CHECK (Status IN ('WaitingDiagnosis', 'Diagnosing', 'Repairing', 'WaitingParts', 'Testing', 'Repaired', 'CannotRepair')),
        CONSTRAINT CK_RepairPart_Severity CHECK (Severity IN ('Low', 'Medium', 'High', 'Critical')),
        CONSTRAINT FK_RepairPart_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairPart_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairPart_Ticket_PartNumber' AND object_id = OBJECT_ID('dbo.RepairPart')
)
    CREATE NONCLUSTERED INDEX IX_RepairPart_Ticket_PartNumber
        ON dbo.RepairPart (RepairTicketId ASC, PartNumber ASC);
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.triggers WHERE name = 'trg_RepairPart_SetUpdatedAt' AND parent_id = OBJECT_ID('dbo.RepairPart')
)
EXEC ('
CREATE TRIGGER dbo.trg_RepairPart_SetUpdatedAt
ON dbo.RepairPart
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE p
    SET UpdatedAt = SYSUTCDATETIME()
    FROM dbo.RepairPart p
    INNER JOIN inserted i ON i.RepairPartId = p.RepairPartId;
END
');
GO

-- ── 3. dbo.RepairPartNote ─────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairPartNote')
)
BEGIN
    CREATE TABLE dbo.RepairPartNote (
        PartNoteId       BIGINT          IDENTITY (1, 1) NOT NULL,
        RepairPartId     INT             NOT NULL,
        RepairTicketId   INT             NOT NULL,
        NoteType         NVARCHAR (20)   CONSTRAINT DF_RepairPartNote_NoteType DEFAULT ('Diagnostic') NOT NULL,
        NoteText         NVARCHAR (4000) NOT NULL,
        CreatedAt        DATETIME2 (2)   CONSTRAINT DF_RepairPartNote_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CreatedByUserId  INT             NULL,
        CreatedByEmpId   INT             NULL,
        CONSTRAINT PK_RepairPartNote PRIMARY KEY CLUSTERED (PartNoteId ASC),
        CONSTRAINT CK_RepairPartNote_Type CHECK (NoteType IN ('Diagnostic', 'Repair')),
        CONSTRAINT FK_RepairPartNote_Part FOREIGN KEY (RepairPartId) REFERENCES dbo.RepairPart (RepairPartId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairPartNote_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId),
        CONSTRAINT FK_RepairPartNote_CreatedByUser FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User] (UserId),
        CONSTRAINT FK_RepairPartNote_CreatedByEmp FOREIGN KEY (CreatedByEmpId) REFERENCES dbo.Employee (EmpId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairPartNote_Part_Type_CreatedAt' AND object_id = OBJECT_ID('dbo.RepairPartNote')
)
    CREATE NONCLUSTERED INDEX IX_RepairPartNote_Part_Type_CreatedAt
        ON dbo.RepairPartNote (RepairPartId ASC, NoteType ASC, CreatedAt DESC);
GO

-- ── 4. dbo.RepairPartAttachment ──────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairPartAttachment')
)
BEGIN
    CREATE TABLE dbo.RepairPartAttachment (
        PartAttachmentId   INT             IDENTITY (1, 1) NOT NULL,
        RepairPartId       INT             NOT NULL,
        RepairTicketId     INT             NOT NULL,
        AttachmentType      VARCHAR (10)    NOT NULL,
        FileName            NVARCHAR (260)  NULL,
        MimeType            NVARCHAR (100)  NULL,
        FileBytes           VARBINARY (MAX) NOT NULL,
        FileSizeBytes       INT             NULL,
        SortOrder           INT             CONSTRAINT DF_RepairPartAttachment_SortOrder DEFAULT (0) NOT NULL,
        UploadedAt          DATETIME2 (2)   CONSTRAINT DF_RepairPartAttachment_UploadedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UploadedByUserId    INT             NULL,
        CONSTRAINT PK_RepairPartAttachment PRIMARY KEY CLUSTERED (PartAttachmentId ASC),
        CONSTRAINT CK_RepairPartAttachment_Type CHECK (AttachmentType IN ('Image', 'Video', 'Document')),
        CONSTRAINT FK_RepairPartAttachment_Part FOREIGN KEY (RepairPartId) REFERENCES dbo.RepairPart (RepairPartId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairPartAttachment_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId),
        CONSTRAINT FK_RepairPartAttachment_UploadedByUser FOREIGN KEY (UploadedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairPartAttachment_Part_Type_Sort' AND object_id = OBJECT_ID('dbo.RepairPartAttachment')
)
    CREATE NONCLUSTERED INDEX IX_RepairPartAttachment_Part_Type_Sort
        ON dbo.RepairPartAttachment (RepairPartId ASC, AttachmentType ASC, SortOrder ASC);
GO

-- ── 5. dbo.RepairPartHistory ─────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairPartHistory')
)
BEGIN
    CREATE TABLE dbo.RepairPartHistory (
        PartHistoryId    BIGINT          IDENTITY (1, 1) NOT NULL,
        RepairPartId     INT             NOT NULL,
        RepairTicketId   INT             NOT NULL,
        ChangedAt        DATETIME2 (2)   CONSTRAINT DF_RepairPartHistory_ChangedAt DEFAULT (sysutcdatetime()) NOT NULL,
        ChangedByUserId  INT             NULL,
        FieldName        NVARCHAR (50)   NOT NULL,
        OldValue         NVARCHAR (2000) NULL,
        NewValue         NVARCHAR (2000) NULL,
        Note             NVARCHAR (2000) NULL,
        CONSTRAINT PK_RepairPartHistory PRIMARY KEY CLUSTERED (PartHistoryId ASC),
        CONSTRAINT FK_RepairPartHistory_Part FOREIGN KEY (RepairPartId) REFERENCES dbo.RepairPart (RepairPartId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairPartHistory_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId),
        CONSTRAINT FK_RepairPartHistory_ChangedByUser FOREIGN KEY (ChangedByUserId) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_RepairPartHistory_Part_ChangedAt' AND object_id = OBJECT_ID('dbo.RepairPartHistory')
)
    CREATE NONCLUSTERED INDEX IX_RepairPartHistory_Part_ChangedAt
        ON dbo.RepairPartHistory (RepairPartId ASC, ChangedAt DESC);
GO

-- ── 6. dbo.RepairConclusion (1:1 with RepairTicket) ──────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.RepairConclusion')
)
BEGIN
    CREATE TABLE dbo.RepairConclusion (
        RepairTicketId     INT             NOT NULL,
        RootCause          NVARCHAR (2000) NULL,
        WorkPerformed      NVARCHAR (2000) NULL,
        FinalOutcome       NVARCHAR (2000) NULL,
        Recommendations    NVARCHAR (2000) NULL,
        CompletedByUserId  INT             NULL,
        CompletedByEmpId   INT             NULL,
        CompletedAt        DATETIME2 (2)   NULL,
        CreatedAt          DATETIME2 (2)   CONSTRAINT DF_RepairConclusion_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt          DATETIME2 (2)   CONSTRAINT DF_RepairConclusion_UpdatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_RepairConclusion PRIMARY KEY CLUSTERED (RepairTicketId ASC),
        CONSTRAINT FK_RepairConclusion_Ticket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId) ON DELETE CASCADE,
        CONSTRAINT FK_RepairConclusion_CompletedByUser FOREIGN KEY (CompletedByUserId) REFERENCES dbo.[User] (UserId),
        CONSTRAINT FK_RepairConclusion_CompletedByEmp FOREIGN KEY (CompletedByEmpId) REFERENCES dbo.Employee (EmpId)
    );
END
GO
