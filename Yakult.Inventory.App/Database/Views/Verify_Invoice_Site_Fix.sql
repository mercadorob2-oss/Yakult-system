-- ========================================================================
-- VERIFICATION SCRIPT: Invoice Report Site Column Fix
-- Run this AFTER applying Fix_vw_InvoiceItems_Site_Column.sql
-- ========================================================================

USE Yakult_Inventory_System;
GO

PRINT '╔════════════════════════════════════════════════════════════════════════╗';
PRINT '║            INVOICE REPORT SITE COLUMN - VERIFICATION TESTS             ║';
PRINT '╚════════════════════════════════════════════════════════════════════════╝';
PRINT '';

-- Test 1: Check if view exists
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 1: Checking if vw_InvoiceItems view exists...';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';

IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    PRINT '✓ PASS: View exists';
ELSE
    PRINT '✗ FAIL: View does not exist - run the fix script first!';

PRINT '';

-- Test 2: Check if Site column is present
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 2: Checking if Site column exists in view...';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';

IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.VIEW_COLUMN_USAGE 
    WHERE VIEW_NAME = 'vw_InvoiceItems' 
    AND COLUMN_NAME = 'Site'
)
    PRINT '✓ PASS: Site column exists in view';
ELSE
    PRINT '✗ FAIL: Site column not found in view';

PRINT '';

-- Test 3: Check for data in the view
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 3: Checking if view returns data...';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';

DECLARE @RecordCount INT;
SELECT @RecordCount = COUNT(*) FROM vw_InvoiceItems;

IF @RecordCount > 0
    PRINT '✓ PASS: View returns ' + CAST(@RecordCount AS VARCHAR(10)) + ' record(s)';
ELSE
    PRINT '⚠ WARNING: View returns 0 records - might be expected if no invoices exist';

PRINT '';

-- Test 4: Sample data from view
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 4: Sample data from view (first 5 records)';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '';

SELECT TOP 5
    SetId,
    DocumentNumber,
    ReferenceNumber,
    ItemName,
    Site,
    Year,
    Month
FROM vw_InvoiceItems
ORDER BY Year DESC, Month DESC, DocumentNumber;

PRINT '';

-- Test 5: Check for items with different Sites in same invoice
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 5: Checking for invoices with items in different Sites...';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '';

SELECT 
    DocumentNumber,
    COUNT(DISTINCT Site) AS UniqueSiteCount,
    COUNT(*) AS TotalItems,
    STRING_AGG(DISTINCT Site, ', ') AS Sites
FROM vw_InvoiceItems
GROUP BY DocumentNumber
HAVING COUNT(DISTINCT Site) > 1
ORDER BY DocumentNumber;

DECLARE @MultiSiteInvoices INT;
SELECT @MultiSiteInvoices = COUNT(DISTINCT DocumentNumber)
FROM vw_InvoiceItems
GROUP BY DocumentNumber
HAVING COUNT(DISTINCT Site) > 1;

IF @MultiSiteInvoices > 0
    PRINT '✓ PASS: Found ' + CAST(@MultiSiteInvoices AS VARCHAR(10)) + ' invoice(s) with items in multiple Sites';
ELSE
    PRINT '⚠ INFO: No invoices found with items in multiple Sites (might be expected)';

PRINT '';

-- Test 6: Check for NULL or N/A Sites
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 6: Checking for items with missing Site values...';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';

DECLARE @MissingSiteCount INT;
SELECT @MissingSiteCount = COUNT(*)
FROM vw_InvoiceItems
WHERE Site IS NULL OR Site = 'N/A';

IF @MissingSiteCount = 0
    PRINT '✓ PASS: All items have valid Site values';
ELSE
BEGIN
    PRINT '⚠ WARNING: Found ' + CAST(@MissingSiteCount AS VARCHAR(10)) + ' item(s) with missing Site';
    PRINT '';
    PRINT 'Items with missing Site:';
    SELECT TOP 5
        DocumentNumber,
        ItemName,
        Site,
        SetItemId
    FROM vw_InvoiceItems
    WHERE Site IS NULL OR Site = 'N/A'
    ORDER BY DocumentNumber;
END

PRINT '';

-- Test 7: Detailed view of one invoice (if any exist)
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT 'TEST 7: Detailed view of most recent invoice';
PRINT '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
PRINT '';

DECLARE @LatestDoc NVARCHAR(255);
SELECT TOP 1 @LatestDoc = DocumentNumber 
FROM vw_InvoiceItems 
ORDER BY Year DESC, Month DESC;

IF @LatestDoc IS NOT NULL
BEGIN
    PRINT 'Invoice: ' + @LatestDoc;
    PRINT '';
    SELECT 
        SetItemId,
        ItemName,
        Site,
        Quantity,
        StartDate,
        EndDate
    FROM vw_InvoiceItems
    WHERE DocumentNumber = @LatestDoc
    ORDER BY SetItemId;
    
    PRINT '';
    PRINT '✓ Check above: Each item should show its own Site value';
END
ELSE
    PRINT '⚠ INFO: No invoices found in the system';

PRINT '';
PRINT '╔════════════════════════════════════════════════════════════════════════╗';
PRINT '║                         VERIFICATION COMPLETE                           ║';
PRINT '╚════════════════════════════════════════════════════════════════════════╝';
PRINT '';
PRINT 'NEXT STEPS:';
PRINT '1. If all tests pass, test in the application:';
PRINT '   - Open Yakult Inventory app';
PRINT '   - Go to "View Master Data" → "Invoice Reports"';
PRINT '   - Click "Generate Report"';
PRINT '   - Verify each item shows its correct Site';
PRINT '';
PRINT '2. If any tests fail, review the fix script and reapply if needed.';
PRINT '';
GO
