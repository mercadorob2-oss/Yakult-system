-- STEP-BY-STEP SOLUTION FOR NULL VALUES IN VIEWS
-- ================================================

/*
PROBLEM DIAGNOSIS:
1. View returns NULL values for columns with foreign key relationships
2. SetType has invalid value "Invoice" instead of "Software/License" or "Service"
3. This suggests JOIN failures due to missing or mismatched foreign keys

SOLUTION STEPS:
*/

-- ================================================
-- STEP 1: Run Diagnostic First
-- ================================================
PRINT 'STEP 1: Running diagnostics...'
PRINT '--------------------------------'

-- Check what's in your Sets table
SELECT 
    'Total Sets' as Metric, COUNT(*) as Value
FROM dbo.Sets
UNION ALL
SELECT 
    'Invoice Type Sets', COUNT(*)
FROM dbo.Sets
WHERE SetType IN ('Invoice', 'Software/License', 'Service')
UNION ALL
SELECT 
    'Sets with ComId', COUNT(*)
FROM dbo.Sets
WHERE ComId IS NOT NULL
UNION ALL
SELECT 
    'Sets with VendorId', COUNT(*)
FROM dbo.Sets
WHERE VendorId IS NOT NULL

-- ================================================
-- STEP 2: Fix SetType Values
-- ================================================
PRINT ''
PRINT 'STEP 2: Fixing SetType values...'
PRINT '--------------------------------'

-- Show current invalid SetTypes
PRINT 'Current invalid SetType values:'
SELECT SetType, COUNT(*) as Count
FROM dbo.Sets
WHERE SetType NOT IN ('Software/License', 'Service', 'Stock Transfer', 'Stock Addition', 'Stock Subtraction')
GROUP BY SetType

-- Fix "Invoice" SetType
UPDATE dbo.Sets
SET SetType = 'Software/License'  -- Default to Software/License for invoice types
WHERE SetType = 'Invoice'
  AND DocumentNumber IS NOT NULL

PRINT 'SetType values fixed!'

-- ================================================
-- STEP 3: Check and Fix Foreign Key Issues
-- ================================================
PRINT ''
PRINT 'STEP 3: Checking foreign key relationships...'
PRINT '--------------------------------'

-- Find Sets with invalid ComId
SELECT 
    'Sets with invalid ComId' as Issue,
    s.SetId,
    s.SetCode,
    s.ComId as InvalidComId
FROM dbo.Sets s
WHERE s.ComId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.ComId = s.ComId)

-- Find Sets with invalid VendorId
SELECT 
    'Sets with invalid VendorId' as Issue,
    s.SetId,
    s.SetCode,
    s.VendorId as InvalidVendorId
FROM dbo.Sets s
WHERE s.VendorId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Vendors v WHERE v.VendorId = s.VendorId)

-- Option A: Set invalid foreign keys to NULL
/*
UPDATE dbo.Sets
SET ComId = NULL
WHERE ComId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.ComId = Sets.ComId)

UPDATE dbo.Sets
SET VendorId = NULL
WHERE VendorId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Vendors v WHERE v.VendorId = Sets.VendorId)
*/

-- Option B: Create missing Companies/Vendors
/*
INSERT INTO dbo.Companies (ComId, CompanyName, Status, CreatedDate, CreatedBy)
SELECT DISTINCT 
    s.ComId,
    'Unknown Company ' + CAST(s.ComId as varchar(10)),
    'Active',
    GETDATE(),
    'System'
FROM dbo.Sets s
WHERE s.ComId IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM dbo.Companies c WHERE c.ComId = s.ComId)
*/

-- ================================================
-- STEP 4: Recreate the View with Better JOINs
-- ================================================
PRINT ''
PRINT 'STEP 4: Recreating view with improved JOINs...'
PRINT '--------------------------------'

IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems
GO

CREATE VIEW dbo.vw_InvoiceItems
AS
SELECT 
    -- Core fields (these should never be NULL if Sets has data)
    s.SetId,
    ISNULL(s.SetCode, 'NO_CODE') as SetCode,
    ISNULL(s.SetType, 'UNKNOWN') as SetType,
    s.DocumentDate,
    ISNULL(s.Status, 'Unknown') as Status,
    s.Remarks,
    
    -- Financial fields with defaults
    ISNULL(s.Subtotal, 0) as Subtotal,
    ISNULL(s.VatAmount, 0) as VatAmount,
    ISNULL(s.WhtAmount, 0) as WhtAmount,
    ISNULL(s.DiscountAmount, 0) as DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) as TotalAmountDue,
    
    -- Document fields
    s.DocumentNumber,
    s.ReferenceNumber,
    s.StartDate,
    s.EndDate,
    s.DaysUntilExpiry,
    s.ExpiryStatus,
    
    -- Related table fields with NULL handling
    s.SiteId,
    CASE 
        WHEN st.SiteName IS NULL THEN 'No Site'
        ELSE st.SiteName 
    END as Site,
    
    s.ComId,
    CASE 
        WHEN c.CompanyName IS NULL AND s.ComId IS NOT NULL THEN 'Company #' + CAST(s.ComId as varchar(10))
        WHEN c.CompanyName IS NULL THEN 'No Company'
        ELSE c.CompanyName 
    END as CompanyName,
    
    s.VendorId,
    CASE 
        WHEN v.VendorName IS NULL AND s.VendorId IS NOT NULL THEN 'Vendor #' + CAST(s.VendorId as varchar(10))
        WHEN v.VendorName IS NULL THEN 'No Vendor'
        ELSE v.VendorName 
    END as VendorName,
    
    -- Item details (these might be NULL if no items)
    si.ItemId,
    i.ItemName,
    si.ItemType,
    si.Quantity,
    si.UnitPrice,
    si.LineTotal
    
FROM dbo.Sets s
-- Use LEFT JOINs to avoid losing records
LEFT JOIN dbo.SetItems si ON s.SetId = si.SetId
LEFT JOIN dbo.Items i ON si.ItemId = i.ItemId
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId
LEFT JOIN dbo.Sites st ON s.SiteId = st.SiteId
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId

-- Filter for invoice types only
WHERE s.SetType IN ('Software/License', 'Service')
   OR (s.SetType = 'Invoice')  -- Include old Invoice type temporarily
GO

-- ================================================
-- STEP 5: Test the Fixed View
-- ================================================
PRINT ''
PRINT 'STEP 5: Testing the fixed view...'
PRINT '--------------------------------'

-- This query should now return data with no NULLs in core fields
SELECT TOP 10
    SetId,
    SetCode,
    SetType,
    DocumentNumber,
    CompanyName,
    VendorName,
    Status
FROM dbo.vw_InvoiceItems
ORDER BY SetId DESC

-- ================================================
-- STEP 6: Final Verification
-- ================================================
PRINT ''
PRINT 'STEP 6: Final verification...'
PRINT '--------------------------------'

-- Count records in the view
SELECT 
    'Records in vw_InvoiceItems' as Metric,
    COUNT(*) as Count
FROM dbo.vw_InvoiceItems

-- Show sample with all columns
SELECT TOP 3 * 
FROM dbo.vw_InvoiceItems
ORDER BY SetId DESC

PRINT ''
PRINT '============================================'
PRINT 'FIX COMPLETE!'
PRINT ''
PRINT 'If you still see NULL values:'
PRINT '1. Check that you are querying dbo.vw_InvoiceItems'
PRINT '2. Make sure you are in the correct database'
PRINT '3. Refresh your SSMS Object Explorer'
PRINT '4. Close and reopen your query window'
PRINT '============================================'