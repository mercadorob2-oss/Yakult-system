-- Cleanup template: merge a duplicate/dummy dbo.Item row into the real one
-- Purpose : Run Diagnostic_DuplicateItem_EpsonInkExample.sql first to find the ItemId
--           of the REAL catalog row (@KeepItemId) and the dummy/workaround row you
--           created (@RemoveItemId). This script:
--             1) Moves any Requests pointing at the dummy row over to the real row
--             2) Sets the real row's StockOnHand to the true value you supply
--             3) Deactivates the dummy row (soft delete — Active = 0), it is NOT
--                physically deleted so history/audit trails stay intact
-- Usage   : Fill in the three @-variables below, review the SELECT preview output,
--           then uncomment and run the UPDATE/statements at the bottom.

DECLARE @KeepItemId    INT = /* the real item's ItemId, e.g. */ NULL;
DECLARE @RemoveItemId  INT = /* the dummy item's ItemId, e.g. */ NULL;
DECLARE @CorrectStock  INT = 0; -- the TRUE current stock for the kept item

IF @KeepItemId IS NULL OR @RemoveItemId IS NULL
BEGIN
    PRINT 'Set @KeepItemId and @RemoveItemId before running this script.';
    RETURN;
END

-- ── Preview: requests that will be reassigned ───────────────────────────────
SELECT ReqId, ItemId, Quantity, IssuedQty, Status, DateCreated
FROM dbo.Request
WHERE ItemId = @RemoveItemId;

-- ── Preview: the two item rows being merged ─────────────────────────────────
SELECT ItemId, Name, Category, StockOnHand, Active
FROM dbo.Item
WHERE ItemId IN (@KeepItemId, @RemoveItemId);

-- ── Uncomment below once the previews above look correct ────────────────────

/*
BEGIN TRANSACTION;

-- 1) Reassign any requests tied to the dummy item over to the real item
UPDATE dbo.Request
SET ItemId = @KeepItemId
WHERE ItemId = @RemoveItemId;

-- 2) Correct the real item's stock to the true value
UPDATE dbo.Item
SET StockOnHand = @CorrectStock
WHERE ItemId = @KeepItemId;

-- 3) Deactivate the dummy item (soft delete, not physically removed)
UPDATE dbo.Item
SET Active = 0
WHERE ItemId = @RemoveItemId;

COMMIT TRANSACTION;

PRINT 'Merged dummy item ' + CAST(@RemoveItemId AS NVARCHAR(20))
    + ' into ' + CAST(@KeepItemId AS NVARCHAR(20))
    + ', StockOnHand corrected to ' + CAST(@CorrectStock AS NVARCHAR(20)) + '.';
*/
GO
