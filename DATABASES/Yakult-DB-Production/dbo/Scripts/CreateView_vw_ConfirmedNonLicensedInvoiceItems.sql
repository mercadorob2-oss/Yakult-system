-- ============================================================
-- CREATE OR ALTER: dbo.vw_ConfirmedNonLicensedInvoiceItems
-- Safe to run on a fresh database (no view) or to re-apply.
--
-- DEPENDS ON: Migration_Item_AddLicenseReviewStatus.sql having been run
-- first (adds dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt).
--
-- BACKGROUND
--   Companion view to dbo.vw_InvoiceNonLicensedItems (the "Invoice License
--   Review" working queue). Once a staff member reviews a pending Hardware
--   invoice line and marks it 'NonLicensed' (confirmed mistake, not a
--   licensable exception) via that page's Save Changes, the row disappears
--   from the pending queue view — this view is where it ends up instead,
--   backing the "Non-Licensed Invoices" page so the full confirmed list
--   can still be browsed/exported for follow-up.
--
--   THIS VIEW IS READ ONLY. Nothing writes back through it. It does not
--   delete or modify any invoice/Set/SetItem/Request record — the
--   underlying data is exactly what it always was; this view only adds
--   ReviewedByName / ReviewedAt (joined from dbo.[User]/dbo.Item) so the
--   audit trail is visible.
--
-- Depends on: dbo.[Set], dbo.Request, dbo.Item (+ LicenseReviewStatus),
--             dbo.ItemCategory, dbo.SetItem, dbo.Vendor, dbo.[Condition],
--             dbo.Company, dbo.Employee, dbo.Department, dbo.[User].
-- ============================================================

GO

CREATE OR ALTER VIEW [dbo].[vw_ConfirmedNonLicensedInvoiceItems]
AS

/* ------------------------------------------------------------
   PART A — Invoice Sets created from REQUEST
------------------------------------------------------------ */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber                                             AS InvoiceNumber,
    s.ReferenceNumber                                             AS PONumber,

    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    )                                                              AS InvoiceDate,

    s.Status,
    s.Site,
    s.ComId,
    co.Name                                                        AS CompanyName,
    s.TotalAmountDue                                               AS InvoiceTotalAmount,

    dept.Name                                                      AS Department,
    emp.Name                                                       AS Employee,

    r.ReqId,
    i.ItemId,
    i.ItemId                                                       AS ItemCode,
    i.Name                                                         AS ItemName,
    i.Description                                                  AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.CategoryId,
    ic.Name                                                        AS CategoryName,
    i.Category                                                     AS LegacyCategoryText,

    r.Quantity,
    i.UnitOfMeasure                                                AS Unit,
    r.UnitPrice,
    (r.Quantity * r.UnitPrice)                                     AS LineTotal,

    v.VendorName,
    cond.ConditionName,

    reviewer.Name                                                  AS ReviewedByName,
    i.ReviewedAt,

    s.CreatedAt,
    s.CreatedBy

FROM dbo.[Set] s
LEFT JOIN dbo.Request r        ON s.ReqId = r.ReqId
LEFT JOIN dbo.Item i           ON r.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
LEFT JOIN dbo.Employee emp     ON r.EmpId = emp.EmpId
LEFT JOIN dbo.Department dept  ON r.DeptId = dept.DeptId
LEFT JOIN dbo.[User] reviewer  ON i.ReviewedBy = reviewer.UserId
WHERE s.IsInvoice = 1
  AND s.ReqId IS NOT NULL
  AND i.ItemType = 'Hardware'
  AND i.LicenseReviewStatus = 'NonLicensed'

UNION ALL

/* ------------------------------------------------------------
   PART B — Direct Invoice Sets (NO REQUEST)
------------------------------------------------------------ */
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber                                               AS InvoiceNumber,
    s.ReferenceNumber                                               AS PONumber,

    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    )                                                                AS InvoiceDate,

    s.Status,
    s.Site,
    s.ComId,
    co.Name                                                          AS CompanyName,
    s.TotalAmountDue                                                 AS InvoiceTotalAmount,

    -- No Request/Employee on directly-invoiced items; fall back to the
    -- Set's current department/branch (no per-employee owner exists).
    dept.Name                                                        AS Department,
    CAST(NULL AS NVARCHAR(150))                                      AS Employee,

    NULL                                                             AS ReqId,
    i.ItemId,
    i.ItemId                                                         AS ItemCode,
    i.Name                                                           AS ItemName,
    i.Description                                                    AS ItemDescription,
    i.ItemType,
    i.ModelNumber,
    i.SerialNumber,
    ic.CategoryId,
    ic.Name                                                          AS CategoryName,
    i.Category                                                       AS LegacyCategoryText,

    si.Quantity,
    i.UnitOfMeasure                                                  AS Unit,
    si.UnitPrice,
    si.Amount                                                        AS LineTotal,

    v.VendorName,
    cond.ConditionName,

    reviewer.Name                                                    AS ReviewedByName,
    i.ReviewedAt,

    s.CreatedAt,
    s.CreatedBy

FROM dbo.[Set] s
INNER JOIN dbo.SetItem si      ON s.SetId = si.SetId
LEFT JOIN dbo.Item i           ON si.ItemId = i.ItemId
LEFT JOIN dbo.ItemCategory ic  ON i.CategoryId = ic.CategoryId
LEFT JOIN dbo.Vendor v         ON i.VendorId = v.VendorId
LEFT JOIN dbo.[Condition] cond ON i.ConditionId = cond.ConditionId
LEFT JOIN dbo.Company co       ON s.ComId = co.ComId
LEFT JOIN dbo.Department dept  ON s.CurrentDepartmentId = dept.DeptId
LEFT JOIN dbo.[User] reviewer  ON i.ReviewedBy = reviewer.UserId
WHERE s.IsInvoice = 1
  AND i.ItemType = 'Hardware'
  AND i.LicenseReviewStatus = 'NonLicensed'
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Request r2
        WHERE r2.ReqId = s.ReqId AND r2.ItemId = si.ItemId
      );

GO

PRINT 'dbo.vw_ConfirmedNonLicensedInvoiceItems created/updated.';
