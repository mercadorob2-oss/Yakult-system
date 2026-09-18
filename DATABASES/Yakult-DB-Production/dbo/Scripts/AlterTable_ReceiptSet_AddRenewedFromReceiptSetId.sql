IF COL_LENGTH('dbo.ReceiptSet', 'RenewedFromReceiptSetId') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptSet
        ADD RenewedFromReceiptSetId INT NULL;

END
GO

-- NOTE: We do NOT use cascading actions here (SET NULL / CASCADE) because SQL Server can block it
-- when other cascading FKs exist (e.g., ReceiptSetLink ON DELETE CASCADE).

IF NOT EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = 'FK_ReceiptSet_RenewedFromReceiptSetId'
      AND parent_object_id = OBJECT_ID('dbo.ReceiptSet')
)
BEGIN
    ALTER TABLE dbo.ReceiptSet WITH CHECK
        ADD CONSTRAINT FK_ReceiptSet_RenewedFromReceiptSetId
            FOREIGN KEY (RenewedFromReceiptSetId)
            REFERENCES dbo.ReceiptSet (ReceiptSetId);
END
GO

-- Index is optional (performance only). Skip if either INDEX or STATS already uses the name.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ReceiptSet_RenewedFromReceiptSetId'
      AND object_id = OBJECT_ID('dbo.ReceiptSet')
)
AND NOT EXISTS (
    SELECT 1
    FROM sys.stats
    WHERE name = 'IX_ReceiptSet_RenewedFromReceiptSetId'
      AND object_id = OBJECT_ID('dbo.ReceiptSet')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_ReceiptSet_RenewedFromReceiptSetId
        ON dbo.ReceiptSet (RenewedFromReceiptSetId);
END
GO
