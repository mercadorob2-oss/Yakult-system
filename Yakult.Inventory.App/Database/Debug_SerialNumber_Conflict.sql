-- Debug script to find why "AZCD1" is causing a duplicate error

PRINT '================================================='
PRINT 'Investigating Serial Number Conflict: AZCD1'
PRINT '================================================='
PRINT ''

-- Check 1: Does AZCD1 exist in active items?
PRINT 'CHECK 1: Looking for AZCD1 in ACTIVE items...'
SELECT
    ItemId,
    Name,
    SerialNumber,
    Category,
    ItemType,
    DateCreated,
    'ACTIVE' AS Status
FROM dbo.Item
WHERE SerialNumber = 'AZCD1'
  AND NOT EXISTS (
      SELECT 1 FROM dbo.ArchiveStatus arch
      WHERE arch.EntityType = 'Item' AND arch.EntityId = Item.ItemId
  )

IF @@ROWCOUNT = 0
    PRINT 'Result: AZCD1 NOT found in active items'
ELSE
    PRINT 'Result: AZCD1 FOUND in active items (this is the conflict!)'

PRINT ''
PRINT '================================================='

-- Check 2: Does AZCD1 exist in archived items?
PRINT 'CHECK 2: Looking for AZCD1 in ARCHIVED items...'
SELECT
    i.ItemId,
    i.Name,
    i.SerialNumber,
    i.Category,
    i.ItemType,
    i.DateCreated,
    arch.ArchivedAt,
    'ARCHIVED' AS Status
FROM dbo.Item i
INNER JOIN dbo.ArchiveStatus arch
    ON arch.EntityType = 'Item'
    AND arch.EntityId = i.ItemId
WHERE i.SerialNumber = 'AZCD1'

IF @@ROWCOUNT = 0
    PRINT 'Result: AZCD1 NOT found in archived items'
ELSE
    PRINT 'Result: AZCD1 FOUND in archived items'

PRINT ''
PRINT '================================================='

-- Check 3: Does AZCD1 exist ANYWHERE in the Item table?
PRINT 'CHECK 3: Looking for AZCD1 ANYWHERE in Item table...'
SELECT
    i.ItemId,
    i.Name,
    i.SerialNumber,
    i.Category,
    i.ItemType,
    i.DateCreated,
    CASE
        WHEN arch.EntityId IS NOT NULL THEN 'ARCHIVED'
        ELSE 'ACTIVE'
    END AS Status
FROM dbo.Item i
LEFT JOIN dbo.ArchiveStatus arch
    ON arch.EntityType = 'Item'
    AND arch.EntityId = i.ItemId
WHERE i.SerialNumber = 'AZCD1'

IF @@ROWCOUNT = 0
    PRINT 'Result: AZCD1 NOT found anywhere in Item table'
ELSE
    PRINT 'Result: AZCD1 EXISTS in Item table (see above)'

PRINT ''
PRINT '================================================='

-- Check 4: Check the unique index
PRINT 'CHECK 4: Checking unique index on Item.SerialNumber...'
SELECT
    i.name AS IndexName,
    c.name AS ColumnName,
    i.is_unique,
    i.type_desc,
    i.filter_definition
FROM sys.indexes i
INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
WHERE i.object_id = OBJECT_ID('dbo.Item')
  AND c.name = 'SerialNumber'

PRINT ''
PRINT '================================================='
PRINT 'DIAGNOSIS:'
PRINT '================================================='
PRINT 'If AZCD1 exists in ACTIVE items: Delete or modify it before adding new one'
PRINT 'If AZCD1 exists in ARCHIVED items: The unique constraint includes archived items (by design)'
PRINT 'If AZCD1 does NOT exist anywhere: There may be a different issue (check spelling/whitespace)'
PRINT ''
PRINT 'SOLUTION: Search for the existing AZCD1 item and either:'
PRINT '1. Use a different serial number for the new item'
PRINT '2. Delete the existing item if it was added by mistake'
PRINT '3. If archived, this is expected behavior - use different serial number'
