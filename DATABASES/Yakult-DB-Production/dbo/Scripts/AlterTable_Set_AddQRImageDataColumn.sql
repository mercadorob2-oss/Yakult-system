-- Moves Set QR code image storage from local disk (QRImagePath) to the database
-- (QRImageData). New QR generations populate QRImageData; QRImagePath is left in
-- place for legacy rows generated before this migration.

IF COL_LENGTH('dbo.[Set]', 'QRImageData') IS NULL
BEGIN
    ALTER TABLE dbo.[Set]
        ADD QRImageData VARBINARY(MAX) NULL;
END
GO
