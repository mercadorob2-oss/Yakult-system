-- ==========================================================================
-- SCRIPT: AlterView_vw_Report_SetsWithItems_AddDistributorToCompany.sql
-- PURPOSE: Fold the Distributor (dbo.Set.DistributorId) into the existing
--          CompanyName column of the Sets Report view — no new report column.
--          When a Distributor is set on the Set row, CompanyName becomes
--          "<Company> / <Distributor>"; otherwise unchanged.
-- ==========================================================================

ALTER VIEW [dbo].[vw_Report_SetsWithItems]
AS
SELECT
    -- ── Grouping / banner columns ─────────────────────────────────────────
    YEAR  (ISNULL(s.DispatchDate, s.CreatedAt))                  AS [Year],
    MONTH (ISNULL(s.DispatchDate, s.CreatedAt))                  AS MonthNumber,
    DATENAME(MONTH, ISNULL(s.DispatchDate, s.CreatedAt))         AS MonthName,

    -- ── Set-level columns ─────────────────────────────────────────────────
    s.SetId,
    s.SetCode,
    CONVERT(date, ISNULL(s.DispatchDate, s.CreatedAt))           AS DateDispatched,
    emp.Name                                                     AS EmployeeName,

    -- Per-set item sequence number (resets to 1 for each SetCode)
    ROW_NUMBER() OVER (
        PARTITION BY s.SetCode
        ORDER BY si.SetItemId
    )                                                            AS ItemNo,

    -- ── Item-level columns ────────────────────────────────────────────────
    si.SetItemId,
    i.[Name]                                                     AS ItemName,
    ISNULL(i.ModelNumber,  '')                                   AS ModelNumber,
    ISNULL(i.SerialNumber, '')                                   AS SerialNumber,
    i.ItemType,
    ISNULL(i.Category,     '')                                   AS Category,
    ISNULL(si.Quantity,    0)                                    AS Quantity,
    ISNULL(si.UnitPrice,   0)                                    AS Amount,

    -- ── Org columns: employee path first, fall back to direct Request FK ──
    -- Distributor is folded into CompanyName (no new report column).
    CASE
        WHEN dist.Name IS NOT NULL
            THEN COALESCE(co.Name, rc.Name) + ' / ' + dist.Name
        ELSE COALESCE(co.Name, rc.Name)
    END                                                           AS CompanyName,
    COALESCE(br.Name, rb.Name)                                   AS BranchName,
    COALESCE(dp.Name, rd.Name)                                   AS DepartmentName

FROM dbo.[Set]      s
INNER JOIN dbo.SetItem    si   ON si.SetId    = s.SetId
INNER JOIN dbo.Item       i    ON i.ItemId    = si.ItemId
OUTER APPLY (
    SELECT TOP 1 r.EmpId, r.ComId, r.DeptId, r.BranchId
    FROM   dbo.Request r
    WHERE  r.SetId = s.SetId
    ORDER  BY r.ReqId
) top_req
LEFT  JOIN dbo.Employee   emp  ON emp.EmpId   = COALESCE(s.ReceivedById, top_req.EmpId)
-- org units via employee record
LEFT  JOIN dbo.Branch     br   ON br.BranchId = emp.BranchId
LEFT  JOIN dbo.Department dp   ON dp.DeptId   = emp.DeptId
LEFT  JOIN dbo.Company    co   ON co.ComId    = emp.ComId
-- org units directly on the request (dept-level fallback)
LEFT  JOIN dbo.Company    rc   ON rc.ComId    = top_req.ComId
LEFT  JOIN dbo.Department rd   ON rd.DeptId   = top_req.DeptId
LEFT  JOIN dbo.Branch     rb   ON rb.BranchId = top_req.BranchId
-- distributor, folded into CompanyName above
LEFT  JOIN dbo.Distributor dist ON dist.DistributorId = s.DistributorId

WHERE ISNULL(s.IsInvoice, 0) = 0
  AND ISNULL(i.Category, '') <> 'Cartridge';
