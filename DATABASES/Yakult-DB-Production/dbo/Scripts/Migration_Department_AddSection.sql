-- ============================================================
-- Migration: Add Section column to dbo.Department
-- Purpose:   Some departments have sub-sections (e.g. Accounting
--            has sections per distributor company). This column
--            stores the optional section name for a department row.
-- ============================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.Department')
      AND name = 'Section'
)
BEGIN
    ALTER TABLE dbo.Department
        ADD [Section] NVARCHAR(200) NULL;

    PRINT 'Column Section added to dbo.Department.';
END
ELSE
    PRINT 'Column Section already exists. Skipping.';
GO
