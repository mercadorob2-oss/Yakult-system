-- Backfill: Request.IssuedQty for Requests whose Set was already Dispatched
-- Purpose : Request.IssuedQty defaults to 0 for every row created before that column
--           existed. For requests whose Set has Status = 'Dispatched', the items were
--           actually handed to the requester through the normal Set dispatch workflow
--           long before IssuedQty existed — so those rows incorrectly show up as
--           Unfulfilled / Partially Fulfilled in the new fulfillment-tracking views.
-- Note    : Safe to re-run — the WHERE clause only touches rows that still need fixing,
--           so a second run is a no-op.

UPDATE r
SET r.IssuedQty = r.Quantity
FROM dbo.Request r
INNER JOIN dbo.[Set] s ON s.SetId = r.SetId
WHERE s.Status = 'Dispatched'
  AND r.IssuedQty < r.Quantity;

PRINT 'Backfilled IssuedQty for Requests linked to Dispatched Sets.';
GO
