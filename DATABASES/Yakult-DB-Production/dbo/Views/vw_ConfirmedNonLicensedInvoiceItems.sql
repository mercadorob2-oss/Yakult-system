
-- ==========================================================================
-- VIEW: vw_ConfirmedNonLicensedInvoiceItems
--
-- PURPOSE:
--   Read-only audit/report view backing the "Non-Licensed Invoices" page —
--   the companion to the "Invoice License Review" page's working queue
--   (dbo.vw_InvoiceNonLicensedItems). Where that view shows Hardware
--   invoice lines still awaiting a decision (LicenseReviewStatus IS NULL),
--   this view shows the ones a staff member has already confirmed as
--   NON-licensable (LicenseReviewStatus = 'NonLicensed') after checking the
--   real invoice — i.e. hardware that was mistakenly attached to an invoice.
--
--   THIS VIEW IS READ ONLY, same as vw_InvoiceNonLicensedItems. Nothing
--   currently writes back through it. No invoice, Set, SetItem, or Request
--   record has been modified or deleted by the classification workflow —
--   this view exists purely so the confirmed list can be browsed/exported
--   for follow-up (e.g. correcting the invoice outside this system), not
--   to drive any further automated action.
--
--   Adds ReviewedByName / ReviewedAt (absent from the pending queue view,
--   since those are always NULL there) so the audit trail — who classified
--   this item and when — is visible on this page.
--
--   Same two-part Part A (Request-linked) / Part B (direct SetItem) shape
--   as vw_InvoiceNonLicensedItems — see that view's comments for why.
-- ==========================================================================

CREATE VIEW [dbo].[vw_ConfirmedNonLicensedInvoiceItems]
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
