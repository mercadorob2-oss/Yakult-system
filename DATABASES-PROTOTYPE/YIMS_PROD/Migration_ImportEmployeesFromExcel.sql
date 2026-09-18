-- ============================================================
-- Migration : Import Employees from Excel
-- Database  : Yakult_Inventory_System_DEV
-- File      : Yakult Regular Employees as of Dec. 2025 (3).xlsx
-- Sheets    : YPI&YMC (1010 rows) + FAC (354 rows)
--
-- PREREQUISITES
--   1. Microsoft ACE OLEDB 12.0 or 16.0 driver installed on the
--      SQL Server host machine.
--      Download: https://www.microsoft.com/en-us/download/details.aspx?id=54920
--   2. SQL Server instance must have Ad Hoc Distributed Queries
--      enabled (STEP 1 handles this automatically).
--
-- BEFORE RUNNING
--   • Set @CreatedBy (STEP 3 config block) to a valid UserId.
--     Run: SELECT UserId, Username FROM dbo.[User] WHERE Active = 1;
--   • Confirm the Excel file path if the file has been moved.
-- ============================================================

USE [Yakult_Inventory_System_DEV];
GO

-- ============================================================
-- STEP 1: Enable Ad Hoc Distributed Queries + configure the
--         ACE OLEDB provider to run in-process with SQL Server.
--         Must be run as sysadmin.
-- ============================================================

-- 1a. Enable Ad Hoc Distributed Queries
EXEC sp_configure 'show advanced options', 1;
RECONFIGURE WITH OVERRIDE;

EXEC sp_configure 'Ad Hoc Distributed Queries', 1;
RECONFIGURE WITH OVERRIDE;

-- 1b. Allow the ACE OLEDB 16.0 provider (2016 x64) to run in-process
EXEC master.dbo.sp_MSset_oledb_prop N'Microsoft.ACE.OLEDB.16.0', N'AllowInProcess',    1;
EXEC master.dbo.sp_MSset_oledb_prop N'Microsoft.ACE.OLEDB.16.0', N'DynamicParameters', 1;

PRINT 'OLEDB provider configured.';
GO

-- ============================================================
-- STEP 2: Ensure Employee_Staging exists with all required columns
--         • If table is missing  → CREATE with full schema
--         • If table exists      → ALTER to add any missing columns
--         Safe to re-run at any time.
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE  object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND type = 'U'
)
BEGIN
    CREATE TABLE [dbo].[Employee_Staging] (
        [StagingId]      INT           IDENTITY(1,1) NOT NULL,
        [Name]           NVARCHAR(150) NOT NULL,
        [CompanyName]    NVARCHAR(150) NOT NULL,
        [DepartmentName] NVARCHAR(150) NOT NULL,
        [BranchName]     NVARCHAR(150) NOT NULL,
        [Position]       NVARCHAR(50)  NULL,
        [EmployeeNumber] VARCHAR(20)   NULL,
        [IsFactory]      BIT           NOT NULL CONSTRAINT [DF_EmpStaging_IsFactory]     DEFAULT(0),
        [IsDepot]        BIT           NOT NULL CONSTRAINT [DF_EmpStaging_IsDepot]       DEFAULT(0),
        [IsDistributor]  BIT           NOT NULL CONSTRAINT [DF_EmpStaging_IsDistributor] DEFAULT(0),
        [IsCenter]       BIT           NOT NULL CONSTRAINT [DF_EmpStaging_IsCenter]      DEFAULT(0),
        [Processed]      BIT           NOT NULL CONSTRAINT [DF_EmpStaging_Processed]     DEFAULT(0),
        CONSTRAINT [PK_Employee_Staging] PRIMARY KEY CLUSTERED ([StagingId] ASC)
    );
    PRINT 'Employee_Staging created.';
