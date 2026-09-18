-- Migration: Stored procedures for the Repair Technician Portal
-- Mirrors the sp_Call_* procedures used by CallTicket (dbo.CallTicket) for the same shape
-- of writes: create ticket, set status, set priority, add note, add attachment, time in/out.
-- Reads are done as plain parameterized SQL in RepairTicketRepository, not stored procs.
-- CREATE OR ALTER is used so this script is safe to re-run.

-- ── sp_RepairPortal_CreateTicket ─────────────────────────────────────────────
GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_CreateTicket
    @ItemId             INT,
    @Problem             NVARCHAR(2000),
    @Priority            NVARCHAR(20)  = NULL,
    @SubmittedByEmpId    INT           = NULL,
    @SubmittedByUserId   INT           = NULL,
    @CreatedByUserId     INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @ItemId IS NULL THROW 51001, 'ItemId is required.', 1;
    SET @Problem = LTRIM(RTRIM(@Problem));
    IF @Problem IS NULL OR @Problem = '' THROW 51002, 'Problem is required.', 1;

    IF NOT EXISTS (SELECT 1 FROM dbo.Item WHERE ItemId = @ItemId)
        THROW 51003, 'Item not found.', 1;

    IF @Priority IS NULL OR LTRIM(RTRIM(@Priority)) = '' SET @Priority = 'Medium';
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
        THROW 51004, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    -- Resolve the item's current active Set (same "latest active set for item" join used by
    -- RepairedItemsPageViewModel.Actions.cs) so Set/Company/Branch/Department can be snapshotted.
    DECLARE @SetId INT, @SetCode NVARCHAR(20), @ComId INT, @BranchId INT, @DeptId INT;

    SELECT TOP (1)
        @SetId = aset.SetId,
        @SetCode = aset.SetCode,
        @ComId = s.ComId,
        @BranchId = s.CurrentBranchId,
        @DeptId = s.CurrentDepartmentId
    FROM (
        SELECT
            x.ItemId, x.SetId, x.SetCode,
            ROW_NUMBER() OVER (PARTITION BY x.ItemId ORDER BY x.SetCreatedAt DESC, x.SetId DESC) AS rn
        FROM (
            SELECT si.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
            FROM dbo.SetItem si
            INNER JOIN dbo.[Set] s ON s.SetId = si.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE s.Active = 1 AND archS.EntityId IS NULL

            UNION ALL

            SELECT r.ItemId, s.SetId, s.SetCode, s.CreatedAt AS SetCreatedAt
            FROM dbo.Request r
            INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
            LEFT JOIN dbo.ArchiveStatus archS ON archS.EntityType = 'Set' AND archS.EntityId = s.SetId AND archS.IsArchived = 1
            WHERE r.Active = 1 AND r.SetId IS NOT NULL AND s.Active = 1 AND archS.EntityId IS NULL
        ) x
    ) aset
    INNER JOIN dbo.[Set] s ON s.SetId = aset.SetId
    WHERE aset.ItemId = @ItemId AND aset.rn = 1;

    DECLARE @ItemNameSnapshot NVARCHAR(200), @ItemSerialSnapshot VARCHAR(255);
    SELECT @ItemNameSnapshot = Name, @ItemSerialSnapshot = SerialNumber
    FROM dbo.Item WHERE ItemId = @ItemId;

    DECLARE @RepairTicketId INT;

    BEGIN TRAN;

    INSERT dbo.RepairTicket
    (
        ItemId, SetId, SetCode, ComId, BranchId, DeptId,
        ItemNameSnapshot, ItemSerialSnapshot,
        Problem, Priority, Status,
        SubmittedByEmpId, SubmittedByUserId
    )
    VALUES
    (
        @ItemId, @SetId, @SetCode, @ComId, @BranchId, @DeptId,
        @ItemNameSnapshot, @ItemSerialSnapshot,
        @Problem, @PriorityCanonical, 'Waiting',
        @SubmittedByEmpId, @SubmittedByUserId
    );

    SET @RepairTicketId = SCOPE_IDENTITY();

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @CreatedByUserId, 'Created', NULL, NULL, 'Repair ticket created');

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES
        (@RepairTicketId, @CreatedByUserId, 'Status', NULL, 'Waiting'),
        (@RepairTicketId, @CreatedByUserId, 'Priority', NULL, @PriorityCanonical);

    COMMIT;

    SELECT
        t.RepairTicketId, t.TicketCode, t.Status, t.Priority, t.CreatedAt
    FROM dbo.RepairTicket t
    WHERE t.RepairTicketId = @RepairTicketId;
