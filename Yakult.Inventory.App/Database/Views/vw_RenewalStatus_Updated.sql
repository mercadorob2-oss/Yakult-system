-- =====================================================
-- Updated Renewal Status View
-- Shows all Software/License and Service items with renewal tracking
-- Now uses Set.VendorId instead of deriving from items
-- =====================================================

USE [Yakult_Inventory_System]
GO

IF OBJECT_ID('dbo.vw_RenewalStatus', 'V') IS NOT NULL
    DROP VIEW dbo.vw_RenewalStatus
GO

CREATE VIEW dbo.vw_RenewalStatus
AS
SELECT
    -- Set Core Information
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.DispatchDate AS DocumentDate,
    s.Status,
    s.Remarks,

    -- Renewal Period Information
    s.StartDate,
    s.EndDate,

    -- Calculate Days Until Expiry
    CASE
        WHEN s.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
    END AS DaysUntilExpiry,

    -- Expiry Status
    CASE
        WHEN s.EndDate IS NULL THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    -- Company Information
    s.ComId,
    ISNULL(c.Name, 'N/A') AS CompanyName,

    -- Site Information (stored as text in Set table)
    s.Site AS SiteName,

    -- Branch and Department Information
    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,

    -- Vendor Information (now from Set table directly)
    s.VendorId,
    ISNULL(v.VendorName, 'N/A') AS VendorName,

    -- Financial Information
    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    -- Item Count (aggregated from SetItems)
    (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId) AS ItemCount,

    -- Audit Information
    s.CreatedBy,
    s.CreatedAt AS CreatedDate

FROM dbo.[Set] s
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
LEFT JOIN dbo.Vendor v ON s.VendorId = v.VendorID

-- Filter for Software/License and Service types only
WHERE s.SetType IN ('Software/License', 'Service')

GO

-- Test the updated view
PRINT 'Testing vw_RenewalStatus:';
SELECT TOP 10
    SetId, SetCode, SetType, VendorName, CompanyName,
    CurrentBranchName, CurrentDepartmentName,
    StartDate, EndDate, DaysUntilExpiry, ExpiryStatus, ItemCount
FROM dbo.vw_RenewalStatus
ORDER BY
    CASE ExpiryStatus
        WHEN 'Expired' THEN 1
        WHEN 'Expiring Soon' THEN 2
        WHEN 'Warning' THEN 3
        WHEN 'Active' THEN 4
        ELSE 5
    END,
    DaysUntilExpiry ASC

PRINT '';
PRINT '============================================';
PRINT 'vw_RenewalStatus updated successfully!';
PRINT 'Now includes VendorName from Set.VendorId';
PRINT 'Also includes Branch and Department names';
PRINT '============================================';
GO
