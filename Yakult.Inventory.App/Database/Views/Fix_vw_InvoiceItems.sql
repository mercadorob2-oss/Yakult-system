-- Drop and recreate vw_InvoiceItems view to fix NULL values and SetType issues
-- This view shows invoice items with proper JOINs to related tables

IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems
GO

CREATE VIEW dbo.vw_InvoiceItems
AS
SELECT 
    -- Set/Invoice Information
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentDate,
    s.Status,
    s.Remarks,
    
    -- Financial Information
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    
    -- Invoice Specific Information
    s.DocumentNumber,
    s.ReferenceNumber,
    s.StartDate,
    s.EndDate,
    s.DaysUntilExpiry,
    s.ExpiryStatus,
    
    -- Location Information
    st.SiteName AS Site,
    
    -- Company Information
    s.ComId,
    c.CompanyName,
    
    -- Item Details from SetItems
    si.ItemId,
    i.ItemName,
    i.ItemDescription,
    si.ItemType,
    si.ModelNumber,
    si.SerialNumber,
    si.Quantity,
    si.UnitPrice,
    si.LineTotal,
    
    -- Category Information
    cat.CategoryName,
    
    -- Vendor Information
    s.VendorId,
    v.VendorName,
    v.VendorAddress,
    
    -- Condition tracking
    si.ConditionId,
    cond.ConditionName,
    si.ConditionChangeReason
    
FROM dbo.Sets s
-- Use INNER JOIN for required relationships, LEFT JOIN for optional
INNER JOIN dbo.SetItems si ON s.SetId = si.SetId
LEFT JOIN dbo.Items i ON si.ItemId = i.ItemId
LEFT JOIN dbo.Categories cat ON si.CategoryName = cat.CategoryName
LEFT JOIN dbo.Companies c ON s.ComId = c.ComId
LEFT JOIN dbo.Sites st ON s.SiteId = st.SiteId
LEFT JOIN dbo.Vendors v ON s.VendorId = v.VendorId
LEFT JOIN dbo.Conditions cond ON si.ConditionId = cond.ConditionId

-- Filter for Invoice types only (Software/License or Service)
WHERE s.SetType IN ('Software/License', 'Service')
  AND s.Status = 'Active'  -- Only show active invoices

GO

-- Add helpful comments
EXEC sp_addextendedproperty 
    @name = N'MS_Description', 
    @value = N'View showing invoice items with proper SetType filtering for Software/License and Service only', 
    @level0type = N'SCHEMA', @level0name = N'dbo',
    @level1type = N'VIEW', @level1name = N'vw_InvoiceItems'
GO