-- Migration: Add index for ITCM reminder candidate queries
-- The GetTicketIdsNeedingReminderAsync query filters by Status + LastContactAt
-- and this index prevents full table scans on every scheduled run.

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_CallTicket_ReminderCandidates'
      AND object_id = OBJECT_ID('dbo.CallTicket')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_CallTicket_ReminderCandidates]
        ON [dbo].[CallTicket]([Status] ASC, [LastContactAt] ASC)
        INCLUDE([TicketId], [UpdatedAt], [LastReminderSentAt]);
END
