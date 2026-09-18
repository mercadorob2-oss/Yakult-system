-- Migration: Drop receive-tracking columns from RefillTransaction
-- Reason: Refilled cartridges are no longer tracked as a return from vendor.
-- They are entered as new stock via BatchAddItemDialog (origin = Refilled).
-- The RefillTransaction is now a lightweight outbound-only log entry.

-- 1. Drop the check constraint that references ReceivedQty
IF EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_RefillTransaction_Qty'
      AND parent_object_id = OBJECT_ID('dbo.RefillTransaction')
)
BEGIN
    ALTER TABLE dbo.RefillTransaction DROP CONSTRAINT [CK_RefillTransaction_Qty];
END

-- 2. Add back a simpler SentQty-only check
IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints
    WHERE name = 'CK_RefillTransaction_Qty'
      AND parent_object_id = OBJECT_ID('dbo.RefillTransaction')
)
BEGIN
    ALTER TABLE dbo.RefillTransaction
        ADD CONSTRAINT [CK_RefillTransaction_Qty] CHECK ([SentQty] > 0);
END

-- 3. Drop default constraint on ReceivedDate before dropping the column
DECLARE @dfName NVARCHAR(200);
SELECT @dfName = dc.name
FROM sys.default_constraints dc
INNER JOIN sys.columns c ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID('dbo.RefillTransaction')
  AND c.name = 'ReceivedDate';

IF @dfName IS NOT NULL
BEGIN
    EXEC ('ALTER TABLE dbo.RefillTransaction DROP CONSTRAINT [' + @dfName + ']');
END

-- 4. Drop ReceivedQty column
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RefillTransaction') AND name = 'ReceivedQty'
)
BEGIN
    ALTER TABLE dbo.RefillTransaction DROP COLUMN [ReceivedQty];
END

-- 5. Drop ReceivedDate column
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.RefillTransaction') AND name = 'ReceivedDate'
)
BEGIN
    ALTER TABLE dbo.RefillTransaction DROP COLUMN [ReceivedDate];
END

-- 6. Update the index that INCLUDEs ReceivedQty
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_RefillTransaction_BatchId' AND object_id = OBJECT_ID('dbo.RefillTransaction'))
BEGIN
    DROP INDEX [IX_RefillTransaction_BatchId] ON dbo.RefillTransaction;
END

CREATE NONCLUSTERED INDEX [IX_RefillTransaction_BatchId]
    ON dbo.RefillTransaction([BatchId] ASC)
    INCLUDE([Status], [SentQty]);
