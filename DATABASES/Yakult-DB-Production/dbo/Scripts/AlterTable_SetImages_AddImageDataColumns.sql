-- Moves Set image storage from local disk (ImagePath) to the database (ImageData).
-- New uploads populate ImageData/MimeType/OriginalFileName/FileSizeBytes and leave
-- ImagePath NULL. Existing rows keep their ImagePath so legacy files on disk still work.

IF COL_LENGTH('dbo.SetImages', 'ImageData') IS NULL
BEGIN
    ALTER TABLE dbo.SetImages
        ADD ImageData VARBINARY(MAX) NULL;
END
GO

IF COL_LENGTH('dbo.SetImages', 'MimeType') IS NULL
BEGIN
    ALTER TABLE dbo.SetImages
        ADD MimeType NVARCHAR(50) NULL;
END
GO

IF COL_LENGTH('dbo.SetImages', 'OriginalFileName') IS NULL
BEGIN
    ALTER TABLE dbo.SetImages
        ADD OriginalFileName NVARCHAR(260) NULL;
END
GO

IF COL_LENGTH('dbo.SetImages', 'FileSizeBytes') IS NULL
BEGIN
    ALTER TABLE dbo.SetImages
        ADD FileSizeBytes INT NULL;
END
GO

-- ImagePath was NOT NULL; new DB-stored rows won't have one, so relax it.
IF EXISTS (
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SetImages')
      AND name = 'ImagePath'
      AND is_nullable = 0
)
BEGIN
    ALTER TABLE dbo.SetImages
        ALTER COLUMN ImagePath VARCHAR(500) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.check_constraints
    WHERE name = 'CK_SetImages_StorageNotEmpty'
      AND parent_object_id = OBJECT_ID('dbo.SetImages')
)
BEGIN
    ALTER TABLE dbo.SetImages WITH CHECK
        ADD CONSTRAINT CK_SetImages_StorageNotEmpty
            CHECK (ImagePath IS NOT NULL OR ImageData IS NOT NULL);
END
GO
