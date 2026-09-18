-- Migration: Stored procedures for the part-based repair workflow.
-- Writes go through procs (matches sp_RepairPortal_* / sp_Call_* convention); reads are plain
-- parameterized SQL in RepairTicketRepository.Parts.cs.
-- CREATE OR ALTER is used so this script is safe to re-run.

-- ── sp_RepairPortal_RollUpTicketStatus (internal helper, called by the Part procs below) ────
GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_RollUpTicketStatus
    @RepairTicketId   INT,
    @ChangedByUserId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairPart WHERE RepairTicketId = @RepairTicketId)
        RETURN; -- no parts yet: ticket Status stays whatever it was (manual)

    DECLARE @PartCount INT, @RepairedCount INT, @CannotRepairCount INT, @TerminalCount INT;

    SELECT
        @PartCount = COUNT(*),
        @RepairedCount = SUM(CASE WHEN Status = 'Repaired' THEN 1 ELSE 0 END),
        @CannotRepairCount = SUM(CASE WHEN Status = 'CannotRepair' THEN 1 ELSE 0 END)
    FROM dbo.RepairPart
    WHERE RepairTicketId = @RepairTicketId;

    SET @TerminalCount = @RepairedCount + @CannotRepairCount;

    DECLARE @NewStatus NVARCHAR(20);

    IF @TerminalCount = @PartCount
    BEGIN
        -- All parts are terminal: Completed if any part was actually repaired, else every
        -- part was unrepairable.
        SET @NewStatus = CASE WHEN @RepairedCount > 0 THEN 'Completed' ELSE 'Unrepairable' END;
    END
    ELSE
    BEGIN
        -- >= 1 active part: show the least-advanced one (lowest rank wins).
        SELECT TOP (1) @NewStatus =
            CASE Status
                WHEN 'WaitingDiagnosis' THEN 'Waiting'
                WHEN 'Diagnosing'       THEN 'Diagnosing'
                WHEN 'Repairing'        THEN 'Repairing'
                WHEN 'WaitingParts'     THEN 'AwaitingParts'
                WHEN 'Testing'          THEN 'Testing'
            END
        FROM dbo.RepairPart
        WHERE RepairTicketId = @RepairTicketId
          AND Status NOT IN ('Repaired', 'CannotRepair')
        ORDER BY
            CASE Status
                WHEN 'WaitingDiagnosis' THEN 0
                WHEN 'Diagnosing'       THEN 1
                WHEN 'Repairing'        THEN 2
                WHEN 'WaitingParts'     THEN 3
                WHEN 'Testing'          THEN 4
                ELSE 5
            END ASC;
    END

    DECLARE @OldStatus NVARCHAR(20);
    SELECT @OldStatus = Status FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId;

    IF @NewStatus IS NULL OR @NewStatus = @OldStatus
        RETURN;

    UPDATE dbo.RepairTicket
    SET Status = @NewStatus,
        CompletedAt = CASE WHEN @NewStatus IN ('Completed', 'Unrepairable') THEN COALESCE(CompletedAt, SYSUTCDATETIME()) ELSE NULL END
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatus, 'Auto (parts roll-up)');
END
GO

-- ── Observations ──────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddObservation
    @RepairTicketId   INT,
    @ObservationText  NVARCHAR(2000),
    @CreatedByUserId  INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ObservationText = LTRIM(RTRIM(@ObservationText));
    IF @ObservationText IS NULL OR @ObservationText = ''
        THROW 51100, 'ObservationText is required.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51101, 'Repair ticket not found.', 1;

    DECLARE @NextSort INT;
    SELECT @NextSort = ISNULL(MAX(SortOrder), -1) + 1
    FROM dbo.RepairItemObservation
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairItemObservation (RepairTicketId, SortOrder, ObservationText, CreatedByUserId)
    VALUES (@RepairTicketId, @NextSort, @ObservationText, @CreatedByUserId);

    SELECT SCOPE_IDENTITY() AS ObservationId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_UpdateObservation
    @ObservationId    INT,
    @ObservationText  NVARCHAR(2000)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @ObservationText = LTRIM(RTRIM(@ObservationText));
    IF @ObservationText IS NULL OR @ObservationText = ''
        THROW 51102, 'ObservationText is required.', 1;

    UPDATE dbo.RepairItemObservation
    SET ObservationText = @ObservationText,
        UpdatedAt = SYSUTCDATETIME()
    WHERE ObservationId = @ObservationId;

    IF @@ROWCOUNT = 0
        THROW 51103, 'Observation not found.', 1;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_DeleteObservation
    @ObservationId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DELETE FROM dbo.RepairItemObservation WHERE ObservationId = @ObservationId;

    IF @@ROWCOUNT = 0
        THROW 51104, 'Observation not found.', 1;