END
GO

-- ── sp_RepairPortal_SetTicketStatus ──────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetTicketStatus
    @RepairTicketId   INT,
    @NewStatus         NVARCHAR(20),
    @ChangedByUserId   INT            = NULL,
    @Note              NVARCHAR(2000) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewStatus = LTRIM(RTRIM(@NewStatus));
    IF @NewStatus IS NULL OR @NewStatus = '' THROW 51010, 'NewStatus is required.', 1;

    DECLARE @NewStatusCanonical NVARCHAR(20) =
        CASE UPPER(@NewStatus)
            WHEN 'WAITING' THEN 'Waiting'
            WHEN 'DIAGNOSING' THEN 'Diagnosing'
            WHEN 'REPAIRING' THEN 'Repairing'
            WHEN 'AWAITINGPARTS' THEN 'AwaitingParts'
            WHEN 'AWAITING PARTS' THEN 'AwaitingParts'
            WHEN 'TESTING' THEN 'Testing'
            WHEN 'COMPLETED' THEN 'Completed'
            WHEN 'UNREPAIRABLE' THEN 'Unrepairable'
            ELSE NULL
        END;

    IF @NewStatusCanonical IS NULL
        THROW 51011, 'Invalid status. Allowed: Waiting, Diagnosing, Repairing, AwaitingParts, Testing, Completed, Unrepairable.', 1;

    DECLARE @OldStatus NVARCHAR(20);

    BEGIN TRAN;

    SELECT @OldStatus = Status
    FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairTicketId = @RepairTicketId;

    IF @@ROWCOUNT = 0 THROW 51012, 'Repair ticket not found.', 1;

    IF @OldStatus = @NewStatusCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.RepairTicket
    SET Status = @NewStatusCanonical,
        CompletedAt = CASE
            WHEN @NewStatusCanonical IN ('Completed', 'Unrepairable') THEN COALESCE(CompletedAt, SYSUTCDATETIME())
            ELSE NULL
        END
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Status', @OldStatus, @NewStatusCanonical, @Note);

    COMMIT;
END
GO

-- ── sp_RepairPortal_SetTicketPriority ────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_SetTicketPriority
    @RepairTicketId   INT,
    @NewPriority       NVARCHAR(20),
    @ChangedByUserId   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NewPriority = LTRIM(RTRIM(@NewPriority));
    IF @NewPriority IS NULL OR @NewPriority = '' THROW 51020, 'NewPriority is required.', 1;

    DECLARE @NewPriorityCanonical NVARCHAR(20) =
        CASE UPPER(@NewPriority)
            WHEN 'LOW' THEN 'Low'
            WHEN 'MEDIUM' THEN 'Medium'
            WHEN 'HIGH' THEN 'High'
            WHEN 'CRITICAL' THEN 'Critical'
            ELSE NULL
        END;

    IF @NewPriorityCanonical IS NULL
        THROW 51021, 'Invalid priority. Allowed: Low, Medium, High, Critical.', 1;

    DECLARE @OldPriority NVARCHAR(20);

    BEGIN TRAN;

    SELECT @OldPriority = Priority
    FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK)
    WHERE RepairTicketId = @RepairTicketId;

    IF @@ROWCOUNT = 0 THROW 51022, 'Repair ticket not found.', 1;

    IF ISNULL(@OldPriority, '') = @NewPriorityCanonical
    BEGIN
        COMMIT;
        RETURN;
    END

    UPDATE dbo.RepairTicket
    SET Priority = @NewPriorityCanonical
    WHERE RepairTicketId = @RepairTicketId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@RepairTicketId, @ChangedByUserId, 'Priority', @OldPriority, @NewPriorityCanonical);

    COMMIT;
END
GO

-- ── sp_RepairPortal_AddTicketNote ────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddTicketNote
    @RepairTicketId    INT,
    @NoteText           NVARCHAR(4000),
    @NoteType           NVARCHAR(30) = 'Note',
    @CreatedByUserId    INT = NULL,
    @CreatedByEmpId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @NoteType = COALESCE(NULLIF(LTRIM(RTRIM(@NoteType)), ''), 'Note');
    SET @NoteText = LTRIM(RTRIM(@NoteText));

    IF @NoteText IS NULL OR @NoteText = ''
        THROW 51030, 'NoteText is required.', 1;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK) WHERE RepairTicketId = @RepairTicketId)
        THROW 51031, 'Repair ticket not found.', 1;

    INSERT dbo.RepairTicketNote (RepairTicketId, NoteType, NoteText, CreatedByUserId, CreatedByEmpId)
    VALUES (@RepairTicketId, @NoteType, @NoteText, @CreatedByUserId, @CreatedByEmpId);

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue)
    VALUES (@RepairTicketId, @CreatedByUserId, 'NoteAdded', NULL, @NoteType);

    COMMIT;

    SELECT SCOPE_IDENTITY() AS NoteId;
