-- Migration: Add IssuedQty to dbo.Request
-- Purpose : Tracks how much of a Request's Quantity has actually been issued to date.
--           Staff encode Requests from a physical requisition slip and check stock at that
--           moment; if stock is short they issue what's available and leave the remainder
--           outstanding. IssuedQty vs Quantity drives the Partially Fulfilled / Unfulfilled
--           Requests views (mirrors the Cartridge Management fulfillment tracking, but a plain
--           Request row is already the finest-grained unit so no child table is needed).
-- Note    : IssuedQty = 0            -> Unfulfilled
--           0 < IssuedQty < Quantity -> Partially Fulfilled
--           IssuedQty >= Quantity    -> Fulfilled

IF NOT EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Request')
      AND name = N'IssuedQty'
)
BEGIN
    ALTER TABLE dbo.Request
        ADD [IssuedQty] INT NOT NULL CONSTRAINT [DF_Request_IssuedQty] DEFAULT (0);

    PRINT 'Column IssuedQty added to dbo.Request.';
END
ELSE
BEGIN
    PRINT 'Column IssuedQty already exists on dbo.Request. Skipping.';
END
GO
