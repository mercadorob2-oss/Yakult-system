-- =====================================================================
-- Migration: Widen ReceiptSetDocumentImage.DocType to support 'PDF'
-- =====================================================================
-- Background:
--   Migration_ReceiptSetMultiImage.sql originally created DocType as
--   VARCHAR(2) with a CHECK constraint limited to ('SI','DR','PO').
--   The WPF Receipt Set Viewer was later extended with a 4th document
--   type, "PDF" (Wpf\Receipt\ReceiptSetViewerWindow.xaml.cs), but the
--   database column/constraint were never updated to match. Saving a
--   PDF attachment now fails with:
--     "String or binary data would be truncated in table
--      'YIMS_PROD.dbo.ReceiptSetDocumentImage', column 'DocType'."
--
-- Fix:
--   Widen DocType to VARCHAR(10) (room for future doc types without
--   another truncation issue) and replace the CHECK constraint to also
--   allow 'PDF'. Idempotent / safe to run repeatedly.
-- =====================================================================

IF EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ReceiptSetDocumentImage'
)
BEGIN
    -- Drop the old CHECK constraint (name may vary; look it up dynamically
    -- in case it was created with a system-generated name in some environments).
    DECLARE @ConstraintName SYSNAME;

    SELECT @ConstraintName = cc.name
    FROM sys.check_constraints cc
    JOIN sys.tables t ON t.object_id = cc.parent_object_id
    WHERE t.name = 'ReceiptSetDocumentImage'
      AND cc.definition LIKE '%DocType%';

    IF @ConstraintName IS NOT NULL
    BEGIN
        EXEC('ALTER TABLE dbo.ReceiptSetDocumentImage DROP CONSTRAINT [' + @ConstraintName + ']');
        PRINT 'Dropped existing DocType CHECK constraint: ' + @ConstraintName;
    END

    -- Widen the column only if it isn't already wide enough.
    IF EXISTS (
        SELECT 1 FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id
        WHERE t.name = 'ReceiptSetDocumentImage'
          AND c.name = 'DocType'
          AND c.max_length < 10
    )
    BEGIN
        ALTER TABLE dbo.ReceiptSetDocumentImage ALTER COLUMN DocType VARCHAR(10) NOT NULL;
        PRINT 'Widened ReceiptSetDocumentImage.DocType to VARCHAR(10).';
    END

    -- Re-add the CHECK constraint including 'PDF'.
    IF NOT EXISTS (
        SELECT 1 FROM sys.check_constraints cc
        JOIN sys.tables t ON t.object_id = cc.parent_object_id
        WHERE t.name = 'ReceiptSetDocumentImage'
          AND cc.name = 'CK_ReceiptSetDocumentImage_DocType'
    )
    BEGIN
        ALTER TABLE dbo.ReceiptSetDocumentImage
            ADD CONSTRAINT CK_ReceiptSetDocumentImage_DocType
            CHECK (DocType IN ('SI', 'DR', 'PO', 'PDF'));
        PRINT 'Added updated DocType CHECK constraint (SI, DR, PO, PDF).';
    END
END
ELSE
BEGIN
    PRINT 'dbo.ReceiptSetDocumentImage table does not exist; nothing to migrate.';
END
GO
