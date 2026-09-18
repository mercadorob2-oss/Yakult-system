-- Migration: Add a separate small ThumbnailBytes column to dbo.RepairTicketAttachment.
-- The ticket list query (GetTicketsAsync) previously pulled the FULL-resolution FileBytes for
-- every row's thumbnail via an OUTER APPLY — for a multi-megabyte evidence photo, just streaming
-- that one column back over the wire took 1.5-1.9 seconds per row (measured), even though the rest
-- of the query (connection open + execute) took under 15ms. That cost is paid on every list load
-- and scales with attachment count/size, not row count alone.
-- Fix: generate a small (max ~200px) JPEG thumbnail once at upload time (see
-- RepairTicketRepository.Tickets.cs AddAttachmentAsync) and store it here; the list query reads
-- ThumbnailBytes instead of FileBytes. Existing attachments uploaded before this migration have
-- NULL ThumbnailBytes and just show the placeholder icon in list views until re-uploaded — their
-- full-resolution image is untouched and still viewable in the Detail window.
-- Idempotent — safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RepairTicketAttachment') AND name = 'ThumbnailBytes'
)
BEGIN
    ALTER TABLE dbo.RepairTicketAttachment ADD ThumbnailBytes VARBINARY(MAX) NULL;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_AddAttachment
    @RepairTicketId    INT,
    @AttachmentType     VARCHAR(10),
    @FileName           NVARCHAR(260)   = NULL,
    @MimeType           NVARCHAR(100)   = NULL,
    @FileBytes          VARBINARY(MAX),
    @FileSizeBytes      INT             = NULL,
    @UploadedByUserId   INT             = NULL,
    @ThumbnailBytes     VARBINARY(MAX)  = NULL
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
        (RepairTicketId, AttachmentType, FileName, MimeType, FileBytes, FileSizeBytes, SortOrder, UploadedByUserId, ThumbnailBytes)
    VALUES
        (@RepairTicketId, @AttachmentType, @FileName, @MimeType, @FileBytes, @FileSizeBytes, @NextSortOrder, @UploadedByUserId, @ThumbnailBytes);

    SET @AttachmentId = SCOPE_IDENTITY();

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @UploadedByUserId, 'Attachment', NULL, @AttachmentType, @FileName);

    COMMIT;

    SELECT @AttachmentId AS AttachmentId;
END
GO
