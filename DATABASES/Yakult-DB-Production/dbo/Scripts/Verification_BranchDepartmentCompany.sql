-- ============================================================
-- Verification Queries: BranchDepartmentCompany Migration
-- Run these AFTER Migration_BranchDepartmentCompany_NullableDept.sql
-- ============================================================


-- ── V1. Schema check: confirm DepartmentID is nullable ───────────────
SELECT
    c.[name]         AS ColumnName,
    t.[name]         AS DataType,
    c.[is_nullable]  AS IsNullable,       -- must be 1
    c.[max_length]
FROM   sys.columns c
JOIN   sys.types   t ON t.[user_type_id] = c.[user_type_id]
WHERE  c.[object_id] = OBJECT_ID(N'dbo.BranchDepartmentCompany')
  AND  c.[name]      = N'DepartmentID';
-- Expected: IsNullable = 1
GO


-- ── V2. Constraint inventory: confirm old UQ replaced by two indexes ─
SELECT
    i.[name]          AS IndexName,
    i.[type_desc]     AS IndexType,
    i.[is_unique]     AS IsUnique,
    i.[has_filter]    AS IsFiltered,
    i.[filter_definition] AS FilterExpr
FROM   sys.indexes i
WHERE  i.[object_id] = OBJECT_ID(N'dbo.BranchDepartmentCompany')
  AND  i.[name] IN (
      N'UQ_BranchDeptCompany_Binding',  -- should NOT appear
      N'UQ_BDC_WithDept',               -- should appear, filtered
      N'UQ_BDC_NoDept'                  -- should appear, filtered
  );
-- Expected rows: UQ_BDC_WithDept (IsFiltered=1) + UQ_BDC_NoDept (IsFiltered=1)
-- UQ_BranchDeptCompany_Binding must NOT appear.
GO


-- ── V3. FK check: confirm both FKs still active after ALTER COLUMN ───
SELECT
    fk.[name]              AS FKName,
    OBJECT_NAME(fk.[parent_object_id]) AS OnTable,
    COL_NAME(fkc.[parent_object_id], fkc.[parent_column_id]) AS FKColumn,
    OBJECT_NAME(fk.[referenced_object_id]) AS RefsTable
FROM   sys.foreign_keys        fk
JOIN   sys.foreign_key_columns fkc
           ON fkc.[constraint_object_id] = fk.[object_id]
WHERE  fk.[parent_object_id] = OBJECT_ID(N'dbo.BranchDepartmentCompany')
ORDER  BY fk.[name];
-- Expected: FK_BranchDeptCompany_Branch, _Company, _Department all present
GO


-- ── V4. Row counts ───────────────────────────────────────────────────
SELECT
    'BDC rows — DepartmentID IS NOT NULL'
        AS Description,
    COUNT(*) AS [RowCount]
FROM   [dbo].[BranchDepartmentCompany]
WHERE  [DepartmentID] IS NOT NULL

UNION ALL

SELECT
    'BDC rows — DepartmentID IS NULL (YMC)',
    COUNT(*)
FROM   [dbo].[BranchDepartmentCompany]
WHERE  [DepartmentID] IS NULL

UNION ALL

SELECT
    'BDC total rows',
    COUNT(*)
FROM   [dbo].[BranchDepartmentCompany];
GO


-- ── V5. Duplicate check: any uniqueness violations? ──────────────────

-- Rows WITH department that appear more than once
SELECT
    [BranchID], [CompanyID], [DepartmentID],
    COUNT(*) AS DuplicateCount
FROM   [dbo].[BranchDepartmentCompany]
WHERE  [DepartmentID] IS NOT NULL
GROUP  BY [BranchID], [CompanyID], [DepartmentID]
HAVING COUNT(*) > 1;
-- Expected: 0 rows

-- Rows WITHOUT department that appear more than once
SELECT
    [BranchID], [CompanyID],
    COUNT(*) AS DuplicateCount
