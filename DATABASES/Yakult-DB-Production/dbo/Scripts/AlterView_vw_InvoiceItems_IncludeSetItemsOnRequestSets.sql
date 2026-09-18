-- Fixes vw_InvoiceItems dropping dbo.SetItem rows (e.g. hardware items added via the
-- Renew Items feature) whenever the owning Set also has a ReqId set.
--
-- The view is a UNION ALL split on s.ReqId:
--   Part A (s.ReqId IS NOT NULL) -> one line per Set, joined from dbo.Request
--   Part B (s.ReqId IS NULL)     -> all lines from dbo.SetItem
--
-- A Set can have BOTH a ReqId AND extra SetItem rows added later (Renew Items writes
-- SetItem directly). Part B's "s.ReqId IS NULL" filter excluded those Sets entirely,
-- so any SetItem-only lines (including hardware items) never appeared in Invoice
-- reports/exports built on this view -- even though the Set's header still showed up
-- fine in vw_Invoices (header-only, no line items) and the items were visible via
-- Renewal Details -> Invoice Items (which reads dbo.SetItem directly).
--
-- Fix: Part B now runs for every invoice Set and only skips the single item Part A
-- already emitted for that Set (matched by ItemId against dbo.Request), instead of
-- skipping the whole Set whenever ReqId is set.
--
-- Run this against the real production database.

ALTER VIEW [dbo].[vw_InvoiceItems]
AS

/* ----------------------------------------------------------
   PART A — Invoice Sets created from REQUEST
---------------------------------------------------------- */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,

    -- Document Date (SGT)
    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS DocumentDate,

    s.Status,
    s.Remarks,

    -- Grouping anchored to this Set's coverage StartDate (SGT),
    -- falling back to DispatchDate when StartDate is unset.
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

    -- Days Until Expiry (SGT)
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
   PART B — SetItem-sourced lines (direct-invoice Sets, AND any
   extra SetItem rows on request-linked Sets, e.g. renewal-added items)
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

    -- Grouping anchored to this Set's coverage StartDate (SGT),
    -- falling back to DispatchDate when StartDate is unset.
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
  -- Was "AND s.ReqId IS NULL", which hid every SetItem row (e.g. hardware items
  -- added via Renew Items) on a Set that also has a ReqId, since Part A already
  -- claimed the whole Set. Instead, only skip the single line Part A already
  -- emitted for this Set (matched by ItemId against the request-side item),
  -- so extra SetItem rows on request-linked Sets are no longer dropped.
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Request r2
        WHERE r2.ReqId = s.ReqId AND r2.ItemId = si.ItemId
      );
GO
