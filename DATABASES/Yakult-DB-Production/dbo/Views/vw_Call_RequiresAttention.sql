
CREATE   VIEW dbo.vw_Call_RequiresAttention
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
