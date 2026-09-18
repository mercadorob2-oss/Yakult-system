-- =====================================================
-- Renewal Status View
-- Shows all Software/License and Service items with renewal tracking
-- Includes expiry status and days until expiry
-- =====================================================

ALTER VIEW dbo.vw_RenewalStatus
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

    -- DaysUntilExpiry:
    --   Superseded set (has a direct child renewal): frozen at child's StartDate so the
    --   countdown stops ticking once the set has been renewed.
    --   Active set (no child): live countdown against GETDATE().
    CASE
        WHEN s.EndDate IS NULL THEN NULL
        WHEN eff.RenewalStartDate IS NOT NULL
            THEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate)
        ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
    END AS DaysUntilExpiry,

    -- ExpiryStatus: same freeze logic as DaysUntilExpiry.
    CASE
        WHEN eff.RenewalStartDate IS NOT NULL THEN
            CASE
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) < 0  THEN 'Expired'
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 30 THEN 'Expiring Soon'
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 90 THEN 'Warning'
                ELSE 'Active'
            END
        WHEN s.EndDate IS NULL           THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()       THEN 'Expired'
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

    -- Vendor Information (get from items in this set)
    (SELECT TOP 1 i.VendorId
     FROM dbo.SetItem si
     INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
     WHERE si.SetId = s.SetId) AS VendorId,

    (SELECT TOP 1 v.VendorName
     FROM dbo.SetItem si
     INNER JOIN dbo.Item i ON si.ItemId = i.ItemId
     INNER JOIN dbo.Vendor v ON i.VendorId = v.VendorID
     WHERE si.SetId = s.SetId) AS VendorName,

    -- Financial Information
    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    -- Item Count (aggregated from SetItems)
    (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId) AS ItemCount,

    -- Renewed Date (get most recent RenewedDate from Renewals table for items in this set)
    (SELECT TOP 1 r.RenewedDate
     FROM dbo.SetItem si
     INNER JOIN dbo.Renewals r ON si.ItemId = r.ItemId
     WHERE si.SetId = s.SetId AND r.RenewedDate IS NOT NULL
     ORDER BY r.RenewedDate DESC) AS RenewedDate,

    -- Audit Information
    s.CreatedBy,
    s.CreatedAt AS CreatedDate

FROM dbo.[Set] s
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
CROSS APPLY (
    -- RenewalStartDate: StartDate of the earliest direct child, used to freeze the
    -- countdown for superseded sets at the moment the renewal was linked.
    SELECT
        (SELECT TOP 1 s2.StartDate FROM dbo.[Set] s2
         WHERE s2.RenewalOfSetId = s.SetId ORDER BY s2.SetId ASC) AS RenewalStartDate
) eff

-- Filter for Software/License and Services types only
WHERE s.SetType IN ('Software/License', 'Service', 'Services')

GO

-- Test the view
PRINT 'Testing vw_RenewalStatus:'
SELECT TOP 10
    SetId, SetCode, SetType, CompanyName, StartDate, EndDate,
    DaysUntilExpiry, ExpiryStatus, ItemCount
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

PRINT ''
PRINT '============================================'
PRINT 'vw_RenewalStatus created successfully!'
PRINT '============================================'
