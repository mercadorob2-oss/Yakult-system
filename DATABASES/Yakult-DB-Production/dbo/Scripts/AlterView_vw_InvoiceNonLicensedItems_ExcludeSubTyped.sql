-- A Hardware item explicitly tagged with a Sub-Type (Contract/Subscription/License/
-- Services via BatchAddItemDialog/EditItemDialog — see Migration_Item_AddSubType.sql) is
-- now self-evidently licensable, same as a Software/License or Services item — it no
-- longer needs to sit in the manual "Invoice License Review" queue. A Hardware item with
-- no Sub-Type set (still the common case) is unaffected and keeps landing in the queue.
-- Run after Migration_Item_AddSubType.sql.
ALTER VIEW [dbo].[vw_InvoiceNonLicensedItems]
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
