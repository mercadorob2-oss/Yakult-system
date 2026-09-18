-- ========================================================================
-- FIX: Invoice Report Site Column Issue (CORRECTED VERSION)
-- Problem: Site values showing incorrectly when generating reports
-- Solution: Site is a text field in Set table, but needs to come from SetItem
-- ========================================================================

USE Yakult_Inventory_System;
GO

PRINT '╔════════════════════════════════════════════════════════════════════════╗';
PRINT '║              Checking SetItem table structure for Site...              ║';
PRINT '╚════════════════════════════════════════════════════════════════════════╝';
PRINT '';

-- First, let's check if SetItem has a Site column
IF EXISTS (
    SELECT 1 
    FROM INFORMATION_SCHEMA.COLUMNS 
    WHERE TABLE_NAME = 'SetItem' 
    AND COLUMN_NAME = 'Site'
)
BEGIN
    PRINT '✓ SetItem.Site column exists';
    PRINT '';
    PRINT 'Creating view that uses SetItem.Site...';
    PRINT '';
    
    -- Drop existing view
    IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
        DROP VIEW dbo.vw_InvoiceItems;
    GO

    -- Recreate view pulling Site from SetItem
    CREATE VIEW [dbo].[vw_InvoiceItems]
    AS
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
        -- Get Site from SetItem (user-inputted text field)
        ISNULL(si.Site, 'N/A') AS Site,
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
    LEFT JOIN dbo.Item i           ON si.ItemId = i.ItemId
    LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
    LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
    LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
    LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
    WHERE s.SetType IN ('Software/License','Service')
      AND s.Status IS NOT NULL;
    GO
    
    PRINT '✓ View created using SetItem.Site';
END
ELSE
BEGIN
    PRINT '⚠ WARNING: SetItem.Site column does NOT exist';
    PRINT '';
    PRINT 'Site is stored in Set table only. All items will show the same Site.';
    PRINT '';
    PRINT 'Creating view that uses Set.Site (fallback)...';
    PRINT '';
    
    -- Drop existing view
    IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
        DROP VIEW dbo.vw_InvoiceItems;
    GO

    -- Recreate view pulling Site from Set (original behavior)
    CREATE VIEW [dbo].[vw_InvoiceItems]
    AS
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
        -- Get Site from Set table (all items will have same Site)
        ISNULL(s.Site, 'N/A') AS Site,
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
    LEFT JOIN dbo.Item i           ON si.ItemId = i.ItemId
    LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
    LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
    LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
    LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
    WHERE s.SetType IN ('Software/License','Service')
      AND s.Status IS NOT NULL;
    GO
    
    PRINT '⚠ View created using Set.Site (all items will show same Site)';
    PRINT '';
    PRINT 'RECOMMENDATION: Add Site column to SetItem table if each item';
    PRINT 'needs its own Site value.';
END

PRINT '';
PRINT '════════════════════════════════════════════════════════════════════════';
PRINT 'vw_InvoiceItems view updated successfully!';
PRINT '';
PRINT 'Testing the view...';

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
PRINT 'Verification: Check if Site values appear correctly above.';
GO
