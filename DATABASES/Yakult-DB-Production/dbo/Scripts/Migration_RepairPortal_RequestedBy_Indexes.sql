-- Migration: Index dbo.RepairTicket's RequestedByComId/RequestedByBranchId/RequestedByDeptId.
-- GetTicketsAsync's Company/Branch/Department filter matches whichever the Requested By columns
-- resolve to first (ISNULL(RequestedByComId, ComId) etc. — see RepairTicketRepository.Tickets.cs),
-- mirroring what's actually shown in the Company/Branch/Department columns. The existing
-- IX_RepairTicket_ComBranchDept index only covers ComId/BranchId/DeptId, so it can't help that half
-- of the predicate at all. Adds a matching index on the RequestedBy* columns so both halves of the
-- SARGable OR (see the query) have an index to seek.
-- Idempotent — safe to re-run.

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_RepairTicket_RequestedByComBranchDept' AND object_id = OBJECT_ID('dbo.RepairTicket')
)
    CREATE NONCLUSTERED INDEX IX_RepairTicket_RequestedByComBranchDept
        ON dbo.RepairTicket (RequestedByComId ASC, RequestedByBranchId ASC, RequestedByDeptId ASC);
GO