END
GO

-- ── sp_RepairPortal_AddAttachment ────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddAttachment
    @RepairTicketId    INT,
    @AttachmentType     VARCHAR(10),
    @FileName           NVARCHAR(260)   = NULL,
    @MimeType           NVARCHAR(100)   = NULL,
    @FileBytes          VARBINARY(MAX),
    @FileSizeBytes      INT             = NULL,
    @UploadedByUserId   INT             = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @AttachmentType NOT IN ('Image', 'Video', 'Document')
        THROW 51040, 'Invalid AttachmentType. Allowed: Image, Video, Document.', 1;

    IF @FileBytes IS NULL
        THROW 51041, 'FileBytes is required.', 1;

    BEGIN TRAN;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WITH (UPDLOCK, HOLDLOCK) WHERE RepairTicketId = @RepairTicketId)
        THROW 51042, 'Repair ticket not found.', 1;

    DECLARE @NextSortOrder INT;
    SELECT @NextSortOrder = ISNULL(MAX(SortOrder), -1) + 1
    FROM dbo.RepairTicketAttachment
    WHERE RepairTicketId = @RepairTicketId;

    DECLARE @AttachmentId INT;

    INSERT dbo.RepairTicketAttachment
        (RepairTicketId, AttachmentType, FileName, MimeType, FileBytes, FileSizeBytes, SortOrder, UploadedByUserId)
    VALUES
        (@RepairTicketId, @AttachmentType, @FileName, @MimeType, @FileBytes, @FileSizeBytes, @NextSortOrder, @UploadedByUserId);

    SET @AttachmentId = SCOPE_IDENTITY();

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @UploadedByUserId, 'Attachment', NULL, @AttachmentType, @FileName);

    COMMIT;

    SELECT @AttachmentId AS AttachmentId;
END
GO

-- ── sp_RepairPortal_TimeIn ───────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_TimeIn
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EmployeeId IS NULL THROW 51050, 'EmployeeId is required.', 1;

    DECLARE @WorkDate DATE = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time');
    DECLARE @AttendanceId INT;

    BEGIN TRAN;

    SELECT @AttendanceId = AttendanceId
    FROM dbo.RepairTechnicianAttendance WITH (UPDLOCK, HOLDLOCK)
    WHERE EmployeeId = @EmployeeId AND WorkDate = @WorkDate;

    IF @AttendanceId IS NULL
    BEGIN
        INSERT dbo.RepairTechnicianAttendance (EmployeeId, WorkDate)
        VALUES (@EmployeeId, @WorkDate);

        SET @AttendanceId = SCOPE_IDENTITY();
    END
    ELSE
    BEGIN
        UPDATE dbo.RepairTechnicianAttendance
        SET TimeOut = NULL
        WHERE AttendanceId = @AttendanceId;
    END

    COMMIT;

    SELECT AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
    FROM dbo.RepairTechnicianAttendance
    WHERE AttendanceId = @AttendanceId;
END
GO

-- ── sp_RepairPortal_TimeOut ──────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_TimeOut
    @EmployeeId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @EmployeeId IS NULL THROW 51060, 'EmployeeId is required.', 1;

    DECLARE @WorkDate DATE = CONVERT(DATE, SYSDATETIMEOFFSET() AT TIME ZONE 'Singapore Standard Time');
    DECLARE @AttendanceId INT;

    BEGIN TRAN;

    SELECT @AttendanceId = AttendanceId
    FROM dbo.RepairTechnicianAttendance WITH (UPDLOCK, HOLDLOCK)
    WHERE EmployeeId = @EmployeeId AND WorkDate = @WorkDate AND TimeOut IS NULL;

    IF @AttendanceId IS NULL
        THROW 51061, 'No open Time In session found for today.', 1;

    UPDATE dbo.RepairTechnicianAttendance
    SET TimeOut = SYSUTCDATETIME()
    WHERE AttendanceId = @AttendanceId;

    COMMIT;

    SELECT AttendanceId, EmployeeId, WorkDate, TimeIn, TimeOut
    FROM dbo.RepairTechnicianAttendance
    WHERE AttendanceId = @AttendanceId;
END
GO