END
ELSE
BEGIN
    PRINT 'Employee_Staging exists — checking for missing columns...';

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'Name')
        ALTER TABLE [dbo].[Employee_Staging] ADD [Name]           NVARCHAR(150) NOT NULL DEFAULT('');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'CompanyName')
        ALTER TABLE [dbo].[Employee_Staging] ADD [CompanyName]    NVARCHAR(150) NOT NULL DEFAULT('');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'DepartmentName')
        ALTER TABLE [dbo].[Employee_Staging] ADD [DepartmentName] NVARCHAR(150) NOT NULL DEFAULT('');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'BranchName')
        ALTER TABLE [dbo].[Employee_Staging] ADD [BranchName]     NVARCHAR(150) NOT NULL DEFAULT('');

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'Position')
        ALTER TABLE [dbo].[Employee_Staging] ADD [Position]       NVARCHAR(50)  NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'EmployeeNumber')
        ALTER TABLE [dbo].[Employee_Staging] ADD [EmployeeNumber] VARCHAR(20)   NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'IsFactory')
        ALTER TABLE [dbo].[Employee_Staging] ADD [IsFactory]      BIT NOT NULL CONSTRAINT [DF_EmpStaging_IsFactory]     DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'IsDepot')
        ALTER TABLE [dbo].[Employee_Staging] ADD [IsDepot]        BIT NOT NULL CONSTRAINT [DF_EmpStaging_IsDepot]       DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'IsDistributor')
        ALTER TABLE [dbo].[Employee_Staging] ADD [IsDistributor]  BIT NOT NULL CONSTRAINT [DF_EmpStaging_IsDistributor] DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'IsCenter')
        ALTER TABLE [dbo].[Employee_Staging] ADD [IsCenter]       BIT NOT NULL CONSTRAINT [DF_EmpStaging_IsCenter]      DEFAULT(0);

    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Employee_Staging]') AND name = 'Processed')
        ALTER TABLE [dbo].[Employee_Staging] ADD [Processed]      BIT NOT NULL CONSTRAINT [DF_EmpStaging_Processed]     DEFAULT(0);

    PRINT 'Column check complete.';
END
GO

-- ============================================================
-- STEP 3: Load Excel → Employee_Staging
--
--   Two separate INSERTs, one per sheet.
--   OPENROWSET does not support UNION ALL in a single INSERT.
--   IMEX=1  → reads all columns as text (avoids type errors).
--   FAC sheet has an extra [Title] column which is ignored.
--
--   ↓ Set @CreatedBy to a valid UserId before running ↓
-- ============================================================
DECLARE @CreatedBy    INT  = 1;          -- ← SET to a valid UserId
DECLARE @ExcelPath    NVARCHAR(500) =
    'C:\Users\shawn\YIMS\Yakult Regular Employees as of Dec. 2025 (3).xlsx';

PRINT 'Excel path : ' + @ExcelPath;
PRINT 'CreatedBy  : ' + CAST(@CreatedBy AS VARCHAR(10));

TRUNCATE TABLE [dbo].[Employee_Staging];

-- ── Sheet 1: YPI & YMC ──────────────────────────────────────
INSERT INTO [dbo].[Employee_Staging]
    (Name, CompanyName, DepartmentName, BranchName, Position, EmployeeNumber,
     IsFactory, IsDepot, IsDistributor, IsCenter)
SELECT
    LTRIM(RTRIM([Name]))                    AS Name,
    LTRIM(RTRIM([Company Name]))            AS CompanyName,
    LTRIM(RTRIM([Department Name]))         AS DepartmentName,
    LTRIM(RTRIM([Branch Name]))             AS BranchName,
    LTRIM(RTRIM([Position]))                AS Position,
    CAST([Code] AS VARCHAR(20))             AS EmployeeNumber,
    ISNULL(CAST([IsFactory]     AS BIT), 0) AS IsFactory,
    ISNULL(CAST([IsDepot]       AS BIT), 0) AS IsDepot,
    ISNULL(CAST([IsDistributor] AS BIT), 0) AS IsDistributor,
    ISNULL(CAST([IsCenter]      AS BIT), 0) AS IsCenter
