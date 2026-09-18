-- Migration: Delete evidence attachments (ticket-level and part-level), for multi-select
-- deletion in the Evidence Gallery. Both log a history row noting the removal.
-- CREATE OR ALTER is idempotent — safe to re-run.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_DeleteAttachment
    @AttachmentId      INT,
    @ChangedByUserId   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RepairTicketId INT, @FileName NVARCHAR(260), @AttachmentType VARCHAR(10);

    BEGIN TRAN;

    SELECT @RepairTicketId = RepairTicketId, @FileName = FileName, @AttachmentType = AttachmentType
    FROM dbo.RepairTicketAttachment WITH (UPDLOCK, HOLDLOCK)
    WHERE AttachmentId = @AttachmentId;

    IF @@ROWCOUNT = 0 THROW 51210, 'Attachment not found.', 1;

    DELETE FROM dbo.RepairTicketAttachment WHERE AttachmentId = @AttachmentId;

    INSERT dbo.RepairTicketHistory (RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairTicketId, @ChangedByUserId, 'AttachmentDeleted', @AttachmentType, NULL, @FileName);

    COMMIT;
END
GO

CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_DeletePartAttachment
    @PartAttachmentId  INT,
    @ChangedByUserId   INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @RepairPartId INT, @RepairTicketId INT, @FileName NVARCHAR(260), @AttachmentType VARCHAR(10), @PartDisplayName NVARCHAR(220);

    BEGIN TRAN;

    SELECT @RepairPartId = a.RepairPartId, @RepairTicketId = a.RepairTicketId,
           @FileName = a.FileName, @AttachmentType = a.AttachmentType, @PartDisplayName = p.PartDisplayName
    FROM dbo.RepairPartAttachment a WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.RepairPart p ON p.RepairPartId = a.RepairPartId
    WHERE a.PartAttachmentId = @PartAttachmentId;

    IF @@ROWCOUNT = 0 THROW 51211, 'Attachment not found.', 1;

    DELETE FROM dbo.RepairPartAttachment WHERE PartAttachmentId = @PartAttachmentId;

    INSERT dbo.RepairPartHistory (RepairPartId, RepairTicketId, ChangedByUserId, FieldName, OldValue, NewValue, Note)
    VALUES (@RepairPartId, @RepairTicketId, @ChangedByUserId, 'AttachmentDeleted', @AttachmentType, NULL, @PartDisplayName + ': ' + ISNULL(@FileName, @AttachmentType));

    COMMIT;
END
GO
