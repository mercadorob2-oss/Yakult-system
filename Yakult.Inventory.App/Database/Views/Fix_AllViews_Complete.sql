-- Complete Fix for All Invoice and Set-Related Views
-- This script recreates all views with proper JOINs and data type handling

-- =====================================================
-- 1. FIX BASE VIEW: vw_Sets (Foundation view)
-- =====================================================
IF OBJECT_ID('dbo.vw_Sets', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Sets
GO

CREATE VIEW dbo.vw_Sets
AS
SELECT 
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentDate,
    s.Status,
    s.Remarks,
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.StartDate,
    s.EndDate,
    s.DaysUntilExpiry,
    s.ExpiryStatus,
    s.SiteId,
    ISNULL(st.SiteName, 'N/A') AS SiteName,
    s.ComId,
    ISNULL(c.CompanyName, 'N/A') AS CompanyName,
    s.VendorId,
    ISNULL(v.VendorName, 'N/A') AS VendorName,
    s.CreatedBy,
    s.CreatedDate,
    s.ModifiedBy,
    s.ModifiedDate
FROM dbo.Sets s
LEFT JOIN dbo.Sites st ON s.SiteId = st.SiteId AND st.Status = 'Active'
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId AND c.Status = 'Active'
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId AND v.Status = 'Active'
WHERE s.Status = 'Active'
GO

-- =====================================================
-- 2. FIX VIEW: vw_InvoiceItems (Invoice specific)
-- =====================================================
IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems
GO

CREATE VIEW dbo.vw_InvoiceItems
AS
SELECT 
    -- Set/Invoice Core Information
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentDate,
    s.Status,
    s.Remarks,
    
    -- Financial Information
    ISNULL(s.Subtotal, 0) as Subtotal,
    ISNULL(s.VatAmount, 0) as VatAmount,
    ISNULL(s.WhtAmount, 0) as WhtAmount,
    ISNULL(s.DiscountAmount, 0) as DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) as TotalAmountDue,
    
    -- Invoice Document Information
    ISNULL(s.DocumentNumber, '') as DocumentNumber,
    ISNULL(s.ReferenceNumber, '') as ReferenceNumber,
    s.StartDate,
    s.EndDate,
    ISNULL(s.DaysUntilExpiry, 0) as DaysUntilExpiry,
    ISNULL(s.ExpiryStatus, 'N/A') as ExpiryStatus,
    
    -- Site/Location Information
    s.SiteId,
    ISNULL(st.SiteName, 'N/A') AS Site,
    
    -- Company Information
    s.ComId,
    ISNULL(c.CompanyName, 'N/A') AS CompanyName,
    
    -- Vendor Information
    s.VendorId,
    ISNULL(v.VendorName, 'N/A') AS VendorName,
    ISNULL(v.VendorAddress, 'N/A') AS VendorAddress,
    
    -- Item Details (if joining with SetItems)
    si.SetItemId,
    si.ItemId,
    ISNULL(i.ItemName, 'N/A') AS ItemName,
    ISNULL(i.ItemDescription, '') AS ItemDescription,
    si.ItemType,
    si.ModelNumber,
    si.SerialNumber,
    ISNULL(si.Quantity, 0) as Quantity,
    ISNULL(si.UnitPrice, 0) as UnitPrice,
    ISNULL(si.LineTotal, 0) as LineTotal,
    
    -- Category Information
    si.CategoryName,
    
    -- Condition tracking
    si.ConditionId,
    ISNULL(cond.ConditionName, 'N/A') as ConditionName,
    si.ConditionChangeReason,
    
    -- Audit fields
    s.CreatedBy,
    s.CreatedDate,
    s.ModifiedBy,
    s.ModifiedDate
    