FROM OPENROWSET(
    'Microsoft.ACE.OLEDB.16.0',
    'Excel 16.0 Xml;HDR=YES;IMEX=1;Database=C:\Users\shawn\YIMS\Yakult Regular Employees as of Dec. 2025 (3).xlsx',
    'SELECT * FROM [YPI&YMC$]'
)
WHERE [Name] IS NOT NULL AND [Company Name] IS NOT NULL;

PRINT 'YPI&YMC loaded : ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows.';

-- ── Sheet 2: FAC (Factory) ───────────────────────────────────
INSERT INTO [dbo].[Employee_Staging]
    (Name, CompanyName, DepartmentName, BranchName, Position, EmployeeNumber,
     IsFactory, IsDepot, IsDistributor, IsCenter)
SELECT
    LTRIM(RTRIM([Name]))                    AS Name,
    LTRIM(RTRIM([Company Name]))            AS CompanyName,
    LTRIM(RTRIM([Department Name]))         AS DepartmentName,
    LTRIM(RTRIM([Branch Name]))             AS BranchName,
    LTRIM(RTRIM([Position]))                AS Position,
    CAST([Code] AS VARCHAR(20))             AS EmployeeNumber,
    ISNULL(CAST([IsFactory]     AS BIT), 0) AS IsFactory,
    ISNULL(CAST([IsDepot]       AS BIT), 0) AS IsDepot,
    ISNULL(CAST([IsDistributor] AS BIT), 0) AS IsDistributor,
    ISNULL(CAST([IsCenter]      AS BIT), 0) AS IsCenter
FROM OPENROWSET(
    'Microsoft.ACE.OLEDB.16.0',
    'Excel 16.0 Xml;HDR=YES;IMEX=1;Database=C:\Users\shawn\YIMS\Yakult Regular Employees as of Dec. 2025 (3).xlsx',
    'SELECT * FROM [FAC$]'
)
WHERE [Name] IS NOT NULL AND [Company Name] IS NOT NULL;

PRINT 'FAC loaded     : ' + CAST(@@ROWCOUNT AS VARCHAR(10)) + ' rows.';
GO

-- ============================================================
-- STEP 4: Insert missing Companies
--         Case-insensitive name match. Skips existing records.
-- ============================================================
DECLARE @CreatedBy INT = 1; -- ← SET to a valid UserId

INSERT INTO [dbo].[Company] (Name, CreatedBy)
SELECT DISTINCT s.CompanyName, @CreatedBy
FROM   [dbo].[Employee_Staging] s
WHERE  NOT EXISTS (
    SELECT 1 FROM [dbo].[Company] c
    WHERE  UPPER(LTRIM(RTRIM(c.Name))) = UPPER(s.CompanyName)
      AND  c.Active = 1
);

PRINT 'Companies inserted : ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

-- ============================================================
-- STEP 5: Insert missing Departments
--         Shared across companies; case-insensitive name match.
-- ============================================================
DECLARE @CreatedBy INT = 1; -- ← SET to a valid UserId

INSERT INTO [dbo].[Department] (Name, CreatedBy)
SELECT DISTINCT s.DepartmentName, @CreatedBy
FROM   [dbo].[Employee_Staging] s
WHERE  NOT EXISTS (
    SELECT 1 FROM [dbo].[Department] d
    WHERE  UPPER(LTRIM(RTRIM(d.Name))) = UPPER(s.DepartmentName)
      AND  d.Active = 1
);

PRINT 'Departments inserted : ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

-- ============================================================
-- STEP 6: Insert missing Branches
--         IsFactory / IsDepot / IsDistributor / IsCenter flags
--         are sourced from Excel. MAX() per branch name resolves
--         any rows that had inconsistent flag values.
-- ============================================================
DECLARE @CreatedBy INT = 1; -- ← SET to a valid UserId

