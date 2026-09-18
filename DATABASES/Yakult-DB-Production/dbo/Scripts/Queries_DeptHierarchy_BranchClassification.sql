-- ============================================================
-- QUERIES — Department Hierarchy & Branch Classification
--
-- Requires:
--   Migration_Department_AddParentDeptId.sql   (schema)
--   Migration_Branch_AddBranchType.sql         (schema + data)
--   Migration_DeptHierarchy_SectionToParentDeptId.sql (data)
-- ============================================================


-- ────────────────────────────────────────────────────────────
-- Q1: Full department tree (all levels) using recursive CTE
--     Returns every department with its depth and full path.
-- ────────────────────────────────────────────────────────────
;WITH DeptHierarchy AS (

    -- Anchor: top-level departments (no parent)
    SELECT
        d.[DeptId],
        d.[Name],
        d.[Section],
        d.[ParentDeptId],
        d.[ComId],
        d.[Active],
        0                          AS [Depth],
        CAST(d.[Name] AS NVARCHAR(2000)) AS [FullPath]
    FROM   [dbo].[Department] AS d
    WHERE  d.[ParentDeptId] IS NULL

    UNION ALL

    -- Recursive: children
    SELECT
        child.[DeptId],
        child.[Name],
        child.[Section],
        child.[ParentDeptId],
        child.[ComId],
        child.[Active],
        parent.[Depth] + 1,
        CAST(parent.[FullPath] + N' > ' + child.[Name] AS NVARCHAR(2000))
    FROM   [dbo].[Department] AS child
    JOIN   DeptHierarchy       AS parent ON parent.[DeptId] = child.[ParentDeptId]

)
SELECT
    [DeptId],
    REPLICATE(N'  ', [Depth]) + [Name] AS [IndentedName],
    [Section],
    [ParentDeptId],
    [Depth],
    [FullPath],
    [ComId],
    [Active]
FROM   DeptHierarchy
ORDER  BY [FullPath];
GO


-- ────────────────────────────────────────────────────────────
-- Q2: All branches under a department, including descendant
--     departments (recursive).
--
--     @RootDeptId — the top department you want to search under.
--     Replace the literal value (e.g. 3) with your DeptId.
-- ────────────────────────────────────────────────────────────
DECLARE @RootDeptId INT = 3;   -- ← change to desired DeptId

;WITH DeptTree AS (

    SELECT [DeptId]
    FROM   [dbo].[Department]
    WHERE  [DeptId] = @RootDeptId

    UNION ALL

    SELECT child.[DeptId]
    FROM   [dbo].[Department] AS child
    JOIN   DeptTree            AS parent ON parent.[DeptId] = child.[ParentDeptId]

)
SELECT
    b.[BranchId],
    b.[Name]        AS BranchName,
    b.[BranchType],
    b.[IsDistributor],
    b.[IsDepot],
    b.[IsCenter],
    b.[IsFactory],
    b.[CenterRegion],
    d.[DeptId],
    d.[Name]        AS DepartmentName,
    b.[ComId],
    b.[Active]
FROM       DeptTree              AS dt
JOIN       [dbo].[Department]    AS d  ON d.[DeptId]   = dt.[DeptId]
JOIN       [dbo].[Branch]        AS b  ON b.[DeptId]   = d.[DeptId]
ORDER BY   d.[Name], b.[Name];
GO


-- ────────────────────────────────────────────────────────────
-- Q3: Distributor branches under a department (and its
--     descendant departments).
--
--     Uses BranchType for readability; also filters on the
--     legacy IsDistributor flag for maximum safety.
--
--     @RootDeptId — replace with the department you care about.
-- ────────────────────────────────────────────────────────────
DECLARE @RootDeptId INT = 3;   -- ← change to desired DeptId

;WITH DeptTree AS (

    SELECT [DeptId]
    FROM   [dbo].[Department]
    WHERE  [DeptId] = @RootDeptId

    UNION ALL

    SELECT child.[DeptId]
    FROM   [dbo].[Department] AS child
    JOIN   DeptTree            AS parent ON parent.[DeptId] = child.[ParentDeptId]

)
SELECT
    b.[BranchId],
    b.[Name]        AS DistributorName,
    b.[BranchType],
    b.[CenterRegion],
    d.[DeptId],
    d.[Name]        AS DepartmentName,
    b.[ComId],
    b.[Active]
FROM       DeptTree           AS dt
JOIN       [dbo].[Department] AS d ON d.[DeptId]  = dt.[DeptId]
JOIN       [dbo].[Branch]     AS b ON b.[DeptId]  = d.[DeptId]
WHERE      b.[BranchType]    = N'Distributor'   -- derived classification
  AND      b.[IsDistributor] = 1                -- belt-and-suspenders with original flag
ORDER BY   d.[Name], b.[Name];
GO


-- ────────────────────────────────────────────────────────────
-- Q4: Flat list of immediate children of a department
--     (non-recursive, for UI dropdowns / breadcrumb loaders)
-- ────────────────────────────────────────────────────────────
DECLARE @ParentDeptId INT = 3;   -- ← change to desired DeptId

SELECT
    [DeptId],
    [Name],
    [Section],
    [Active]
FROM   [dbo].[Department]
WHERE  [ParentDeptId] = @ParentDeptId
ORDER  BY [Name];
GO


-- ────────────────────────────────────────────────────────────
-- Q5: Branch classification summary (for reporting / audit)
-- ────────────────────────────────────────────────────────────
SELECT
    b.[BranchType],
    COUNT(*)                                        AS TotalBranches,
    SUM(CASE WHEN b.[Active] = 1 THEN 1 ELSE 0 END) AS ActiveBranches,
    SUM(CASE WHEN b.[Active] = 0 THEN 1 ELSE 0 END) AS InactiveBranches
FROM   [dbo].[Branch] AS b
GROUP  BY b.[BranchType]
ORDER  BY TotalBranches DESC;
GO
