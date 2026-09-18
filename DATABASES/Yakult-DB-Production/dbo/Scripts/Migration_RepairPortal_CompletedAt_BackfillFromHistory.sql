-- Migration: one-time data backfill for tickets affected by the CompletedAt "stale timestamp" bug
-- fixed in Migration_RepairPortal_SetTicketStatus_CompletedAt_AlwaysFresh.sql.
--
-- Unlike a blanket "set every terminal ticket's CompletedAt to now" (which would corrupt tickets
-- that already have a correct value), this recomputes CompletedAt from dbo.RepairTicketHistory —
-- which was NEVER affected by the bug (each row's ChangedAt is stamped with SYSUTCDATETIME() at
-- INSERT time, unconditionally, regardless of what happened to RepairTicket.CompletedAt). For every
-- ticket currently sitting in a terminal status, this sets CompletedAt to the ChangedAt of its most
-- recent "Status -> Completed/Unrepairable" history row — the genuine, authoritative timestamp of
-- when it actually last became terminal.
--
-- Idempotent — safe to re-run (always recomputes to the same correct source-of-truth value).

;WITH LatestTerminalHistory AS (
    SELECT
        h.RepairTicketId,
        h.ChangedAt,
        ROW_NUMBER() OVER (PARTITION BY h.RepairTicketId ORDER BY h.ChangedAt DESC, h.HistoryId DESC) AS rn
    FROM dbo.RepairTicketHistory h
    WHERE h.FieldName = 'Status' AND h.NewValue IN ('Completed', 'Unrepairable')
)
UPDATE t
SET t.CompletedAt = lth.ChangedAt
FROM dbo.RepairTicket t
INNER JOIN LatestTerminalHistory lth ON lth.RepairTicketId = t.RepairTicketId AND lth.rn = 1
WHERE t.Status IN ('Completed', 'Unrepairable')
  AND (t.CompletedAt IS NULL OR t.CompletedAt <> lth.ChangedAt);
