-- ============================================================
-- Migration: dbo.SetItem — add line-level renewal categorization
--
-- Prototype/testing migration. Contract / subscription / license / service
-- identifiers are metadata of a specific invoice or renewal line, not of the
-- master Item. Both columns are nullable so existing workflows are unchanged.
-- Safe to run repeatedly.
-- ============================================================

IF COL_LENGTH('dbo.SetItem', 'RenewalSubcategory') IS NULL
BEGIN
    ALTER TABLE dbo.SetItem
        ADD RenewalSubcategory NVARCHAR(50) NULL;
    PRINT 'dbo.SetItem.RenewalSubcategory added.';
END
ELSE
BEGIN
    PRINT 'dbo.SetItem.RenewalSubcategory already exists — skipped.';
END
GO

IF COL_LENGTH('dbo.SetItem', 'RenewalIdentifier') IS NULL
BEGIN
    ALTER TABLE dbo.SetItem
        ADD RenewalIdentifier NVARCHAR(100) NULL;
    PRINT 'dbo.SetItem.RenewalIdentifier added.';
END
ELSE
BEGIN
    PRINT 'dbo.SetItem.RenewalIdentifier already exists — skipped.';
END
GO
