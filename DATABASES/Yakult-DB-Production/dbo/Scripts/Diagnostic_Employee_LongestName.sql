-- ============================================================
-- Diagnostic: Employee — Longest Name check
-- ============================================================
-- PURPOSE
--   Finds which dbo.Employee row(s) have the longest Name value.
--   Used to sanity-check UI layouts (e.g. the requisition form
--   signature block) that need to handle worst-case name length.
--
-- HOW TO RUN
--   Run each block individually (highlight + execute).
-- ============================================================

-- ── Single longest name (active employees) ─────────────────────
-- Highlight this block and execute.
PRINT '--- Longest active employee name ---';
SELECT TOP (1)
       e.EmpId,
       e.Name,
       LEN(e.Name)      AS NameLength,
       e.Position,
       e.EmployeeNumber
FROM   dbo.Employee e
WHERE  e.Active = 1
ORDER BY LEN(e.Name) DESC, e.EmpId ASC;
GO

-- ── Top 20 longest names (active employees) ─────────────────────
-- Useful when several names are close in length, or to eyeball a
-- realistic worst-case sample rather than just the single longest.
-- Highlight this block and execute.
PRINT '--- Top 20 longest active employee names ---';
SELECT TOP (20)
       e.EmpId,
       e.Name,
       LEN(e.Name)      AS NameLength,
       e.Position,
       e.EmployeeNumber
FROM   dbo.Employee e
WHERE  e.Active = 1
ORDER BY LEN(e.Name) DESC, e.EmpId ASC;
GO

-- ── Same check across ALL employees (including inactive) ────────
-- Highlight this block and execute.
PRINT '--- Top 20 longest employee names (all, including inactive) ---';
SELECT TOP (20)
       e.EmpId,
       e.Name,
       LEN(e.Name)      AS NameLength,
       e.Active,
       e.Position,
       e.EmployeeNumber
FROM   dbo.Employee e
ORDER BY LEN(e.Name) DESC, e.EmpId ASC;
GO
