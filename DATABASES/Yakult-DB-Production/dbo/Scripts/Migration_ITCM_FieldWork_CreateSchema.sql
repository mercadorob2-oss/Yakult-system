-- Migration: IT Call Monitoring — Field Work (one visit per ticket, no GPS)
-- One CallFieldVisit per CallTicket (A=One only), with status lifecycle:
--   Scheduled → InTransit → CheckedIn → CheckedOut → Completed (Cancelled branch)
-- No GPS per your choice — time + notes + photos + signature only.
-- Append-only photos; history is mirrored to CallTicketHistory (FieldVisitStatus) for timeline.
-- Idempotent — safe to re-run.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CallFieldVisit' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.CallFieldVisit (
        FieldVisitId        INT             IDENTITY(1,1) NOT NULL,
        TicketId            INT             NOT NULL,
        TechnicianEmpId     INT             NULL,
        Status              NVARCHAR(20)    CONSTRAINT DF_CallFieldVisit_Status DEFAULT ('Scheduled') NOT NULL,
        ScheduledAt         DATETIME2(2)    NULL,
        CheckedInAt         DATETIME2(2)    NULL,
        CheckedOutAt        DATETIME2(2)    NULL,
        Notes               NVARCHAR(2000)  NULL,
        CustomerSignature   VARBINARY(MAX)  NULL, -- PNG bytes from SignaturePad
        CreatedByUserId     INT             NULL,
        CreatedAt           DATETIME2(2)    CONSTRAINT DF_CallFieldVisit_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UpdatedAt           DATETIME2(2)    CONSTRAINT DF_CallFieldVisit_UpdatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_CallFieldVisit PRIMARY KEY CLUSTERED (FieldVisitId ASC),
        CONSTRAINT FK_CallFieldVisit_Ticket FOREIGN KEY (TicketId) REFERENCES dbo.CallTicket(TicketId) ON DELETE CASCADE,
        CONSTRAINT FK_CallFieldVisit_Technician FOREIGN KEY (TechnicianEmpId) REFERENCES dbo.Employee(EmpId),
        CONSTRAINT FK_CallFieldVisit_CreatedBy FOREIGN KEY (CreatedByUserId) REFERENCES dbo.[User](UserId),
        CONSTRAINT CK_CallFieldVisit_Status CHECK (Status IN ('Scheduled','InTransit','CheckedIn','CheckedOut','Completed','Cancelled'))
    );
END
GO

