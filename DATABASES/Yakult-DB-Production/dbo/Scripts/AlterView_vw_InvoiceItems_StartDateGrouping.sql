-- ==========================================================================
-- MIGRATION: Invoice Report [Year]/[Month] grouping columns now anchor to
-- each Set's own StartDate (coverage start), not its DispatchDate
-- (document/dispatch date).
--
-- WHY: same reasoning as AlterView_vw_Report_RenewalsWithItems_StartDateGrouping.sql
-- and AlterView_vw_Report_RenewalGroupsLatest_StartDateGrouping.sql — the
-- report's Year/Month banner is meant to reflect the coverage period, not
-- the day the document happened to be created/dispatched.
--
-- Falls back to DispatchDate only when StartDate is NULL, so rows without
-- a coverage start date still group somewhere sensible instead of
-- disappearing. Only the [Year]/[Month] grouping expressions change in
-- both UNION ALL branches (Part A — request-derived invoices, Part B —
-- direct invoices); DocumentDate/StartDate/EndDate output columns and the
-- SGT timezone-conversion pattern are unchanged.
--
-- SAFE TO RE-RUN: ALTER VIEW is idempotent.
-- ==========================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

ALTER VIEW [dbo].[vw_InvoiceItems]
AS

/* ----------------------------------------------------------
   PART A — Invoice Sets created from REQUEST
---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,

    -- ✅ Document Date (SGT)
    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS DocumentDate,

    s.Status,
    s.Remarks,

    -- ✅ Grouping anchored to this Set's coverage StartDate (SGT),
    --    falling back to DispatchDate when StartDate is unset.
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

    -- ✅ Days Until Expiry (SGT)
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
-- OUTER APPLY (not a plain JOIN) so an item with more than one non-archived
-- Renewals row (e.g. renewed more than once) contributes only its single most
-- recent Renewals row, not one row per Renewals history entry.
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

    -- ✅ Grouping anchored to this Set's coverage StartDate (SGT),
    --    falling back to DispatchDate when StartDate is unset.
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
-- OUTER APPLY (not a plain JOIN) so an item with more than one non-archived
-- Renewals row (e.g. renewed more than once) contributes only its single most
-- recent Renewals row, not one row per Renewals history entry.
OUTER APPLY (
    SELECT TOP (1) rn2.PartNumber, rn2.AssetId
    FROM dbo.Renewals rn2
    WHERE rn2.ItemId = i.ItemId AND rn2.IsArchived = 0
    ORDER BY rn2.RenewalId DESC
) rn
LEFT JOIN dbo.Asset a          ON rn.AssetId = a.AssetId
WHERE s.IsInvoice = 1
  AND s.ReqId IS NULL;
GO
