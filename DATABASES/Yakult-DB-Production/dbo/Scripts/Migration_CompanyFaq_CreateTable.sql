-- Migration: Create CompanyFaq table
-- Company FAQs shown on the public FAQ page (nav, next to Company).
-- Managed via Admin dashboard (Company Info admin).

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.CompanyFaq')
)
BEGIN
    CREATE TABLE dbo.CompanyFaq (
        FaqId       INT IDENTITY(1,1) NOT NULL,
        Question    NVARCHAR(300) NOT NULL,
        Answer      NVARCHAR(MAX) NOT NULL,
        Category    NVARCHAR(100) NOT NULL DEFAULT ('General'),
        SortOrder   INT NOT NULL DEFAULT (0),
        IsPublished BIT NOT NULL DEFAULT (0),
        CreatedBy   INT NULL,
        CreatedAt   DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        UpdatedBy   INT NULL,
        UpdatedAt   DATETIME2 NOT NULL DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT PK_CompanyFaq PRIMARY KEY (FaqId)
    );
END