-- One visit per ticket (choice 1.A)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_CallFieldVisit_OnePerTicket' AND object_id = OBJECT_ID('dbo.CallFieldVisit'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_CallFieldVisit_OnePerTicket ON dbo.CallFieldVisit(TicketId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallFieldVisit_Ticket_Status_CreatedAt' AND object_id = OBJECT_ID('dbo.CallFieldVisit'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallFieldVisit_Ticket_Status_CreatedAt ON dbo.CallFieldVisit(TicketId, Status, CreatedAt DESC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallFieldVisit_Technician_Status' AND object_id = OBJECT_ID('dbo.CallFieldVisit'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallFieldVisit_Technician_Status ON dbo.CallFieldVisit(TechnicianEmpId, Status);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'CallFieldVisitAttachment' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.CallFieldVisitAttachment (
        AttachmentId        INT             IDENTITY(1,1) NOT NULL,
        FieldVisitId        INT             NOT NULL,
        FileName            NVARCHAR(255)   NOT NULL,
        MimeType            NVARCHAR(100)   NULL,
        FileBytes           VARBINARY(MAX)  NULL,
        ThumbnailBytes      VARBINARY(MAX)  NULL,
        FileSizeBytes       INT             NULL,
        UploadedAt          DATETIME2(2)    CONSTRAINT DF_CallFieldVisitAttachment_UploadedAt DEFAULT (sysutcdatetime()) NOT NULL,
        UploadedByUserId    INT             NULL,
        CONSTRAINT PK_CallFieldVisitAttachment PRIMARY KEY CLUSTERED (AttachmentId ASC),
        CONSTRAINT FK_CallFieldVisitAttachment_Visit FOREIGN KEY (FieldVisitId) REFERENCES dbo.CallFieldVisit(FieldVisitId) ON DELETE CASCADE,
        CONSTRAINT FK_CallFieldVisitAttachment_UploadedBy FOREIGN KEY (UploadedByUserId) REFERENCES dbo.[User](UserId)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CallFieldVisitAttachment_Visit' AND object_id = OBJECT_ID('dbo.CallFieldVisitAttachment'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_CallFieldVisitAttachment_Visit ON dbo.CallFieldVisitAttachment(FieldVisitId, UploadedAt DESC);
END
GO

-- Keep UpdatedAt fresh (like CallTicket trigger)
IF OBJECT_ID('dbo.trg_CallFieldVisit_SetUpdatedAt', 'TR') IS NULL
BEGIN
    EXEC('CREATE TRIGGER dbo.trg_CallFieldVisit_SetUpdatedAt ON dbo.CallFieldVisit AFTER UPDATE AS BEGIN SET NOCOUNT ON; UPDATE v SET UpdatedAt = sysutcdatetime() FROM dbo.CallFieldVisit v INNER JOIN inserted i ON i.FieldVisitId = v.FieldVisitId; END');
END
GO

-- ── Procs ──

CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_Schedule
    @TicketId INT,
    @TechnicianEmpId INT = NULL,
    @ScheduledAt DATETIME2(2) = NULL,
    @Notes NVARCHAR(2000) = NULL,
    @CreatedByUserId INT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    IF @TicketId IS NULL OR NOT EXISTS (SELECT 1 FROM dbo.CallTicket WHERE TicketId=@TicketId)
        THROW 51010, 'Call ticket not found.', 1;
    IF EXISTS (SELECT 1 FROM dbo.CallFieldVisit WHERE TicketId=@TicketId)
        THROW 51011, 'This ticket already has a field visit (one per ticket).', 1;
    IF @TechnicianEmpId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM dbo.Employee WHERE EmpId=@TechnicianEmpId AND ISNULL(Active,1)=1)
        THROW 51012, 'Technician not found or inactive.', 1;

    INSERT dbo.CallFieldVisit (TicketId, TechnicianEmpId, Status, ScheduledAt, Notes, CreatedByUserId)
    VALUES (@TicketId, @TechnicianEmpId, 'Scheduled', @ScheduledAt, @Notes, @CreatedByUserId);

    DECLARE @Id INT = SCOPE_IDENTITY();

    -- Mirror to history for timeline in call-ticket-detail
    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @CreatedByUserId, 'FieldVisitStatus', NULL, 'Scheduled', @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CheckedInAt, CheckedOutAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId=@Id;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_Call_FieldVisit_SetStatus
    @FieldVisitId INT,
    @NewStatus NVARCHAR(20),
    @ChangedByUserId INT = NULL,
    @Notes NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    DECLARE @TicketId INT, @OldStatus NVARCHAR(20);
    SELECT @TicketId=TicketId, @OldStatus=Status FROM dbo.CallFieldVisit WITH (UPDLOCK, HOLDLOCK) WHERE FieldVisitId=@FieldVisitId;
    IF @TicketId IS NULL THROW 51013, 'Field visit not found.', 1;

    -- Allowed transitions (one-visit linear, no GPS)
    -- Scheduled → InTransit → CheckedIn → CheckedOut → Completed
    -- Any → Cancelled (except Completed/Cancelled terminal)
    IF @OldStatus IN ('Completed','Cancelled') THROW 51014, 'Completed/Cancelled visits cannot be changed.', 1;
    IF @NewStatus NOT IN ('InTransit','CheckedIn','CheckedOut','Completed','Cancelled') THROW 51015, 'Invalid status transition.', 1;

    -- Linear order check
    DECLARE @OrdOld INT = CASE @OldStatus WHEN 'Scheduled' THEN 0 WHEN 'InTransit' THEN 1 WHEN 'CheckedIn' THEN 2 WHEN 'CheckedOut' THEN 3 ELSE -1 END;
    DECLARE @OrdNew INT = CASE @NewStatus WHEN 'InTransit' THEN 1 WHEN 'CheckedIn' THEN 2 WHEN 'CheckedOut' THEN 3 WHEN 'Completed' THEN 4 WHEN 'Cancelled' THEN 99 ELSE -1 END;
    IF @NewStatus='Cancelled' AND @OrdOld>=0 GOTO DoUpdate;
    IF @OrdNew <> @OrdOld + 1 AND NOT (@OrdOld=2 AND @NewStatus='Completed') -- allow CheckedIn → Completed (skip CheckOut if quick)
        THROW 51016, 'Status must advance one step at a time.', 1;

DoUpdate:
    UPDATE dbo.CallFieldVisit
    SET Status=@NewStatus,
        CheckedInAt = CASE WHEN @NewStatus='CheckedIn' THEN sysutcdatetime() ELSE CheckedInAt END,
        CheckedOutAt = CASE WHEN @NewStatus='CheckedOut' THEN sysutcdatetime() ELSE CheckedOutAt END,
        Notes = COALESCE(@Notes, Notes)
    WHERE FieldVisitId=@FieldVisitId;

    INSERT dbo.CallTicketHistory (TicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@TicketId, @ChangedByUserId, 'FieldVisitStatus', @OldStatus, @NewStatus, @Notes);

    SELECT FieldVisitId, TicketId, TechnicianEmpId, Status, ScheduledAt, CheckedInAt, CheckedOutAt, Notes, CreatedByUserId, CreatedAt, UpdatedAt
    FROM dbo.CallFieldVisit WHERE FieldVisitId=@FieldVisitId;
END
GO
