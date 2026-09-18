
CREATE   VIEW dbo.vw_Call_RequiresAttention
AS
SELECT *
FROM dbo.vw_Call_TicketList
WHERE Status NOT IN ('Solved', 'Resolved (Temporary)')
  AND (Priority IN ('High','Critical') OR ResponsiblePerson IS NULL);
