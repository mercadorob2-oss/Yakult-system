-- ========================================================================
-- FIX: Invoice Report Site Column Issue
-- Problem: Site values showing incorrectly when generating reports
-- Solution: Update vw_InvoiceItems to pull Site from correct source
-- ========================================================================

USE Yakult_Inventory_System;
GO

-- Drop existing view
IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems;
GO

-- Recreate view with corrected Site column logic
CREATE VIEW [dbo].[vw_InvoiceItems]
AS
/* ----------------------------------------------------------
   Invoice Items View - Pulls Site from SetItem.BranchId
   Each item in an invoice shows its own Site location
   ---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DispatchDate AS DocumentDate,
    s.Status,
    s.Remarks,
    DATENAME(MONTH, s.DispatchDate) AS [Month],
    YEAR(s.DispatchDate) AS [Year],
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,
    si.StartDate,
    si.EndDate,
    DATEDIFF(DAY, GETDATE(), si.EndDate) AS DaysUntilExpiry,
    CASE
        WHEN si.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), si.EndDate) <= 30 THEN 'Expiring Soon'
        ELSE 'Active'
    END AS ExpiryStatus,
    -- FIXED: Get Site from Branch table via SetItem.BranchId
    -- This ensures each item shows its correct Site location
    ISNULL(b.BranchName, 'N/A') AS Site,
    s.ComId,
    co.Name AS CompanyName,
    si.SetItemId,
    si.ItemId,
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.Name AS CategoryName,
    si.Quantity,
    si.UnitPrice,
    si.Amount AS LineTotal,
    i.VendorId,
    v.VendorName,
    v.TIN,
    v.Address AS VendorAddress,
    i.ConditionId,
    cond.ConditionName,
    s.CreatedAt,
    s.CreatedBy
FROM dbo.[Set] s
INNER JOIN dbo.SetItem si      ON s.SetId = si.SetId
LEFT JOIN dbo.Branch b         ON si.BranchId = b.BranchId  -- CRITICAL JOIN for Site
LEFT JOIN dbo.Item i           ON si.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
WHERE s.SetType IN ('Software/License','Service')
  AND s.Status IS NOT NULL;
GO

PRINT 'vw_InvoiceItems view updated successfully!';
PRINT '';
PRINT 'Key changes:';
PRINT '1. Site now comes from SetItem.BranchId -> Branch.BranchName';
PRINT '2. Each item in the invoice shows its correct Site based on its BranchId';
PRINT '3. This fixes the issue where all items showed the same Site value';
PRINT '';
PRINT 'Testing the view...';

SELECT TOP 10
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
PRINT 'Verification: Check if Site values are correct for each item above.';
PRINT 'Each item should show its own Site, not all the same value.';
GO
