
CREATE   VIEW dbo.vw_Call_DashboardMetrics
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
