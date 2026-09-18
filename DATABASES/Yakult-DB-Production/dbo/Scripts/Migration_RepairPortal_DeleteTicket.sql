-- Migration: Permanently delete a Repair Ticket, for correcting accidental intake mistakes
-- (e.g. the wrong item was selected). All child rows (RepairTicketHistory, RepairTicketNote,
-- RepairTicketAttachment, RepairItemObservation, RepairPart and its own children, RepairConclusion)
-- already cascade via ON DELETE CASCADE, so a single DELETE against dbo.RepairTicket is sufficient.
-- CREATE OR ALTER is idempotent — safe to re-run.

GO
CREATE OR ALTER PROCEDURE dbo.sp_RepairPortal_DeleteTicket
    @RepairTicketId INT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId)
        THROW 51200, 'Repair ticket not found.', 1;

    DELETE FROM dbo.RepairTicket WHERE RepairTicketId = @RepairTicketId;
END
GO
