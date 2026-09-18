
-- ==========================================================================
-- VIEW: vw_InvoiceNonLicensedItems
--
-- PURPOSE:
--   Reporting/isolation view backing the "Invoice License Review" page.
--   Returns invoice line items (from dbo.[Set] WHERE IsInvoice = 1) whose
--   associated Item has ItemType = 'Hardware' AND has not yet been manually
--   reviewed (dbo.Item.LicenseReviewStatus IS NULL). THIS VIEW IS READ ONLY
--   — the app must never write to it directly; it always writes to
--   dbo.Item.LicenseReviewStatus/ReviewedBy/ReviewedAt (see
--   Migration_Item_AddLicenseReviewStatus.sql), and this view automatically
--   reflects that once the underlying UPDATE commits (this is a plain view,
--   not an indexed/materialized one — no separate "refresh" step exists or
--   is needed in SQL Server).
--
-- IMPORTANT — WHY "Hardware" ALONE ISN'T ENOUGH TO CALL A ROW WRONG:
--   There is no database column, category, or naming pattern that reliably
--   tells us whether a given hardware line was legitimately part of a
--   license/subscription invoice (e.g. a Database Server license, Cisco IP
--   Phone service) or was mistakenly attached alongside licensed items.
--   That distinction can only be confirmed by physically checking the
--   original sales invoice — it is NOT something this view (or any query)
--   can determine on its own. An earlier version of this view guessed at
--   exception categories via a hardcoded keyword list ('Database Server',
--   'Cisco', 'IP Phone'); that was a fabricated assumption, not a verified
--   rule, and has been removed.
--
--   Every Hardware line on an invoice starts with LicenseReviewStatus =
--   NULL and shows up here as a review-queue row. A staff member uses the
--   Invoice License Review page to check the actual invoice and mark each
--   one 'Licensed' (genuine exception — keep) or 'NonLicensed' (mistake).
--   Either classification removes the row from this view automatically,
--   since both are no longer NULL.
--
--   Licensable, by contrast, IS reliably determined today without review:
--     i.ItemType IN ('Software/License', 'Services')
--     OR i.SubType IS NOT NULL (a Hardware item explicitly tagged
--        Contract/Subscription/License/Services via BatchAddItemDialog/
--        EditItemDialog — see Migration_Item_AddSubType.sql)
--   A Hardware item with no Sub-Type set still lands in this queue —
--   SubType is optional, so its absence is not itself evidence either way.
--
--   Structurally this is the same two-part shape as dbo.vw_InvoiceItems
--   (Part A = invoice Sets created from a Request, Part B = invoice Sets
--   with items attached directly via SetItem, no Request), with
--   Department/Employee columns added. Nothing in vw_InvoiceItems or
--   vw_Invoices is modified.
-- ==========================================================================

CREATE VIEW [dbo].[vw_InvoiceNonLicensedItems]
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
  AND i.SubType IS NULL
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
  AND i.SubType IS NULL
  AND i.LicenseReviewStatus IS NULL
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Request r2
        WHERE r2.ReqId = s.ReqId AND r2.ItemId = si.ItemId
      );
