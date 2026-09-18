-- Fix for vw_InvoiceItems to exclude archived/deleted invoices and items
-- This prevents archived sets, items, and requests from appearing in invoice reports

IF OBJECT_ID('dbo.vw_InvoiceItems', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoiceItems
GO

CREATE VIEW [dbo].[vw_InvoiceItems]
AS

/* ----------------------------------------------------------
   PART A — Sets created from REQUEST
   Excludes archived Sets, Requests, and Items
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
    s.Site,
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

-- FILTER OUT ARCHIVED ITEMS
LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId
LEFT JOIN dbo.ArchiveStatus arch_req ON arch_req.EntityType = 'Request' AND arch_req.EntityId = r.ReqId
LEFT JOIN dbo.ArchiveStatus arch_item ON arch_item.EntityType = 'Item' AND arch_item.EntityId = i.ItemId

WHERE s.SetType IN ('Software/License','Service')
  AND s.ReqId IS NOT NULL
  AND s.Status IS NOT NULL
  -- Exclude archived sets, requests, and items
  AND arch_set.EntityId IS NULL
  AND (r.ReqId IS NULL OR arch_req.EntityId IS NULL)
  AND (i.ItemId IS NULL OR arch_item.EntityId IS NULL)


UNION ALL


/* ----------------------------------------------------------
   PART B — Direct Invoice Sets (NO REQUEST)
   Pull items from SetItem + attributes from dbo.Item
   Excludes archived Sets and Items
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
    s.Site,
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

-- FILTER OUT ARCHIVED ITEMS
LEFT JOIN dbo.ArchiveStatus arch_set ON arch_set.EntityType = 'Set' AND arch_set.EntityId = s.SetId
LEFT JOIN dbo.ArchiveStatus arch_item ON arch_item.EntityType = 'Item' AND arch_item.EntityId = i.ItemId

WHERE s.SetType IN ('Software/License','Service')
  AND s.ReqId IS NULL
  AND s.Status IS NOT NULL
  -- Exclude archived sets and items
  AND arch_set.EntityId IS NULL
  AND (i.ItemId IS NULL OR arch_item.EntityId IS NULL);
GO

PRINT '================================================='
PRINT 'vw_InvoiceItems view updated successfully!'
PRINT '================================================='
PRINT ''
PRINT 'Changes applied:'
PRINT '1. Added filter to exclude archived Sets (EntityType=''Set'')'
PRINT '2. Added filter to exclude archived Requests (EntityType=''Request'')'
PRINT '3. Added filter to exclude archived Items (EntityType=''Item'')'
PRINT ''
PRINT 'This ensures that:'
PRINT '- Archived/deleted invoices do not appear in reports'
PRINT '- Items that were edited will still appear (as long as they are not archived)'
PRINT ''
PRINT '================================================='
PRINT 'Testing the view (showing first 5 records)...'
PRINT '================================================='

SELECT TOP 5
    SetId,
    DocumentNumber,
    ReferenceNumber,
    Site,
    Year,
    Month,
    ItemName,
    CompanyName,
    Status
FROM vw_InvoiceItems
ORDER BY Year DESC, Month DESC, DocumentNumber

PRINT ''
PRINT 'Verifying no archived items appear...'

-- Check if any archived sets are in the view (should return 0)
SELECT COUNT(*) AS ArchivedSetsInView
FROM vw_InvoiceItems vi
INNER JOIN dbo.ArchiveStatus arch ON arch.EntityType = 'Set' AND arch.EntityId = vi.SetId

PRINT ''
PRINT 'If ArchivedSetsInView = 0, the fix is working correctly!'
