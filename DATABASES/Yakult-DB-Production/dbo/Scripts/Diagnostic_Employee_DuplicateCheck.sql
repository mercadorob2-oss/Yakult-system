-- ============================================================
-- Diagnostic: Employee — Duplicate record check
-- ============================================================
-- PURPOSE
--   dbo.Employee has no unique constraint on EmployeeNumber, and
--   the WPF Employee Management import matches rows first by
--   EmployeeNumber, falling back to Name+Company when it's blank.
--   This script surfaces any duplicates that slipped in — from
--   manual entry, a re-import, or an employee re-added under a
--   different Emp # after a transfer/rehire.
--
--   Each duplicate group shows FirstAdded/LastAdded (DateCreated)
--   plus a per-row "Emp#123 @ timestamp by CreatedByName" breakdown,
--   so you can tell whether a duplicate came from one bulk import
--   (dates/creator clustered together) or grew over time via manual
--   entry (dates spread out, different creators).
--
-- HOW TO RUN
--   Run each block individually (highlight + execute).
--   Expected result for every block: 0 rows, unless noted.
--   Archived employees (dbo.ArchiveStatus) are flagged via the
--   IsArchived column so you can judge whether a "duplicate" is
--   actually an old record that was never cleaned up.
-- ============================================================

-- ── 1. Duplicate EmployeeNumber (active employees only) ────────
-- Same Emp # assigned to more than one currently-active employee.
-- This is the case the import logic relies on most — if this
-- returns rows, the import's "match by EmployeeNumber" step is
-- ambiguous for those numbers (it will only ever update whichever
-- row SQL happens to return first).
PRINT '--- 1. Duplicate EmployeeNumber (active) ---';
SELECT e.EmployeeNumber,
       COUNT(*)                AS DuplicateCount,
       MIN(e.DateCreated)      AS FirstAdded,
       MAX(e.DateCreated)      AS LastAdded,
       STRING_AGG(
           CONCAT('Emp#', e.EmpId,
                  ' @ ', CONVERT(VARCHAR(19), e.DateCreated, 120),
                  ' by ', ISNULL(u.Name, 'Unknown')),
           ' | '
       ) WITHIN GROUP (ORDER BY e.DateCreated) AS Details
FROM   dbo.Employee e
LEFT JOIN dbo.[User] u
       ON u.UserId = e.CreatedBy
LEFT JOIN dbo.ArchiveStatus arc
       ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
WHERE  e.EmployeeNumber IS NOT NULL
  AND  LTRIM(RTRIM(e.EmployeeNumber)) <> ''
  AND  arc.ArchiveId IS NULL
GROUP BY e.EmployeeNumber
HAVING COUNT(*) > 1
ORDER BY FirstAdded DESC;
-- Expected: 0 rows
GO

-- ── 2. Duplicate EmployeeNumber (including archived) ────────────
-- Same as above but without excluding archived rows — an active
-- employee sharing a number with an archived one is lower-risk
-- (archived rows are excluded from import matching) but still
-- worth knowing about, e.g. a rehire reusing an old Emp #.
PRINT '--- 2. Duplicate EmployeeNumber (incl. archived) ---';
SELECT e.EmployeeNumber,
       COUNT(*)                                                   AS TotalCount,
       SUM(CASE WHEN arc.ArchiveId IS NULL THEN 1 ELSE 0 END)     AS ActiveCount,
       SUM(CASE WHEN arc.ArchiveId IS NOT NULL THEN 1 ELSE 0 END) AS ArchivedCount,
       MIN(e.DateCreated)                                         AS FirstAdded,
       MAX(e.DateCreated)                                         AS LastAdded,
       STRING_AGG(
           CONCAT('Emp#', e.EmpId,
                  ' @ ', CONVERT(VARCHAR(19), e.DateCreated, 120),
                  ' by ', ISNULL(u.Name, 'Unknown'),
                  CASE WHEN arc.ArchiveId IS NOT NULL THEN ' [ARCHIVED]' ELSE '' END),
           ' | '
       ) WITHIN GROUP (ORDER BY e.DateCreated) AS Details
FROM   dbo.Employee e
LEFT JOIN dbo.[User] u
       ON u.UserId = e.CreatedBy
LEFT JOIN dbo.ArchiveStatus arc
       ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
WHERE  e.EmployeeNumber IS NOT NULL
  AND  LTRIM(RTRIM(e.EmployeeNumber)) <> ''
GROUP BY e.EmployeeNumber
HAVING COUNT(*) > 1
ORDER BY ActiveCount DESC, FirstAdded DESC;
-- Expected: rows here that aren't in block 1 are archived-vs-active
-- overlaps only — review, not necessarily an error.
GO

