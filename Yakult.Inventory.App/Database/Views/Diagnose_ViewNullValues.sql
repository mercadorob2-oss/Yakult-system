-- Diagnostic Script for vw_InvoiceItems NULL Values Issue
-- This script will help identify why the view returns NULL values

PRINT '============================================'
PRINT 'DIAGNOSTIC REPORT FOR vw_InvoiceItems ISSUES'
PRINT '============================================'
PRINT ''

-- 1. Check if Sets table has data
PRINT '1. Checking Sets table...'
SELECT 
    'Sets Table' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT SetType) as UniqueSetTypes
FROM dbo.Sets

-- Show SetType distribution
PRINT '   SetType Distribution:'
SELECT SetType, COUNT(*) as RecordCount
FROM dbo.Sets
GROUP BY SetType
ORDER BY SetType

-- 2. Check if SetItems table has data and proper foreign keys
PRINT ''
PRINT '2. Checking SetItems table...'
SELECT 
    'SetItems Table' as TableName,
    COUNT(*) as TotalRecords,
    COUNT(DISTINCT SetId) as UniqueSets,
    COUNT(ItemId) as RecordsWithItemId
FROM dbo.SetItems

-- Check for orphaned SetItems (SetId not in Sets table)
PRINT '   Checking for orphaned SetItems...'
SELECT COUNT(*) as OrphanedSetItems
FROM dbo.SetItems si
WHERE NOT EXISTS (SELECT 1 FROM dbo.Sets s WHERE s.SetId = si.SetId)

-- 3. Check Companies table and relationships
PRINT ''
PRINT '3. Checking Companies table...'
SELECT 
    'Companies Table' as TableName,
    COUNT(*) as TotalRecords
FROM dbo.Companies

-- Check Sets with missing ComId
PRINT '   Checking Sets with missing/invalid ComId...'
SELECT COUNT(*) as SetsWithInvalidComId
FROM dbo.Sets s
WHERE s.ComId IS NOT NULL 
  AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.ComId = s.ComId)

-- 4. Check Sites table and relationships
PRINT ''
PRINT '4. Checking Sites table...'
SELECT 
    'Sites Table' as TableName,
    COUNT(*) as TotalRecords
FROM dbo.Sites

-- Check Sets with missing SiteId
PRINT '   Checking Sets with missing/invalid SiteId...'
SELECT COUNT(*) as SetsWithInvalidSiteId
FROM dbo.Sets s
WHERE s.SiteId IS NOT NULL 
  AND NOT EXISTS (SELECT 1 FROM dbo.Sites st WHERE st.SiteId = s.SiteId)

-- 5. Check Vendors table and relationships
PRINT ''
PRINT '5. Checking Vendors table...'
SELECT 
    'Vendors Table' as TableName,
    COUNT(*) as TotalRecords
FROM dbo.Vendors

-- Check Sets with missing VendorId
PRINT '   Checking Sets with missing/invalid VendorId...'
SELECT COUNT(*) as SetsWithInvalidVendorId
FROM dbo.Sets s
WHERE s.VendorId IS NOT NULL 
  AND NOT EXISTS (SELECT 1 FROM dbo.Vendors v WHERE v.VendorId = s.VendorId)

-- 6. Show sample data from Sets table with Invoice-like SetTypes
PRINT ''
PRINT '6. Sample Invoice-type Sets data:'
SELECT TOP 5
    SetId,
    SetCode,
    SetType,
    DocumentNumber,
    ComId,
    SiteId,
    VendorId,
    Status
FROM dbo.Sets
WHERE SetType IN ('Invoice', 'Software/License', 'Service')
ORDER BY SetId DESC

-- 7. Test simple JOIN to identify the problematic relationship
PRINT ''
PRINT '7. Testing individual JOINs to find the problem:'

-- Test Sets to SetItems
PRINT '   Sets LEFT JOIN SetItems:'
SELECT TOP 5
    s.SetId,
    s.SetCode,
    si.ItemId,
    CASE WHEN si.SetId IS NULL THEN 'NO ITEMS' ELSE 'HAS ITEMS' END as ItemStatus
FROM dbo.Sets s
LEFT JOIN dbo.SetItems si ON s.SetId = si.SetId
WHERE s.SetType IN ('Invoice', 'Software/License', 'Service')

-- Test Sets to Companies
PRINT '   Sets LEFT JOIN Companies:'
SELECT TOP 5
    s.SetId,
    s.SetCode,
    s.ComId,
    c.CompanyName,
    CASE WHEN c.ComId IS NULL THEN 'NO COMPANY' ELSE 'HAS COMPANY' END as CompanyStatus
FROM dbo.Sets s
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId
WHERE s.SetType IN ('Invoice', 'Software/License', 'Service')

-- 8. Final test - Simplified view query
PRINT ''
PRINT '8. Testing simplified view query:'
SELECT TOP 5
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentDate,
    s.Status,
    c.CompanyName,
    st.SiteName,
    v.VendorName
FROM dbo.Sets s
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId
LEFT JOIN dbo.Sites st ON s.SiteId = st.SiteId
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId
WHERE s.SetType IN ('Invoice', 'Software/License', 'Service')

PRINT ''
PRINT '============================================'
PRINT 'END OF DIAGNOSTIC REPORT'
PRINT '============================================'