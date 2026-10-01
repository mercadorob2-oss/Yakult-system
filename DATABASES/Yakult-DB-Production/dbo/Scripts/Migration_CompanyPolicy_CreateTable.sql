-- Migration: Create CompanyPolicy table
-- Company policies shown on the public Company Policy pages.
-- Managed via Admin dashboard (Company Info admin).

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.CompanyPolicy')
)
BEGIN
    CREATE TABLE dbo.CompanyPolicy (
        PolicyId      INT IDENTITY(1,1) NOT NULL,
        Title         NVARCHAR(180) NOT NULL,
        Slug          NVARCHAR(200) NOT NULL,
        Summary       NVARCHAR(500) NOT NULL DEFAULT (''),
        Body          NVARCHAR(MAX) NOT NULL DEFAULT (''),
        Category      NVARCHAR(100) NOT NULL DEFAULT ('General'),
        EffectiveDate DATE NULL,
        SortOrder     INT NOT NULL DEFAULT (0),
        IsPublished   BIT NOT NULL DEFAULT (0),
        CreatedBy     INT NULL,
        CreatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedBy     INT NULL,
        UpdatedAt     DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_CompanyPolicy PRIMARY KEY (PolicyId),
        CONSTRAINT UQ_CompanyPolicy_Slug UNIQUE (Slug)
    );
END