END
GO

-- ── Parts ─────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_CreatePart
    @RepairTicketId      INT,
    @CustomLabel          NVARCHAR(200) = NULL,
    @ProblemDescription   NVARCHAR(2000) = NULL,
    @Severity             NVARCHAR(20) = NULL,
    @CreatedByUserId      INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51110, 'Repair ticket not found.', 1;

    IF @Severity IS NULL OR LTRIM(RTRIM(@Severity)) = '' SET @Severity = 'Medium';
    SET @Severity = LTRIM(RTRIM(@Severity));

    DECLARE @SeverityCanonical NVARCHAR(20) =
        CASE UPPER(@Severity)
            WHEN 'LOW' THEN 'Low'
            WHEN 'MEDIUM' THEN 'Medium'
            WHEN 'HIGH' THEN 'High'
            WHEN 'CRITICAL' THEN 'Critical'
            ELSE NULL
        END;

    IF @SeverityCanonical IS NULL
        THROW 51111, 'Invalid severity. Allowed: Low, Medium, High, Critical.', 1;

    DECLARE @RepairPartId INT, @PartNumber INT, @PartDisplayName NVARCHAR(220);

    BEGIN TRAN;

    SELECT @PartNumber = ISNULL(MAX(PartNumber), 0) + 1
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairPart (RepairTicketId, PartNumber, CustomLabel, ProblemDescription, Status, Severity, CreatedByUserId)
    VALUES (@RepairTicketId, @PartNumber, NULLIF(LTRIM(RTRIM(@CustomLabel)), ''), @ProblemDescription, 'WaitingDiagnosis', @SeverityCanonical, @CreatedByUserId);

    SET @RepairPartId = SCOPE_IDENTITY();

    SELECT @PartDisplayName = PartDisplayName FROM dbo.RepairPart WHERE RepairPartId = @RepairPartId;

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairPartId, @RepairTicketId, @CreatedByUserId, 'Created', NULL, NULL, @PartDisplayName + ' created');

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@RepairTicketId, @CreatedByUserId, 'PartCreated', NULL, @PartDisplayName);

    COMMIT;

    EXEC dbo.sp_RepairPortal_RollUpTicketStatus @RepairTicketId = @RepairTicketId, @ChangedByUserId = @CreatedByUserId;

    SELECT
        p.RepairPartId, p.RepairTicketId, p.PartNumber, p.CustomLabel, p.PartDisplayName,
        p.ProblemDescription, p.Status, p.Severity, p.CreatedAt, p.UpdatedAt
    FROM dbo.RepairPart p
    WHERE p.RepairPartId = @RepairPartId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetPartStatus
    @RepairPartId      INT,
    @NewStatus          NVARCHAR(20),
    @ChangedByUserId    INT = NULL,
    @Note               NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewStatus = LTRIM(RTRIM(@NewStatus));
    IF @NewStatus IS NULL OR @NewStatus = '' THROW 51120, 'NewStatus is required.', 1;

    DECLARE @NewStatusCanonical NVARCHAR(20) =
        CASE @NewStatus
            WHEN 'WaitingDiagnosis' THEN 'WaitingDiagnosis'
            WHEN 'Diagnosing'       THEN 'Diagnosing'
            WHEN 'Repairing'        THEN 'Repairing'
            WHEN 'WaitingParts'     THEN 'WaitingParts'
            WHEN 'Testing'          THEN 'Testing'
            WHEN 'Repaired'         THEN 'Repaired'
            WHEN 'CannotRepair'     THEN 'CannotRepair'
            ELSE NULL
        END;

    IF @NewStatusCanonical IS NULL
        THROW 51121, 'Invalid part status. Allowed: WaitingDiagnosis, Diagnosing, Repairing, WaitingParts, Testing, Repaired, CannotRepair.', 1;

    DECLARE @RepairTicketId INT, @OldStatus NVARCHAR(20), @PartDisplayName NVARCHAR(220);

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @OldStatus = Status, @PartDisplayName = PartDisplayName
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairPartId = @RepairPartId;

    IF @@ROWCOUNT = 0 THROW 51122, 'Part not found.', 1;

    IF @OldStatus = @NewStatusCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.RepairPart
    SET Status = @NewStatusCanonical
    WHERE RepairPartId = @RepairPartId;

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairPartId, @RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'PartStatus', @OldStatus, @NewStatusCanonical, @PartDisplayName);

    COMMIT;

    EXEC dbo.sp_RepairPortal_RollUpTicketStatus @RepairTicketId = @RepairTicketId, @ChangedByUserId = @ChangedByUserId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_UpdatePartLabel
    @RepairPartId      INT,
    @NewCustomLabel     NVARCHAR(200) = NULL,
    @ChangedByUserId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RepairTicketId INT, @OldLabel NVARCHAR(200);

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @OldLabel = CustomLabel
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairPartId = @RepairPartId;

    IF @@ROWCOUNT = 0 THROW 51130, 'Part not found.', 1;

    UPDATE dbo.RepairPart
    SET CustomLabel = NULLIF(LTRIM(RTRIM(@NewCustomLabel)), '')
    WHERE RepairPartId = @RepairPartId;

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@RepairPartId, @RepairTicketId, @ChangedByUserId, 'CustomLabel', @OldLabel, @NewCustomLabel);

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_DeletePart
    @RepairPartId      INT,
    @ChangedByUserId    INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RepairTicketId INT, @PartDisplayName NVARCHAR(220);

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @PartDisplayName = PartDisplayName
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairPartId = @RepairPartId;

    IF @@ROWCOUNT = 0 THROW 51140, 'Part not found.', 1;

    DELETE FROM dbo.RepairPart WHERE RepairPartId = @RepairPartId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@RepairTicketId, @ChangedByUserId, 'PartDeleted', @PartDisplayName, NULL);

    COMMIT;

    EXEC dbo.sp_RepairPortal_RollUpTicketStatus @RepairTicketId = @RepairTicketId, @ChangedByUserId = @ChangedByUserId;
