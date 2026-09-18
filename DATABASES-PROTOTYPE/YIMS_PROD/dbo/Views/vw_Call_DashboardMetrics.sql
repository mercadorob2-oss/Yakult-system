
CREATE   VIEW dbo.vw_Call_DashboardMetrics
AS
SELECT
    SUM(CASE WHEN t.Status NOT IN ('Solved', 'Resolved (Temporary)') THEN 1 ELSE 0 END) AS OpenTickets,
    SUM(CASE WHEN t.Status NOT IN ('Solved', 'Resolved (Temporary)') AND t.Priority = 'Critical' THEN 1 ELSE 0 END) AS CriticalTickets,
    AVG(CASE WHEN t.SolvedAt IS NOT NULL THEN DATEDIFF(MINUTE, t.CreatedAt, t.SolvedAt) END) AS AvgResolutionMinutes,
    SUM(CASE WHEN CONVERT(date, t.CreatedAt) = CONVERT(date, SYSUTCDATETIME()) THEN 1 ELSE 0 END) AS TodaysVolume
FROM dbo.CallTicket t;
