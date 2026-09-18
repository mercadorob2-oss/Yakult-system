-- ============================================================
-- Migration: Create dbo.DepartmentEmail
-- Purpose:   Stores the parent / department-level email for a
--            Company + Department combination.
--
--            The branch-level (child) email lives in
--            dbo.DepartmentAccount.EmailAddressId.
--
--            Effective email resolution:
--              1. Use BranchEmail if set.
--              2. Fall back to DepartmentEmail if not.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID(N'dbo.DepartmentEmail')
      AND type = N'U'
)
BEGIN
    CREATE TABLE dbo.DepartmentEmail (
        [Id]             INT            IDENTITY(1,1) NOT NULL,
        [CompanyName]    NVARCHAR(200)  NOT NULL,
        [DepartmentName] NVARCHAR(200)  NOT NULL,
        [EmailAddressId] INT            NULL,

        CONSTRAINT [PK_DepartmentEmail]
            PRIMARY KEY CLUSTERED ([Id] ASC),

        CONSTRAINT [UQ_DepartmentEmail_CompanyDept]
            UNIQUE ([CompanyName], [DepartmentName]),

        CONSTRAINT [FK_DepartmentEmail_EmailAddress]
            FOREIGN KEY ([EmailAddressId]) REFERENCES dbo.[EmailAddress] ([EmailId])
    );

    PRINT 'Table dbo.DepartmentEmail created.';
END
ELSE
    PRINT 'Table dbo.DepartmentEmail already exists. Skipping.';
GO