END
GO

-- ── Part notes / attachments ─────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddPartNote
    @RepairPartId      INT,
    @NoteType           NVARCHAR(20),
    @NoteText           NVARCHAR(4000),
    @CreatedByUserId    INT = NULL,
    @CreatedByEmpId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NoteType = LTRIM(RTRIM(@NoteType));
    IF @NoteType NOT IN ('Diagnostic', 'Repair')
        THROW 51150, 'Invalid NoteType. Allowed: Diagnostic, Repair.', 1;

    SET @NoteText = LTRIM(RTRIM(@NoteText));
    IF @NoteText IS NULL OR @NoteText = ''
        THROW 51151, 'NoteText is required.', 1;

    DECLARE @RepairTicketId INT, @PartDisplayName NVARCHAR(220);

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @PartDisplayName = PartDisplayName
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairPartId = @RepairPartId;

    IF @@ROWCOUNT = 0 THROW 51152, 'Part not found.', 1;

    INSERT dbo.RepairPartNote (RepairPartId, RepairTicketId, NoteType, NoteText, CreatedByUserId, CreatedByEmpId)
    VALUES (@RepairPartId, @RepairTicketId, @NoteType, @NoteText, @CreatedByUserId, @CreatedByEmpId);

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairPartId, @RepairTicketId, @CreatedByUserId, @NoteType + 'NoteAdded', NULL, NULL, @PartDisplayName);

    COMMIT;

    SELECT SCOPE_IDENTITY() AS PartNoteId;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddPartAttachment
    @RepairPartId      INT,
    @AttachmentType     VARCHAR(10),
    @FileName           NVARCHAR(260) = NULL,
    @MimeType           NVARCHAR(100) = NULL,
    @FileBytes          VARBINARY(MAX),
    @FileSizeBytes      INT = NULL,
    @UploadedByUserId   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AttachmentType NOT IN ('Image', 'Video', 'Document')
        THROW 51160, 'Invalid AttachmentType. Allowed: Image, Video, Document.', 1;

    IF @FileBytes IS NULL
        THROW 51161, 'FileBytes is required.', 1;

    DECLARE @RepairTicketId INT, @PartDisplayName NVARCHAR(220), @NextSortOrder INT, @PartAttachmentId INT;

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @PartDisplayName = PartDisplayName
    FROM dbo.RepairPart WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairPartId = @RepairPartId;

    IF @@ROWCOUNT = 0 THROW 51162, 'Part not found.', 1;

    SELECT @NextSortOrder = ISNULL(MAX(SortOrder), -1) + 1
    FROM dbo.RepairPartAttachment
    WHERE RepairPartId = @RepairPartId;

    INSERT dbo.RepairPartAttachment
        (RepairPartId, RepairTicketId, AttachmentType, FileName, MimeType, FileBytes, FileSizeBytes, SortOrder, UploadedByUserId)
    VALUES
        (@RepairPartId, @RepairTicketId, @AttachmentType, @FileName, @MimeType, @FileBytes, @FileSizeBytes, @NextSortOrder, @UploadedByUserId);

    SET @PartAttachmentId = SCOPE_IDENTITY();

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairPartId, @RepairTicketId, @UploadedByUserId, 'Attachment', NULL, @AttachmentType, @PartDisplayName + ': ' + ISNULL(@FileName, @AttachmentType));

    COMMIT;

    SELECT @PartAttachmentId AS PartAttachmentId;
