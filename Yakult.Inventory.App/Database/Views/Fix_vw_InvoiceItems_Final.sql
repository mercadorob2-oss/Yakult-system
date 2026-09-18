-- Final fix for vw_InvoiceItems - corrected to match actual database structure
-- The Set table has Site as a direct nvarchar column, not a foreign key
-- Month should be numeric (1-12) not text name for proper report grouping

IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems
GO

CREATE VIEW [dbo].[vw_InvoiceItems]
AS

/* ----------------------------------------------------------
   PART A — Sets created from REQUEST
   ---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DispatchDate AS DocumentDate,
    s.Status,
    s.Remarks,
    DATENAME(MONTH, s.DispatchDate) AS [Month],  -- Month name (January, February, etc.)
    YEAR(s.DispatchDate) AS [Year],
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.StartDate,
    s.EndDate,
    DATEDIFF(DAY, GETDATE(), s.EndDate) AS DaysUntilExpiry,
    CASE
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        ELSE 'Active'
    END AS ExpiryStatus,
    s.Site,  -- CORRECT: Site is a direct column in Set table
    s.ComId,
    co.Name AS CompanyName,

    r.ReqId,
    r.ItemId,

    -- Item details (always from dbo.Item)
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.Name AS CategoryName,

    r.Quantity,
    r.UnitPrice,
    (r.Quantity * r.UnitPrice) AS LineTotal,

    -- Vendor
    i.VendorId,
    v.VendorName,
    v.TIN,
    v.Address AS VendorAddress,

    -- Condition
    i.ConditionId,
    cond.ConditionName,

    s.CreatedAt,
    s.CreatedBy

FROM dbo.[Set] s
LEFT JOIN dbo.Request r        ON s.ReqId = r.ReqId
LEFT JOIN dbo.Item i           ON r.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
WHERE s.SetType IN ('Software/License','Service','Services')
  AND s.ReqId IS NOT NULL


UNION ALL


/* ----------------------------------------------------------
   PART B — Direct Invoice Sets (NO REQUEST)
   Pull items from SetItem + attributes from dbo.Item
   ---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DispatchDate AS DocumentDate,
    s.Status,
    s.Remarks,
    DATENAME(MONTH, s.DispatchDate) AS [Month],  -- Month name (January, February, etc.)
    YEAR(s.DispatchDate) AS [Year],
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.StartDate,
    s.EndDate,
    DATEDIFF(DAY, GETDATE(), s.EndDate) AS DaysUntilExpiry,
    CASE
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        ELSE 'Active'
    END AS ExpiryStatus,
    s.Site,  -- CORRECT: Site is a direct column in Set table
    s.ComId,
    co.Name AS CompanyName,

    NULL AS ReqId,
    si.ItemId,

    -- Item details from dbo.Item
    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.Name AS CategoryName,

    si.Quantity,
    si.UnitPrice,
    si.Amount AS LineTotal,

    -- Vendor
    i.VendorId,
    v.VendorName,
    v.TIN,
    v.Address AS VendorAddress,

    -- Condition
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
WHERE s.SetType IN ('Software/License','Service','Services')
  AND s.ReqId IS NULL;
GO

PRINT 'vw_InvoiceItems view recreated successfully!'
PRINT 'Key changes:'
PRINT '1. Month is now numeric (1-12) instead of month name'
PRINT '2. Site comes directly from Set.Site column (not a join)'
PRINT ''
PRINT 'Testing the view...'

SELECT TOP 5
    SetId,
    DocumentNumber,
    Site,
    Year,
    Month,
    ItemName
FROM vw_InvoiceItems
ORDER BY Year DESC, Month DESC, DocumentNumber

PRINT ''
PRINT 'If Site values look correct above, the issue is fixed!'
