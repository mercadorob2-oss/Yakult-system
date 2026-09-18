-- ==========================================================================
-- VIEW: vw_Report_Sets
-- PURPOSE: Reporting view for the "View Sets" module.
--          Flattens Set + Company + Branch + Department.
--          Excludes Invoice Sets (those are covered by vw_InvoiceItems).
--          Adds Year / MonthNumber / MonthName for RDLC grouping.
-- DATE ANCHOR: COALESCE(DispatchDate, CreatedAt) — never NULL.
-- ==========================================================================

CREATE VIEW [dbo].[vw_Report_Sets]
AS
SELECT
    s.SetId,
    s.SetCode,
    ISNULL(s.SetType, 'Hardware')                               AS SetType,

    -- Document date (prefer DispatchDate; fall back to CreatedAt)
    CONVERT(date, ISNULL(s.DispatchDate, s.CreatedAt))          AS DocumentDate,

    -- ── Grouping columns (never NULL) ─────────────────────────────────────
    YEAR(ISNULL(s.DispatchDate, s.CreatedAt))                   AS [Year],
    MONTH(ISNULL(s.DispatchDate, s.CreatedAt))                  AS MonthNumber,
    DATENAME(MONTH, ISNULL(s.DispatchDate, s.CreatedAt))        AS MonthName,

    -- ── Document fields ───────────────────────────────────────────────────
    ISNULL(s.DocumentNumber,  '')                               AS DocumentNumber,
    ISNULL(s.ReferenceNumber, '')                               AS ReferenceNumber,
    ISNULL(s.Status,          'Pending')                        AS Status,
    ISNULL(s.Remarks,         '')                               AS Remarks,

    CONVERT(date, s.StartDate)                                  AS StartDate,
    CONVERT(date, s.EndDate)                                    AS EndDate,

    -- ── Company / Location ────────────────────────────────────────────────
    ISNULL(c.Name,  'N/A')                                      AS CompanyName,
    ISNULL(s.Site,  '')                                         AS SiteName,
    ISNULL(b.Name,  'N/A')                                      AS CurrentBranchName,
    ISNULL(d.Name,  'N/A')                                      AS CurrentDepartmentName,

    -- ── Financials ────────────────────────────────────────────────────────
    ISNULL(s.Subtotal,       0)                                 AS Subtotal,
    ISNULL(s.VatAmount,      0)                                 AS VatAmount,
    ISNULL(s.WhtAmount,      0)                                 AS WhtAmount,
    ISNULL(s.DiscountAmount, 0)                                 AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0)                                 AS TotalAmountDue,

    -- ── Audit ─────────────────────────────────────────────────────────────
    s.CreatedBy,
    CONVERT(date, s.CreatedAt)                                  AS CreatedDate

FROM dbo.[Set] s
LEFT JOIN dbo.Company    c ON s.ComId               = c.ComId
LEFT JOIN dbo.Branch     b ON s.CurrentBranchId     = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId

-- Exclude Invoice Sets — those belong to InvoiceReport (already has RDL)
WHERE ISNULL(s.IsInvoice, 0) = 0;
