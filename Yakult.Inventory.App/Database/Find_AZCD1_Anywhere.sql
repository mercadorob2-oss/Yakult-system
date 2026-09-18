-- Find AZCD1 in ALL possible locations in the database

PRINT '================================================='
PRINT 'Searching for AZCD1 in EVERY table'
PRINT '================================================='
PRINT ''

-- 1. Check Item table (including all statuses)
PRINT '1. Checking Item table (all records)...'
SELECT
    ItemId,
    Name,
    SerialNumber,
    Category,
    ItemType,
    Active,
    DateCreated,
    CreatedBy
FROM dbo.Item
WHERE SerialNumber = 'AZCD1' OR SerialNumber LIKE '%AZCD1%'

IF @@ROWCOUNT = 0
    PRINT '   No AZCD1 found in Item table'
PRINT ''

-- 2. Check Item table with case-insensitive and trimmed search
PRINT '2. Checking Item table (case-insensitive, trimmed)...'
SELECT
    ItemId,
    Name,
    SerialNumber,
    DATALENGTH(SerialNumber) AS SerialNumberLength,
    Category,
    ItemType,
    Active
FROM dbo.Item
WHERE LTRIM(RTRIM(UPPER(SerialNumber))) = 'AZCD1'

IF @@ROWCOUNT = 0
    PRINT '   No AZCD1 found (case-insensitive)'
PRINT ''

-- 3. Check for items with SerialNumber starting with AZCD
PRINT '3. Checking for similar serial numbers (AZCD*)...'
SELECT
    ItemId,
    Name,
    SerialNumber,
    Category,
    ItemType,
    Active
FROM dbo.Item
WHERE SerialNumber LIKE 'AZCD%'
ORDER BY SerialNumber

IF @@ROWCOUNT = 0
    PRINT '   No similar serial numbers found'
PRINT ''

-- 4. Try to actually insert AZCD1 with minimal values
PRINT '4. Attempting to INSERT AZCD1 with minimal required fields...'
BEGIN TRANSACTION

BEGIN TRY
    DECLARE @TestItemId INT

    INSERT INTO dbo.Item
        (Name, SerialNumber, Active, UnitOfMeasure, StockOnHand,
         DateCreated, CreatedBy, DateModified, ModifiedBy, Amount, ItemType)
    VALUES
        ('TEST AZCD1', 'AZCD1', 1, 'pcs', 1,
         GETDATE(), 1, GETDATE(), 1, 0, 'Hardware')

    SET @TestItemId = SCOPE_IDENTITY()

    PRINT '   SUCCESS! AZCD1 was inserted with ItemId = ' + CAST(@TestItemId AS VARCHAR)
    PRINT '   This means the serial number does NOT exist in the database.'

    -- Clean up the test insert
    DELETE FROM dbo.Item WHERE ItemId = @TestItemId
    PRINT '   Test record deleted.'

    ROLLBACK TRANSACTION
END TRY
BEGIN CATCH
    PRINT '   FAILED! Error when inserting AZCD1:'
    PRINT '   Error Number: ' + CAST(ERROR_NUMBER() AS VARCHAR)
    PRINT '   Error Message: ' + ERROR_MESSAGE()
    PRINT '   '
    PRINT '   This means AZCD1 DOES exist in the database.'
    PRINT '   Searching for it...'

    -- Find the existing record
    SELECT TOP 1
        ItemId,
        Name,
        SerialNumber,
        Category,
        ItemType,
        Active,
        DateCreated,
        CreatedBy,
        'THIS IS THE CONFLICTING RECORD' AS Note
    FROM dbo.Item
    WHERE SerialNumber = 'AZCD1'

    ROLLBACK TRANSACTION
END CATCH

PRINT ''
PRINT '================================================='
PRINT 'SUMMARY'
PRINT '================================================='
PRINT 'If you see "SUCCESS!" above, then AZCD1 does NOT exist.'
PRINT 'If you see "FAILED!" above, then AZCD1 DOES exist and the conflicting record is shown.'
