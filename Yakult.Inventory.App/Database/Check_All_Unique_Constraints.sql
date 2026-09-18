-- Check all unique constraints and indexes on the Item table

PRINT '================================================='
PRINT 'All Unique Constraints on Item Table'
PRINT '================================================='
PRINT ''

-- Get all unique indexes
SELECT
    i.name AS IndexName,
    STRING_AGG(c.name, ', ') AS Columns,
    i.is_unique,
    i.type_desc,
    i.filter_definition
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('dbo.Item')
  AND i.is_unique = 1
GROUP BY i.name, i.is_unique, i.type_desc, i.filter_definition
ORDER BY i.name

PRINT ''
PRINT '================================================='
PRINT 'Checking if AZCD1 violates any constraint'
PRINT '================================================='
PRINT ''

-- Try to insert AZCD1 to see which constraint fails
BEGIN TRANSACTION

BEGIN TRY
    INSERT INTO dbo.Item
        (Name, Description, ModelNumber, Active, CategoryId, Category,
         SerialNumber, UnitOfMeasure, StockOnHand, DateCreated, CreatedBy, DateModified, ModifiedBy,
         ItemType, StartDate, EndDate, Amount, ConditionID, VendorId, Remarks, WarrantyYears, DatePurchased, LicenseNumber,
         DurationYears, DurationStartDate, DurationEndDate, WarrantyStartDate)
    VALUES
        ('Test Item', 'Test Description', 'TEST001', 1, 1, 'Test Category',
         'AZCD1', 'pcs', 1, GETDATE(), 1, GETDATE(), 1,
         'Hardware', GETDATE(), NULL, 0, 1, NULL, NULL, 0, NULL, NULL,
         0, NULL, NULL, NULL)

    PRINT 'SUCCESS: AZCD1 can be inserted (no constraint violation)'
    ROLLBACK TRANSACTION
END TRY
BEGIN CATCH
    PRINT 'FAILED: AZCD1 cannot be inserted'
    PRINT 'Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR)
    PRINT 'Error Message: ' + ERROR_MESSAGE()
    PRINT 'Error Line: ' + CAST(ERROR_LINE() AS VARCHAR)
    ROLLBACK TRANSACTION
END CATCH