FROM   [dbo].[BranchDepartmentCompany]
WHERE  [DepartmentID] IS NULL
GROUP  BY [BranchID], [CompanyID]
HAVING COUNT(*) > 1;
-- Expected: 0 rows
GO


-- ── V6. Sample data — WITH department ────────────────────────────────
SELECT TOP 20
    bdc.[BranchDeptCompanyID],
    b.[Name]  AS BranchName,
    d.[Name]  AS DepartmentName,
    c.[Name]  AS CompanyName,
    bdc.[BranchEmail],
    bdc.[CreatedDate]
FROM       [dbo].[BranchDepartmentCompany] bdc
JOIN       [dbo].[Branch]      b ON b.[BranchId] = bdc.[BranchID]
JOIN       [dbo].[Department]  d ON d.[DeptId]   = bdc.[DepartmentID]
JOIN       [dbo].[Company]     c ON c.[ComId]     = bdc.[CompanyID]
WHERE      bdc.[DepartmentID] IS NOT NULL
ORDER BY   c.[Name], b.[Name], d.[Name];
GO


-- ── V7. Sample data — WITHOUT department (YMC case) ──────────────────
SELECT TOP 20
    bdc.[BranchDeptCompanyID],
    b.[Name]  AS BranchName,
    '(none)'  AS DepartmentName,
    c.[Name]  AS CompanyName,
    bdc.[BranchEmail],
    bdc.[CreatedDate]
FROM       [dbo].[BranchDepartmentCompany] bdc
JOIN       [dbo].[Branch]  b ON b.[BranchId] = bdc.[BranchID]
JOIN       [dbo].[Company] c ON c.[ComId]     = bdc.[CompanyID]
WHERE      bdc.[DepartmentID] IS NULL
ORDER BY   c.[Name], b.[Name];
GO


-- ── V8. Orphan check: BDC rows whose Branch/Company/Dept no longer exist
SELECT
    bdc.[BranchDeptCompanyID],
    bdc.[BranchID],
    bdc.[CompanyID],
    bdc.[DepartmentID],
    CASE WHEN b.[BranchId]   IS NULL THEN 'MISSING BRANCH'     ELSE 'ok' END AS BranchStatus,
    CASE WHEN c.[ComId]      IS NULL THEN 'MISSING COMPANY'    ELSE 'ok' END AS CompanyStatus,
    CASE
        WHEN bdc.[DepartmentID] IS NOT NULL AND d.[DeptId] IS NULL
        THEN 'MISSING DEPT'
        ELSE 'ok'
    END AS DeptStatus
FROM       [dbo].[BranchDepartmentCompany] bdc
LEFT JOIN  [dbo].[Branch]     b ON b.[BranchId] = bdc.[BranchID]
LEFT JOIN  [dbo].[Company]    c ON c.[ComId]     = bdc.[CompanyID]
LEFT JOIN  [dbo].[Department] d ON d.[DeptId]    = bdc.[DepartmentID]
WHERE  b.[BranchId] IS NULL
    OR c.[ComId]    IS NULL
    OR (bdc.[DepartmentID] IS NOT NULL AND d.[DeptId] IS NULL);
-- Expected: 0 rows
GO


-- ── V9. Deprecation markers visible ──────────────────────────────────
SELECT
    OBJECT_NAME(ep.[major_id])                         AS TableName,
    COL_NAME(ep.[major_id], ep.[minor_id])             AS ColumnName,
    CAST(ep.[value] AS NVARCHAR(500))                  AS Description
FROM   sys.extended_properties ep
WHERE  ep.[name]     = N'MS_Description'
  AND  ep.[major_id] IN (
      OBJECT_ID(N'dbo.Branch'),
      OBJECT_ID(N'dbo.Department')
  )
  AND  ep.[minor_id] IN (
      COLUMNPROPERTY(OBJECT_ID(N'dbo.Branch'),      N'DeptId', 'ColumnId'),
      COLUMNPROPERTY(OBJECT_ID(N'dbo.Department'),  N'BrId',   'ColumnId')
  );
-- Expected: 2 rows (Branch.DeptId + Department.BrId with DEPRECATED text)
GO