-- ── 3. Duplicate Name + Company (active employees only) ─────────
-- The import's fallback match key when EmployeeNumber is blank.
-- Two active employees with the same name at the same company is
-- either a genuine duplicate entry or two different people who
-- happen to share a name — check Position/Branch to tell them apart.
PRINT '--- 3. Duplicate Name + Company (active) ---';
SELECT LTRIM(RTRIM(e.Name))    AS Name,
       c.Name                  AS CompanyName,
       COUNT(*)                AS DuplicateCount,
       MIN(e.DateCreated)      AS FirstAdded,
       MAX(e.DateCreated)      AS LastAdded,
       STRING_AGG(
           CONCAT('Emp#', e.EmpId,
                  ' @ ', CONVERT(VARCHAR(19), e.DateCreated, 120),
                  ' by ', ISNULL(u.Name, 'Unknown')),
           ' | '
       ) WITHIN GROUP (ORDER BY e.DateCreated) AS Details
FROM   dbo.Employee e
JOIN   dbo.Company c ON c.ComId = e.ComId
LEFT JOIN dbo.[User] u
       ON u.UserId = e.CreatedBy
LEFT JOIN dbo.ArchiveStatus arc
       ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
WHERE  arc.ArchiveId IS NULL
GROUP BY LTRIM(RTRIM(e.Name)), c.Name
HAVING COUNT(*) > 1
ORDER BY FirstAdded DESC;
-- Expected: 0 rows, or reviewed/confirmed as distinct people
GO

-- ── 4. Full-field duplicates (active employees only) ────────────
-- Same Name + Company + Branch + Department + Position — the
-- highest-confidence signal of an accidental double import, since
-- every visible field matches.
PRINT '--- 4. Full-field duplicates (active) ---';
SELECT LTRIM(RTRIM(e.Name))    AS Name,
       c.Name                  AS CompanyName,
       b.Name                  AS BranchName,
       d.Name                  AS DepartmentName,
       e.Position,
       COUNT(*)                AS DuplicateCount,
       MIN(e.DateCreated)      AS FirstAdded,
       MAX(e.DateCreated)      AS LastAdded,
       STRING_AGG(
           CONCAT('Emp#', e.EmpId,
                  ' @ ', CONVERT(VARCHAR(19), e.DateCreated, 120),
                  ' by ', ISNULL(u.Name, 'Unknown')),
           ' | '
       ) WITHIN GROUP (ORDER BY e.DateCreated) AS Details
FROM   dbo.Employee e
JOIN   dbo.Company  c ON c.ComId    = e.ComId
JOIN   dbo.Branch   b ON b.BranchId = e.BranchId
LEFT JOIN dbo.Department d ON d.DeptId = e.DeptId
LEFT JOIN dbo.[User] u
       ON u.UserId = e.CreatedBy
LEFT JOIN dbo.ArchiveStatus arc
       ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
WHERE  arc.ArchiveId IS NULL
GROUP BY LTRIM(RTRIM(e.Name)), c.Name, b.Name, d.Name, e.Position
HAVING COUNT(*) > 1
ORDER BY FirstAdded DESC;
-- Expected: 0 rows
GO

-- ── 5. Duplicates added within the same minute ──────────────────
-- Narrows blocks 3/4 to duplicate pairs whose DateCreated values
-- are within 60 seconds of each other — a strong sign of a single
-- bulk import/paste event rather than two unrelated manual entries.
PRINT '--- 5. Duplicate Name+Company added within 60 seconds of each other ---';
SELECT LTRIM(RTRIM(e.Name))    AS Name,
       c.Name                  AS CompanyName,
       COUNT(*)                AS DuplicateCount,
       MIN(e.DateCreated)      AS FirstAdded,
       MAX(e.DateCreated)      AS LastAdded,
       DATEDIFF(SECOND, MIN(e.DateCreated), MAX(e.DateCreated)) AS SecondsApart,
       STRING_AGG(CAST(e.EmpId AS VARCHAR), ', ') AS EmpIds
FROM   dbo.Employee e
JOIN   dbo.Company c ON c.ComId = e.ComId
LEFT JOIN dbo.ArchiveStatus arc
       ON arc.EntityType = 'Employee' AND arc.EntityId = e.EmpId AND arc.IsArchived = 1
WHERE  arc.ArchiveId IS NULL
GROUP BY LTRIM(RTRIM(e.Name)), c.Name
HAVING COUNT(*) > 1
   AND DATEDIFF(SECOND, MIN(e.DateCreated), MAX(e.DateCreated)) <= 60
ORDER BY FirstAdded DESC;
-- Expected: 0 rows. Non-empty here points at a specific import run —
-- cross-check the timestamp against known import dates.
GO

-- ── 6. Summary counts ─────────────────────────────────────────
PRINT '--- 6. Summary ---';
SELECT
    (SELECT COUNT(*) FROM dbo.Employee) AS TotalEmployeeRows,
    (SELECT COUNT(*) FROM dbo.Employee e
       LEFT JOIN dbo.ArchiveStatus arc ON arc.EntityType='Employee' AND arc.EntityId=e.EmpId AND arc.IsArchived=1
     WHERE arc.ArchiveId IS NULL) AS ActiveEmployeeRows,
    (SELECT COUNT(*) FROM dbo.ArchiveStatus WHERE EntityType='Employee' AND IsArchived=1) AS ArchivedEmployeeRows;
GO
