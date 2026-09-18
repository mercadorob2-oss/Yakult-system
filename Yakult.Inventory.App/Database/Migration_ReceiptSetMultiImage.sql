-- =====================================================================
-- Migration: Multi-image support for Receipt Set SI/DR/PO documents
-- =====================================================================
-- Adds dbo.ReceiptSetDocumentImage, a one-to-many table for storing
-- multiple images per document type (SI/DR/PO) per receipt set.
--
-- Backward compatible: the existing single-image columns on
-- dbo.ReceiptSet (SiImage/SiImagePath, DrImage/DrImagePath,
-- PoImage/PoImagePath) are kept as-is and are NOT dropped. Existing
-- single images are migrated into the new table as the first image
-- (SortOrder = 0) for their document type, so no data is lost and
-- older code paths reading the legacy columns keep working.
-- =====================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo' AND t.name = 'ReceiptSetDocumentImage'
)
BEGIN
    CREATE TABLE dbo.ReceiptSetDocumentImage (
        ImageId        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReceiptSetDocumentImage PRIMARY KEY,
        ReceiptSetId   INT             NOT NULL,
        DocType        VARCHAR(2)      NOT NULL,  -- 'SI', 'DR', 'PO'
        ImagePath      NVARCHAR(500)   NULL,       -- optional original file path reference
        ImageBytes     VARBINARY(MAX)  NULL,       -- stored image content
        SortOrder      INT             NOT NULL DEFAULT 0,
        CreatedAt      DATETIME2       NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedBy      INT             NULL,
        CONSTRAINT FK_ReceiptSetDocumentImage_ReceiptSet
            FOREIGN KEY (ReceiptSetId) REFERENCES dbo.ReceiptSet(ReceiptSetId) ON DELETE CASCADE,
        CONSTRAINT CK_ReceiptSetDocumentImage_DocType
            CHECK (DocType IN ('SI', 'DR', 'PO'))
    );

    CREATE NONCLUSTERED INDEX IX_ReceiptSetDocumentImage_ReceiptSet_DocType
        ON dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, SortOrder);

    PRINT 'Created dbo.ReceiptSetDocumentImage table.';
END
ELSE
BEGIN
    PRINT 'dbo.ReceiptSetDocumentImage table already exists.';
END
GO

-- =====================================================================
-- Backfill: migrate existing single SI/DR/PO images into the new table
-- as SortOrder = 0, only if not already migrated (idempotent).
-- =====================================================================

IF EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ReceiptSetDocumentImage')
BEGIN
    INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImagePath, ImageBytes, SortOrder, CreatedAt, CreatedBy)
    SELECT rs.ReceiptSetId, 'SI', rs.SiImagePath, rs.SiImage, 0, rs.CreatedAt, rs.CreatedBy
    FROM dbo.ReceiptSet rs
    WHERE (rs.SiImage IS NOT NULL OR rs.SiImagePath IS NOT NULL)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ReceiptSetDocumentImage img
          WHERE img.ReceiptSetId = rs.ReceiptSetId AND img.DocType = 'SI'
      );

    INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImagePath, ImageBytes, SortOrder, CreatedAt, CreatedBy)
    SELECT rs.ReceiptSetId, 'DR', rs.DrImagePath, rs.DrImage, 0, rs.CreatedAt, rs.CreatedBy
    FROM dbo.ReceiptSet rs
    WHERE (rs.DrImage IS NOT NULL OR rs.DrImagePath IS NOT NULL)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ReceiptSetDocumentImage img
          WHERE img.ReceiptSetId = rs.ReceiptSetId AND img.DocType = 'DR'
      );

    INSERT INTO dbo.ReceiptSetDocumentImage (ReceiptSetId, DocType, ImagePath, ImageBytes, SortOrder, CreatedAt, CreatedBy)
    SELECT rs.ReceiptSetId, 'PO', rs.PoImagePath, rs.PoImage, 0, rs.CreatedAt, rs.CreatedBy
    FROM dbo.ReceiptSet rs
    WHERE (rs.PoImage IS NOT NULL OR rs.PoImagePath IS NOT NULL)
      AND NOT EXISTS (
          SELECT 1 FROM dbo.ReceiptSetDocumentImage img
          WHERE img.ReceiptSetId = rs.ReceiptSetId AND img.DocType = 'PO'
      );

    PRINT 'Backfilled dbo.ReceiptSetDocumentImage from existing single-image columns.';
END
GO
