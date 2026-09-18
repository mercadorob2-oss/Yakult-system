-- ============================================================================
-- Migration: Attention view and dashboard metrics stay watchable for
-- Service-Only temporaries.
--
-- HOW TO RUN (read this first):
--   1. Open this file in SSMS against the target database.
--   2. Press Ctrl+A to select ALL, then F5 to execute the ENTIRE script.
--   3. Do NOT highlight and run a fragment. In particular, never execute
--      starting from a LEFT JOIN / SELECT line - that produces
--      "Msg 156 Incorrect syntax near 'LEFT'" because a JOIN cannot open
--      a batch.
--   4. Success = both PRINT lines appear, zero red errors:
--        vw_Call_RequiresAttention updated (Service-Only temporaries watchable).
--        vw_Call_DashboardMetrics updated (Service-Only temporaries counted open).
--
-- WHAT IT DOES:
--   Temp tickets WITH issued parts remain parked (awaiting return);
--   Service-Only temporaries (no ReplacementNewItemId allocation rows) count
--   as open and appear in Requires Attention under the normal
--   priority/ownership rules.
--   Idempotent (CREATE OR ALTER) - safe to re-run as a whole.
-- ============================================================================
GO
CREATE OR ALTER VIEW dbo.vw_Call_RequiresAttention
AS
SELECT *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved')
  AND (
    Status <> 'Resolved (Temporary)'
    OR NOT EXISTS (
        SELECT 1 FROM dbo.CallTicketHistory h
        WHERE h.TicketId = vw_Call_TicketList.TicketId
          AND h.FieldName = 'ReplacementNewItemId'
          AND TRY_CONVERT(int, h.NewValue) > 0
    )
  )
  AND (Priority IN ('High','Critical') OR ResponsiblePerson IS NULL);
GO
PRINT 'vw_Call_RequiresAttention updated (Service-Only temporaries watchable).';
GO
CREATE OR ALTER VIEW dbo.vw_Call_DashboardMetrics
AS
SELECT
    SUM(CASE WHEN t.Status NOT IN ('Solved')
        AND (t.Status <> 'Resolved (Temporary)' OR alloc.TicketId IS NULL)
        THEN 1 ELSE 0 END) AS OpenTickets,
    SUM(CASE WHEN t.Status NOT IN ('Solved')
        AND (t.Status <> 'Resolved (Temporary)' OR alloc.TicketId IS NULL)
        AND t.Priority = 'Critical' THEN 1 ELSE 0 END) AS CriticalTickets,
    AVG(CASE WHEN t.SolvedAt IS NOT NULL THEN DATEDIFF(MINUTE, t.CreatedAt, t.SolvedAt) END) AS AvgResolutionMinutes,
    SUM(CASE WHEN CONVERT(date, t.CreatedAt) = CONVERT(date, SYSUTCDATETIME()) THEN 1 ELSE 0 END) AS TodaysVolume
FROM dbo.CallTicket t
LEFT JOIN (
    SELECT DISTINCT h.TicketId
    FROM dbo.CallTicketHistory h
    WHERE h.FieldName = 'ReplacementNewItemId'
      AND TRY_CONVERT(int, h.NewValue) > 0
) alloc ON alloc.TicketId = t.TicketId;
GO
PRINT 'vw_Call_DashboardMetrics updated (Service-Only temporaries counted open).';
GO