WITH BranchData AS (
    SELECT
        BranchName,
        MAX(CAST(IsFactory     AS TINYINT)) AS IsFactory,
        MAX(CAST(IsDepot       AS TINYINT)) AS IsDepot,
        MAX(CAST(IsDistributor AS TINYINT)) AS IsDistributor,
        MAX(CAST(IsCenter      AS TINYINT)) AS IsCenter
    FROM  [dbo].[Employee_Staging]
    GROUP BY BranchName
)
INSERT INTO [dbo].[Branch] (Name, CreatedBy, IsFactory, IsDepot, IsDistributor, IsCenter)
SELECT
    bd.BranchName,
    @CreatedBy,
    CAST(bd.IsFactory     AS BIT),
    CAST(bd.IsDepot       AS BIT),
    CAST(bd.IsDistributor AS BIT),
    CAST(bd.IsCenter      AS BIT)
FROM  BranchData bd
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Branch] b
    WHERE  UPPER(LTRIM(RTRIM(b.Name))) = UPPER(bd.BranchName)
      AND  b.Active = 1
);

PRINT 'Branches inserted : ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

-- ============================================================
-- STEP 7: Insert missing Employees
--         Deduplication : skips any row whose EmployeeNumber
--         already exists in dbo.Employee (Active = 1).
--         FK values (ComId, BranchId, DeptId) are resolved
--         from the lookup tables using case-insensitive names.
-- ============================================================
INSERT INTO [dbo].[Employee] (Name, ComId, BranchId, DeptId, Position, EmployeeNumber)
SELECT
    s.Name,
    c.ComId,
    b.BranchId,
    d.DeptId,
    s.Position,
    s.EmployeeNumber
FROM       [dbo].[Employee_Staging] s
INNER JOIN [dbo].[Company]          c ON UPPER(LTRIM(RTRIM(c.Name))) = UPPER(s.CompanyName)    AND c.Active = 1
INNER JOIN [dbo].[Branch]           b ON UPPER(LTRIM(RTRIM(b.Name))) = UPPER(s.BranchName)     AND b.Active = 1
INNER JOIN [dbo].[Department]       d ON UPPER(LTRIM(RTRIM(d.Name))) = UPPER(s.DepartmentName) AND d.Active = 1
WHERE NOT EXISTS (
    SELECT 1 FROM [dbo].[Employee] e
    WHERE  e.EmployeeNumber = s.EmployeeNumber
      AND  e.Active = 1
);

PRINT 'Employees inserted : ' + CAST(@@ROWCOUNT AS VARCHAR(10));
GO

-- ============================================================
-- STEP 8: Verification
--         All four counts should be 0 after a successful run.
--         Any non-zero row indicates a lookup mismatch.
-- ============================================================
SELECT [Check], [Count] FROM (
    SELECT 1 AS Ord, 'Employees NOT inserted (FK lookup failed)' AS [Check], COUNT(*) AS [Count]
    FROM   [dbo].[Employee_Staging] s
    WHERE  NOT EXISTS (
        SELECT 1 FROM [dbo].[Employee] e
        WHERE  e.EmployeeNumber = s.EmployeeNumber AND e.Active = 1
    )
    UNION ALL
    SELECT 2, 'Missing Company lookup', COUNT(*)
    FROM   [dbo].[Employee_Staging] s
    WHERE  NOT EXISTS (
        SELECT 1 FROM [dbo].[Company] c
        WHERE  UPPER(LTRIM(RTRIM(c.Name))) = UPPER(s.CompanyName) AND c.Active = 1
    )
    UNION ALL
    SELECT 3, 'Missing Branch lookup', COUNT(*)
    FROM   [dbo].[Employee_Staging] s
    WHERE  NOT EXISTS (
        SELECT 1 FROM [dbo].[Branch] b
        WHERE  UPPER(LTRIM(RTRIM(b.Name))) = UPPER(s.BranchName) AND b.Active = 1
    )
    UNION ALL
    SELECT 4, 'Missing Department lookup', COUNT(*)
    FROM   [dbo].[Employee_Staging] s
    WHERE  NOT EXISTS (
        SELECT 1 FROM [dbo].[Department] d
        WHERE  UPPER(LTRIM(RTRIM(d.Name))) = UPPER(s.DepartmentName) AND d.Active = 1
    )
) v
ORDER BY v.Ord;
GO
