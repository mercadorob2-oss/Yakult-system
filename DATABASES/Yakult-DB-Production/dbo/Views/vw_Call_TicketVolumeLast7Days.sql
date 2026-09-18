
CREATE   VIEW dbo.vw_Call_TicketVolumeLast7Days
AS
SELECT
    CONVERT(date, t.CreatedAt) AS [Day],
    COUNT(*) AS TicketCount
FROM dbo.CallTicket t
WHERE t.CreatedAt >= DATEADD(DAY, -6, CONVERT(date, SYSUTCDATETIME()))
GROUP BY CONVERT(date, t.CreatedAt);
