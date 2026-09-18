-- ============================================================
-- Migration: Create DepartmentAccount table
-- Purpose:   Stores one account per unique Company/Department/Branch combination
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'[dbo].[DepartmentAccount]') AND type = N'U'
)
BEGIN
    CREATE TABLE [dbo].[DepartmentAccount] (
        [Id]             INT            IDENTITY(1,1)  NOT NULL,
        [CompanyName]    NVARCHAR(200)  NOT NULL,
        [DepartmentName] NVARCHAR(200)  NOT NULL,
        [BranchName]     NVARCHAR(200)  NOT NULL,
        [Username]       NVARCHAR(100)  NOT NULL,
        [PasswordHash]   VARBINARY(MAX) NULL,
        [PasswordSalt]   VARBINARY(MAX) NULL,
        [IsActive]       BIT            NOT NULL CONSTRAINT [DF_DepartmentAccount_IsActive]    DEFAULT (1),
        [DateCreated]    DATETIME       NOT NULL CONSTRAINT [DF_DepartmentAccount_DateCreated] DEFAULT (GETDATE()),

        CONSTRAINT [PK_DepartmentAccount]         PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [UQ_DepartmentAccount_Combo]   UNIQUE ([CompanyName], [DepartmentName], [BranchName])
    );

    PRINT 'Table [dbo].[DepartmentAccount] created.';
END
ELSE
BEGIN
    PRINT 'Table [dbo].[DepartmentAccount] already exists. Skipping.';
END
GO

-- ============================================================
-- Query: All unique Company / Department / Branch combinations
-- with LEFT JOIN to check if a DepartmentAccount already exists.
-- Run this separately to verify data.
-- ============================================================
/*
SELECT DISTINCT
    c.Name  AS CompanyName,
    d.Name  AS DepartmentName,
    b.Name  AS BranchName,
    da.Id             AS AccountId,
    da.Username,
    da.IsActive       AS AccountIsActive,
    da.DateCreated    AS AccountDateCreated
FROM dbo.Employee e
INNER JOIN dbo.Company    c  ON e.ComId    = c.ComId
INNER JOIN dbo.Department d  ON e.DeptId = d.DeptId
INNER JOIN dbo.Branch     b  ON e.BranchId     = b.BranchId
LEFT  JOIN dbo.DepartmentAccount da
       ON  da.CompanyName    = c.Name
       AND da.DepartmentName = d.Name
       AND da.BranchName     = b.Name
WHERE e.Active = 1
ORDER BY c.Name, d.Name, b.Name;
*/
