-- Migration: Add index to support CartridgeExchange/History (Fulfilled Cartridge Exchange) performance
--
-- GetFulfilledCartridgeHistoryAsync filters dbo.[Set] on CreatedAt >= DATEADD(day, -365, GETDATE()).
-- dbo.[Set] has no index on CreatedAt, so this forces a full clustered index scan of the table
-- (which also carries a VARBINARY(MAX) QRImageData column, making the scan expensive). For every
-- row scanned, a CROSS APPLY into dbo.Request and an OUTER APPLY (STRING_AGG) into
-- dbo.CartridgeRequestModel are also evaluated, compounding the cost. This causes the query to
-- exceed the default 30s SqlCommand timeout as the Set table grows (SqlException: Execution
-- Timeout Expired / Win32Exception: The wait operation timed out).

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[Set]')
      AND name = 'IX_Set_CreatedAt'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Set_CreatedAt
        ON dbo.[Set] (CreatedAt DESC)
        INCLUDE (SetCode, ReqId, IssuedBrandNewQty, IssuedRefilledQty, Status);
    PRINT 'Index IX_Set_CreatedAt created on dbo.[Set]';
END
ELSE
    PRINT 'Index IX_Set_CreatedAt already exists — skipped';
