-- Quick Verification Script
-- Run this to verify what's happening with your views

-- 1. Check if the view exists and its definition
PRINT 'Checking if vw_InvoiceItems exists...'
IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    PRINT '✓ View dbo.vw_InvoiceItems EXISTS'
ELSE
    PRINT '✗ View dbo.vw_InvoiceItems DOES NOT EXIST'

-- 2. Check the actual query that's being run
-- The screenshot shows a query tab named "SQLQuery9.sql"
-- Let's create the correct query to run:

PRINT ''
PRINT 'Correct query to test vw_InvoiceItems:'
PRINT '======================================='
PRINT 'USE [Yakult_Inventory_System]'  -- Make sure you're in the right database
PRINT 'GO'
PRINT ''
PRINT 'SELECT TOP 10 * FROM dbo.vw_InvoiceItems'
PRINT ''

-- 3. Check if you might be querying a different object
PRINT 'Checking for similar named objects that might cause confusion:'

SELECT 
    SCHEMA_NAME(schema_id) AS SchemaName,
    name AS ObjectName,
    type_desc AS ObjectType
FROM sys.objects
WHERE name LIKE '%Invoice%' 
   OR name LIKE '%Items%'
ORDER BY name

-- 4. Check the Sets table directly to see if it has data
PRINT ''
PRINT 'Checking Sets table for Invoice-type records:'
SELECT TOP 5
    SetId,
    SetCode,
    SetType,
    DocumentNumber,
    ComId,
    VendorId,
    Status
FROM dbo.Sets
WHERE SetType IN ('Invoice', 'Software/License', 'Service')
ORDER BY SetId DESC

-- 5. If the above returns data, check why the view might return NULLs
PRINT ''
PRINT 'Checking for common issues:'

-- Check if there are any SetItems for Invoice-type Sets
SELECT 
    'SetItems for Invoices' as CheckType,
    COUNT(*) as RecordCount
FROM dbo.SetItems si
INNER JOIN dbo.Sets s ON si.SetId = s.SetId
WHERE s.SetType IN ('Invoice', 'Software/License', 'Service')

-- Check if Companies table has matching records
SELECT 
    'Companies with matching ComId' as CheckType,
    COUNT(DISTINCT s.ComId) as MatchingCompanies
FROM dbo.Sets s
INNER JOIN dbo.Companies c ON s.ComId = c.ComId
WHERE s.SetType IN ('Invoice', 'Software/License', 'Service')

-- 6. Run a simple test query that should definitely work
PRINT ''
PRINT 'Simple test query (this should return data if Sets table has records):'
SELECT 
    SetId,
    SetCode,
    SetType,
    Status
FROM dbo.Sets
WHERE Status = 'Active'

PRINT ''
PRINT '============================================'
PRINT 'If the simple query works but the view returns NULLs,'
PRINT 'run the Fix_AllViews_Complete.sql script to recreate the views.'
PRINT '============================================'