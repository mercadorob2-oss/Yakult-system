
CREATE   VIEW dbo.vw_Call_IssueTypeDistribution
AS
SELECT
    COALESCE(NULLIF(LTRIM(RTRIM(t.IssueType)), ''), 'Unspecified') AS IssueType,
    COUNT(*) AS TicketCount
FROM dbo.CallTicket t
WHERE t.Status NOT IN ('Solved', 'Resolved (Temporary)')
GROUP BY COALESCE(NULLIF(LTRIM(RTRIM(t.IssueType)), ''), 'Unspecified');
