-- Adds a MimeType column to dbo.ReceiptSetDocumentImage so receipt pages can be stored as
-- either an image (image/jpeg) or a combined multi-page PDF (application/pdf), mirroring the
-- MimeType column dbo.SetImages already has. NULL/existing rows are treated as image/jpeg by
-- readers (the legacy assumption before this column existed).

IF COL_LENGTH('dbo.ReceiptSetDocumentImage', 'MimeType') IS NULL
BEGIN
    ALTER TABLE dbo.ReceiptSetDocumentImage
        ADD MimeType NVARCHAR(50) NULL;
END
GO
