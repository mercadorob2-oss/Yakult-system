-- ============================================================
-- CREATE OR ALTER: dbo.vw_UserPositionSummary
-- Safe to run on a fresh database (no view) or to re-apply.
-- Shows employee count per position, company, and branch.
-- ============================================================

GO

CREATE OR ALTER VIEW [dbo].[vw_UserPositionSummary]
AS
SELECT
    ISNULL(e.Position, '(No Position)') AS Position,
    ISNULL(c.Name,     '(No Company)')  AS CompanyName,
    ISNULL(b.Name,     '(No Branch)')   AS BranchName,
    COUNT(*)                            AS EmployeeCount
FROM dbo.Employee e
LEFT JOIN dbo.Branch  b ON e.BranchId = b.BranchId
LEFT JOIN dbo.Company c ON e.ComId    = c.ComId
GROUP BY e.Position, c.Name, b.Name;

GO

PRINT 'dbo.vw_UserPositionSummary created/updated.';
