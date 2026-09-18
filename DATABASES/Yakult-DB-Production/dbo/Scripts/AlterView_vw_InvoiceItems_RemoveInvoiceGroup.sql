-- Removes InvoiceGroupNum (and the join to dbo.InvoiceGroup) now that the Invoice Group
-- feature is being dropped entirely — see Migration_InvoiceGroup_Remove.sql. Run this
-- BEFORE that migration, since it drops dbo.InvoiceGroup.
--
-- Also adds si.SubType AS SetItemSubType (Part B only — Part A's Request-based rows have
-- no SetItem row to read from, so it's NULL there), alongside the existing
-- i.SubType AS ItemSubType (the catalog default). Run after Migration_SetItem_AddSubType.sql.
ALTER VIEW [dbo].[vw_InvoiceItems]
AS

/* ----------------------------------------------------------
   PART A — Invoice Sets created from REQUEST
---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,

    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS DocumentDate,

    s.Status,
    s.Remarks,

    DATENAME(
        MONTH,
        ISNULL(s.StartDate, s.DispatchDate) AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
    ) AS [Month],

    YEAR(
        ISNULL(s.StartDate, s.DispatchDate) AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
    ) AS [Year],

    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,

    CAST(
        s.StartDate AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS StartDate,

    CAST(
        s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS EndDate,

    DATEDIFF(
        DAY,
        CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                         AT TIME ZONE 'Singapore Standard Time' AS datetime),
        CAST(s.EndDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time' AS datetime)
    ) AS DaysUntilExpiry,

    CASE
        WHEN CAST(s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time' AS datetime)
             < CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time' AS datetime)
        THEN 'Expired'

        WHEN DATEDIFF(
                DAY,
                CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                                 AT TIME ZONE 'Singapore Standard Time' AS datetime),
                CAST(s.EndDate AT TIME ZONE 'UTC'
                               AT TIME ZONE 'Singapore Standard Time' AS datetime)
             ) <= 30
        THEN 'Expiring Soon'

        ELSE 'Active'
    END AS ExpiryStatus,

    s.Site,
    s.ComId,
    co.Name AS CompanyName,

    s.InvoiceSubType,
    i.SubType AS ItemSubType,
    CAST(NULL AS NVARCHAR(20)) AS SetItemSubType,

    r.ReqId,
    r.ItemId,

    i.Name AS ItemName,
    i.Description AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.Name AS CategoryName,

    r.Quantity,
    r.UnitPrice,
    (r.Quantity * r.UnitPrice) AS LineTotal,

    i.VendorId,
    v.VendorName,
    v.TIN,
    v.Address AS VendorAddress,

    i.ConditionId,
    cond.ConditionName,

    COALESCE(rn.PartNumber, a.ModelNumber) AS PartNumber,
    a.SerialNumber AS AssetSerialNumber,

    s.CreatedAt,
    s.CreatedBy

FROM dbo.[Set] s
LEFT JOIN dbo.Request r        ON s.ReqId = r.ReqId
LEFT JOIN dbo.Item i           ON r.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
OUTER APPLY (
    SELECT TOP (1) rn2.PartNumber, rn2.AssetId
    FROM dbo.Renewals rn2
    WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
    ORDER BY rn2.RenewalId DESC
) rn
LEFT JOIN dbo.Asset a          ON rn.AssetId = a.AssetId
WHERE s.IsInvoice = 1
  AND s.ReqId IS NOT NULL

UNION ALL

/* ----------------------------------------------------------
   PART B — Direct Invoice Sets (NO REQUEST)
---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,

    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS DocumentDate,

    s.Status,
    s.Remarks,

    DATENAME(
        MONTH,
        ISNULL(s.StartDate, s.DispatchDate) AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
    ) AS [Month],

    YEAR(
        ISNULL(s.StartDate, s.DispatchDate) AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
    ) AS [Year],

    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.DocumentNumber,
    s.ReferenceNumber,

    CAST(
        s.StartDate AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS StartDate,

    CAST(
        s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS EndDate,

    DATEDIFF(
        DAY,
        CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                         AT TIME ZONE 'Singapore Standard Time' AS datetime),
        CAST(s.EndDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time' AS datetime)
    ) AS DaysUntilExpiry,

    CASE
        WHEN CAST(s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time' AS datetime)
             < CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time' AS datetime)
        THEN 'Expired'

        WHEN DATEDIFF(
                DAY,
                CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                                 AT TIME ZONE 'Singapore Standard Time' AS datetime),
                CAST(s.EndDate AT TIME ZONE 'UTC'
                               AT TIME ZONE 'Singapore Standard Time' AS datetime)
             ) <= 30
        THEN 'Expiring Soon'

        ELSE 'Active'
    END AS ExpiryStatus,

    s.Site,
    s.ComId,
    co.Name AS CompanyName,

    s.InvoiceSubType,
    i.SubType AS ItemSubType,
    si.SubType AS SetItemSubType,

    NULL AS ReqId,
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

    COALESCE(rn.PartNumber, a.ModelNumber) AS PartNumber,
    a.SerialNumber AS AssetSerialNumber,

    s.CreatedAt,
    s.CreatedBy

FROM dbo.[Set] s
INNER JOIN dbo.SetItem si      ON s.SetId = si.SetId
LEFT JOIN dbo.Item i           ON si.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
OUTER APPLY (
    SELECT TOP (1) rn2.PartNumber, rn2.AssetId
    FROM dbo.Renewals rn2
    WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
    ORDER BY rn2.RenewalId DESC
) rn
LEFT JOIN dbo.Asset a          ON rn.AssetId = a.AssetId
WHERE s.IsInvoice = 1
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Request r2
        WHERE r2.ReqId = s.ReqId AND r2.ItemId = si.ItemId
      );