END
GO

-- ── Repair Conclusion (upsert) ───────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SaveConclusion
    @RepairTicketId      INT,
    @RootCause            NVARCHAR(2000) = NULL,
    @WorkPerformed        NVARCHAR(2000) = NULL,
    @FinalOutcome         NVARCHAR(2000) = NULL,
    @Recommendations      NVARCHAR(2000) = NULL,
    @CompletedByUserId    INT = NULL,
    @CompletedByEmpId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51170, 'Repair ticket not found.', 1;

    BEGIN TRAN;

    IF EXISTS (SELECT 1 FROM dbo.RepairConclusion WHERE RepairTicketId = @RepairTicketId)
    BEGIN
        UPDATE dbo.RepairConclusion
        SET RootCause = @RootCause,
            WorkPerformed = @WorkPerformed,
            FinalOutcome = @FinalOutcome,
            Recommendations = @Recommendations,
            CompletedByUserId = @CompletedByUserId,
            CompletedByEmpId = @CompletedByEmpId,
            CompletedAt = COALESCE(CompletedAt, SYSUTCDATETIME()),
            UpdatedAt = SYSUTCDATETIME()
        WHERE RepairTicketId = @RepairTicketId;
    END
    ELSE
    BEGIN
        INSERT dbo.RepairConclusion
            (RepairTicketId, RootCause, WorkPerformed, FinalOutcome, Recommendations, CompletedByUserId, CompletedByEmpId, CompletedAt)
        VALUES
            (@RepairTicketId, @RootCause, @WorkPerformed, @FinalOutcome, @Recommendations, @CompletedByUserId, @CompletedByEmpId, SYSUTCDATETIME());
    END

    COMMIT;
END
GO
