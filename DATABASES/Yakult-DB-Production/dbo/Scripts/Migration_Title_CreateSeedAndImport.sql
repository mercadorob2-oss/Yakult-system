-- ============================================================
-- Migration: Title lookup — create, seed, and link to Employee
-- Source   : Yakult Regular Employees as of Dec. 2025 (Source).xlsx
--            1,010 rows | Raw title values: MR. / MS. / ATTY.
-- Safe to run multiple times.
-- ============================================================

-- ============================================================
-- STEP 1: Create dbo.Title lookup table
-- ============================================================
IF OBJECT_ID('dbo.Title', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Title (
        TitleId     INT           IDENTITY(1,1) NOT NULL,
        Code        NVARCHAR(10)  NOT NULL,
        Description NVARCHAR(50)  NOT NULL,
        CONSTRAINT PK_Title      PRIMARY KEY CLUSTERED (TitleId ASC),
        CONSTRAINT UQ_Title_Code UNIQUE (Code)
    );
    PRINT 'Created table dbo.Title';
END
ELSE
    PRINT 'dbo.Title already exists — skipping CREATE.';

-- ============================================================
-- STEP 2: Seed codes (stripped of trailing period)
-- ============================================================
INSERT INTO dbo.Title (Code, Description)
SELECT v.Code, v.Description
FROM (VALUES
    ('MR',   'Mister'),
    ('MS',   'Miss'),
    ('ATTY', 'Attorney')
) AS v (Code, Description)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.Title t
    WHERE UPPER(LTRIM(RTRIM(t.Code))) = UPPER(LTRIM(RTRIM(v.Code)))
);

PRINT 'dbo.Title seeded.';

-- ============================================================
-- STEP 3: Add Title (raw) to Employee_Staging if missing
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('dbo.Employee_Staging')
      AND  name = 'Title'
)
BEGIN
    ALTER TABLE dbo.Employee_Staging ADD Title NVARCHAR(10) NULL;
    PRINT 'Added Title to dbo.Employee_Staging';
END

-- ============================================================
-- STEP 4: Add TitleId to dbo.Employee if missing
-- ============================================================
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE  object_id = OBJECT_ID('dbo.Employee')
      AND  name = 'TitleId'
)
BEGIN
    ALTER TABLE dbo.Employee ADD TitleId INT NULL;
    PRINT 'Added TitleId to dbo.Employee';
END

-- ============================================================
-- STEP 5: Validation — must return 0 rows before Step 6
-- Shows staging rows whose Title cannot be matched to dbo.Title
-- ============================================================
SELECT
    s.EmployeeNumber,
    s.EmployeeName,
    s.Title                                        AS RawTitle,
    UPPER(REPLACE(LTRIM(RTRIM(s.Title)), '.', '')) AS NormalisedCode
FROM dbo.Employee_Staging s
LEFT JOIN dbo.Title t
       ON t.Code = UPPER(REPLACE(LTRIM(RTRIM(s.Title)), '.', ''))
WHERE t.TitleId IS NULL
  AND s.Title   IS NOT NULL;

-- ============================================================
-- STEP 6: Update Employee.TitleId from staging
-- Matches on EmployeeNumber; strips trailing period from raw Title
-- ============================================================
UPDATE e
SET    e.TitleId = t.TitleId
FROM   dbo.Employee e
INNER JOIN dbo.Employee_Staging s
        ON LTRIM(RTRIM(s.EmployeeNumber)) = LTRIM(RTRIM(e.EmployeeNumber))
INNER JOIN dbo.Title t
        ON t.Code = UPPER(REPLACE(LTRIM(RTRIM(s.Title)), '.', ''))
WHERE  e.TitleId IS NULL   -- skip already-set rows on re-run
  AND  s.Title   IS NOT NULL;

PRINT CONCAT('Updated ', @@ROWCOUNT, ' employee(s) with TitleId.');

-- ============================================================
-- STEP 7: Verify — shows employees still missing a title
-- ============================================================
SELECT
    e.EmpId,
    e.EmployeeNumber,
    e.Name,
    s.Title AS StagingTitle
FROM dbo.Employee e
LEFT JOIN dbo.Employee_Staging s
       ON LTRIM(RTRIM(s.EmployeeNumber)) = LTRIM(RTRIM(e.EmployeeNumber))
WHERE e.TitleId IS NULL;

-- ============================================================
-- STEP 8: Enforce NOT NULL + FK — run only after Step 7 = 0 rows
-- ============================================================
/*
ALTER TABLE dbo.Employee
    ALTER COLUMN TitleId INT NOT NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE  parent_object_id = OBJECT_ID('dbo.Employee')
      AND  name = 'FK_Employee_Title'
)
BEGIN
    ALTER TABLE dbo.Employee
        ADD CONSTRAINT FK_Employee_Title
        FOREIGN KEY (TitleId) REFERENCES dbo.Title (TitleId);
    PRINT 'FK_Employee_Title added.';
END
*/
