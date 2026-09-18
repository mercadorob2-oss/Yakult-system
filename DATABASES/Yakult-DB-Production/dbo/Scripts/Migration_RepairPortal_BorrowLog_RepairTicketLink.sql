-- Migration_RepairPortal_BorrowLog_RepairTicketLink.sql
-- Ties a dbo.BorrowLog row back to the Repair Portal ticket that spawned it, for the new
-- "Spare Item" feature (technician temporarily loans a requester a spare while a ticket is
-- Repairing, reusing the existing Borrow Items feature instead of a bespoke table). Without this
-- column there is no way to find/auto-return "this ticket's open spare" from RepairTicketRepository.
-- No cascade delete — a ticket with an open/linked loan should be unlinked/returned first rather
-- than silently orphaning BorrowLog history.
-- Idempotent, safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.BorrowLog') AND name = 'RepairTicketId'
)
BEGIN
    ALTER TABLE dbo.BorrowLog ADD RepairTicketId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_BorrowLog_RepairTicket'
)
BEGIN
    ALTER TABLE dbo.BorrowLog
        ADD CONSTRAINT FK_BorrowLog_RepairTicket FOREIGN KEY (RepairTicketId) REFERENCES dbo.RepairTicket (RepairTicketId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_BorrowLog_RepairTicketId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_BorrowLog_RepairTicketId
        ON dbo.BorrowLog (RepairTicketId ASC)
        INCLUDE (ReturnedAtUtc, BorrowedAtUtc);
END
GO
