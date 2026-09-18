-- ============================================================
-- CREATE OR ALTER: dbo.vw_InvoiceNonLicensedItems
-- Safe to run on a fresh database (no view) or to re-apply.
--
-- DEPENDS ON: Migration_Item_AddLicenseReviewStatus.sql having been run
-- first (adds dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt). Run
-- that migration before this script on a fresh database.
--
-- BACKGROUND
--   ~1,400+ dbo.SetItem / dbo.Request rows attached to invoice Sets
--   (dbo.[Set].IsInvoice = 1) turned out to include non-licensable
--   hardware that should never have been invoiced alongside licensed
--   items (Software, Software/License, Services). This script does NOT
--   delete, move, or modify a single row of existing data — it only adds
--   a reporting view that isolates unreviewed Hardware-type invoice lines
--   for manual review via the new "Invoice License Review" page.
--   dbo.vw_Invoices and dbo.vw_InvoiceItems are untouched and continue to
--   include every row exactly as before.
--
-- HOW THIS VIEW CLASSIFIES ROWS (read this before trusting the output)
--   Licensable IS reliably determined today:
--     dbo.Item.ItemType IN ('Software/License', 'Services')
--     -> This matches the rule already enforced in
--        Yakult.Inventory.App\Pages\Invoice\AddInvoiceItemDialog.cs
--        and BatchAddInvoiceItemsDialog.cs, which restrict NEW invoice
--        item additions to exactly these two ItemType values.
--
--   "Non-licensable" is NOT reliably determined by data alone.
--     dbo.Item.ItemType = 'Hardware' covers BOTH:
--       (a) hardware that was mistakenly attached to an invoice, and
--       (b) approved hardware-licensing exceptions genuinely tied to a
--           license/subscription (e.g. a Database Server license, Cisco
--           IP Phone service) that legitimately belongs on the invoice.
--     There is no column, category, or naming convention anywhere in the
--     schema that distinguishes (a) from (b). The only way to tell them
--     apart is to physically check the original sales invoice.
--
--   This view now surfaces every Hardware-type invoice line that has NOT
--   yet been reviewed (dbo.Item.LicenseReviewStatus IS NULL) as a REVIEW
--   QUEUE, not as a confirmed list of mistakes. The Invoice License Review
--   page lets a staff member mark each one 'Licensed' (genuine exception)
--   or 'NonLicensed' (mistake) after checking the real invoice; either
--   classification removes that item's rows from this view automatically,
--   since the view only ever shows LicenseReviewStatus IS NULL rows.
--
--   THIS VIEW IS READ ONLY. The app must never write to it directly —
--   it writes to dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt, and
--   this (non-indexed, ordinary) view reflects that automatically the
--   moment the UPDATE commits. No SQL Server "refresh" step exists or is
--   needed for a plain view.
--
-- Depends on: dbo.[Set], dbo.Request, dbo.Item (+ LicenseReviewStatus),
--             dbo.ItemCategory, dbo.SetItem, dbo.Vendor, dbo.[Condition],
--             dbo.Company, dbo.Employee, dbo.Department.
-- ============================================================

GO

CREATE OR ALTER VIEW [dbo].[vw_InvoiceNonLicensedItems]
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
WHERE s.IsInvoice = 1
  AND s.ReqId IS NOT NULL
  AND i.ItemType = 'Hardware'
  AND i.LicenseReviewStatus IS NULL

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
WHERE s.IsInvoice = 1
  AND i.ItemType = 'Hardware'
  AND i.LicenseReviewStatus IS NULL
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Request r2
        WHERE r2.ReqId = s.ReqId AND r2.ItemId = si.ItemId
      );

GO

PRINT 'dbo.vw_InvoiceNonLicensedItems created/updated.';
