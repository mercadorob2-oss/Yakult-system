-- ============================================================
-- Migration: Create dbo.BranchEmail
-- Purpose:   Stores the branch-level email for a
--            Company + Branch combination.
--
--            Email resolution hierarchy (Employee Management):
--              1. Use PersonalEmail if set (per employee).
--              2. Use BranchEmail if set (per company/branch).
--              3. Use DepartmentEmail if set (per company/dept).
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.BranchEmail')
      AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.BranchEmail (
        [Id]             INT            IDENTITY(1,1) NOT NULL,
        [CompanyName]    NVARCHAR(200)  NOT NULL,
        [BranchName]     NVARCHAR(200)  NOT NULL,
        [EmailAddressId] INT            NULL,

        CONSTRAINT [PK_BranchEmail]
            PRIMARY KEY CLUSTERED ([Id] ASC),

        CONSTRAINT [UQ_BranchEmail_CompanyBranch]
            UNIQUE ([CompanyName], [BranchName]),

        CONSTRAINT [FK_BranchEmail_EmailAddress]
            FOREIGN KEY ([EmailAddressId]) REFERENCES dbo.[EmailAddress] ([EmailId])
    );

    PRINT 'Table dbo.BranchEmail created.';
END
ELSE
    PRINT 'Table dbo.BranchEmail already exists. Skipping.';
GO
