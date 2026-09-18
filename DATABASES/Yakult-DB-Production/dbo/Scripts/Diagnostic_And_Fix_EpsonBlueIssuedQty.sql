-- Diagnostic + fix: EPSON 001 (BLUE) request's stale IssuedQty
-- Purpose : IssuedQty is computed once, at request-creation time, from whatever
--           StockOnHand looked like at that moment. If the item's catalog row was
--           still showing pooled/contaminated stock (from the duplicate-item issue)
--           when this request was created, IssuedQty got baked in as if the item
--           were fully issued — even though dbo.Item.StockOnHand has since correctly
--           settled to the true value. This re-syncs that one request's IssuedQty to
--           match the item's CURRENT StockOnHand.

-- Step 1 — see the current mismatch
SELECT
    r.ReqId,
    r.ItemId,
    i.Name,
    i.StockOnHand AS ItemCurrentStock,
    r.Quantity,
    r.IssuedQty AS IssuedQty_Stored,
    CASE
        WHEN r.IssuedQty >= r.Quantity THEN 'Looks Fulfilled (hidden from both pages)'
        WHEN r.IssuedQty > 0            THEN 'Partially Fulfilled'
        ELSE 'Unfulfilled'
    END AS CurrentDisplayBucket
FROM dbo.Request r
INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
WHERE r.ItemId = 2544;

-- Step 2 — re-sync IssuedQty to the item's actual current stock
-- (uncomment and run once Step 1 confirms the mismatch)
/*
UPDATE r
SET r.IssuedQty = CASE
                      WHEN i.StockOnHand >= r.Quantity THEN r.Quantity
                      WHEN i.StockOnHand > 0           THEN i.StockOnHand
                      ELSE 0
                  END
FROM dbo.Request r
INNER JOIN dbo.Item i ON i.ItemId = r.ItemId
WHERE r.ItemId = 2544;
*/
GO