FROM dbo.Sets s
LEFT JOIN dbo.SetItems si ON s.SetId = si.SetId
LEFT JOIN dbo.Items i ON si.ItemId = i.ItemId
LEFT JOIN dbo.Categories cat ON si.CategoryName = cat.CategoryName AND cat.Status = 'Active'
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId AND c.Status = 'Active'
LEFT JOIN dbo.Sites st ON s.SiteId = st.SiteId AND st.Status = 'Active'
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId AND v.Status = 'Active'
LEFT JOIN dbo.Conditions cond ON si.ConditionId = cond.ConditionId

-- Filter for proper Invoice SetTypes only
WHERE s.SetType IN ('Software/License', 'Service')
  AND s.Status = 'Active'
GO

-- =====================================================
-- 3. FIX VIEW: vw_InvoiceSummary (Invoice aggregates)
-- =====================================================
IF OBJECT_ID('dbo.vw_InvoiceSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceSummary
GO

CREATE VIEW dbo.vw_InvoiceSummary
AS
SELECT 
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber,
    s.DocumentDate,
    ISNULL(c.CompanyName, 'N/A') AS CompanyName,
    ISNULL(v.VendorName, 'N/A') AS VendorName,
    COUNT(DISTINCT si.ItemId) as ItemCount,
    SUM(ISNULL(si.Quantity, 0)) as TotalQuantity,
    ISNULL(s.Subtotal, 0) as Subtotal,
    ISNULL(s.VatAmount, 0) as VatAmount,
    ISNULL(s.WhtAmount, 0) as WhtAmount,
    ISNULL(s.DiscountAmount, 0) as DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) as TotalAmountDue,
    s.Status,
    s.ExpiryStatus,
    s.DaysUntilExpiry
FROM dbo.Sets s
LEFT JOIN dbo.SetItems si ON s.SetId = si.SetId
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId AND c.Status = 'Active'
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId AND v.Status = 'Active'
WHERE s.SetType IN ('Software/License', 'Service')
  AND s.Status = 'Active'
GROUP BY 
    s.SetId, s.SetCode, s.SetType, s.DocumentNumber, s.DocumentDate,
    c.CompanyName, v.VendorName, s.Subtotal, s.VatAmount, s.WhtAmount,
    s.DiscountAmount, s.TotalAmountDue, s.Status, s.ExpiryStatus, s.DaysUntilExpiry
GO

-- =====================================================
-- 4. Create Troubleshooting Query
-- =====================================================
PRINT 'Creating troubleshooting query to verify views...'
GO

-- Test the views
PRINT 'Testing vw_Sets:'
SELECT TOP 5 * FROM dbo.vw_Sets ORDER BY SetId DESC

PRINT ''
PRINT 'Testing vw_InvoiceItems:'
SELECT TOP 5 
    SetId, SetCode, SetType, CompanyName, VendorName, ItemName, Quantity, UnitPrice 
FROM dbo.vw_InvoiceItems 
ORDER BY SetId DESC

PRINT ''
PRINT 'Testing vw_InvoiceSummary:'
SELECT TOP 5 * FROM dbo.vw_InvoiceSummary ORDER BY SetId DESC

-- =====================================================
-- 5. Data Cleanup - Fix invalid SetType values
-- =====================================================
PRINT ''
PRINT 'Fixing invalid SetType values...'

-- Update "Invoice" to proper SetType
UPDATE dbo.Sets
SET SetType = CASE 
    WHEN DocumentNumber IS NOT NULL AND DocumentNumber LIKE 'INV%' THEN 'Software/License'
    WHEN DocumentNumber IS NOT NULL THEN 'Service'
    ELSE SetType
END
WHERE SetType = 'Invoice'

-- Show final SetType distribution
PRINT ''
PRINT 'Final SetType distribution:'
SELECT SetType, COUNT(*) as Count
FROM dbo.Sets
GROUP BY SetType
ORDER BY SetType

PRINT ''
PRINT '============================================'
PRINT 'All views have been recreated successfully!'
PRINT 'Please refresh your query window and test again.'
PRINT '============================================'