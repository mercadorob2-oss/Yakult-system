-- ==========================================================================
-- SCRIPT: AlterView_vw_Report_SetsWithItems_UseSetItem.sql
-- PURPOSE: Rebuild vw_Report_SetsWithItems to source items from dbo.SetItem
--          instead of dbo.Request, so sets without Request rows are included.
--
-- CHANGES FROM ORIGINAL:
--   - Item source: dbo.Request  →  dbo.SetItem
--   - Quantity/UnitPrice:  req.Quantity / req.UnitPrice  →  si.Quantity / si.UnitPrice
--   - SetItemId:  req.ReqId AS SetItemId  →  si.SetItemId
--   - Employee resolved via OUTER APPLY on first Request linked to Set
--     (COALESCE with s.ReceivedById as fallback), matching GetAllSetsAsync pattern
--   - Company/Branch/Department now from Employee record (not Set.ComId etc.)
--   - Cartridge exclusion: CartridgeModelId IS NULL  →  Category <> 'Cartridge'
--   - Added: ISNULL(s.IsInvoice, 0) = 0  (invoice sets excluded)
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
    ISNULL(emp.Name, 'N/A')                                      AS EmployeeName,

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

    -- ── Org columns from the requester's Employee record ──────────────────
    ISNULL(co.Name,  'N/A')                                      AS CompanyName,
    ISNULL(br.Name,  'N/A')                                      AS BranchName,
    ISNULL(dp.Name,  'N/A')                                      AS DepartmentName

FROM dbo.[Set]      s
INNER JOIN dbo.SetItem    si   ON si.SetId    = s.SetId
INNER JOIN dbo.Item       i    ON i.ItemId    = si.ItemId
OUTER APPLY (
    SELECT TOP 1 r.EmpId
    FROM   dbo.Request r
    WHERE  r.SetId = s.SetId
    ORDER  BY r.ReqId
) top_req
LEFT  JOIN dbo.Employee   emp  ON emp.EmpId   = COALESCE(s.ReceivedById, top_req.EmpId)
LEFT  JOIN dbo.Branch     br   ON br.BranchId = emp.BranchId
LEFT  JOIN dbo.Department dp   ON dp.DeptId   = emp.DeptId
LEFT  JOIN dbo.Company    co   ON co.ComId    = emp.ComId

WHERE ISNULL(s.IsInvoice, 0) = 0
  AND ISNULL(i.Category, '') <> 'Cartridge';
