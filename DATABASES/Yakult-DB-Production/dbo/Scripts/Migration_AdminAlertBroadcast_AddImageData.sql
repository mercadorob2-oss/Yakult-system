-- Migration: Add custom image payloads to admin alert broadcasts.
-- Image popups remain targeted and expire through the existing alert lifecycle.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.AdminAlertBroadcast') AND name = 'ImageData'
)
BEGIN
    ALTER TABLE dbo.AdminAlertBroadcast ADD ImageData VARBINARY(MAX) NULL;
    PRINT 'Column dbo.AdminAlertBroadcast.ImageData added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.AdminAlertBroadcast.ImageData already exists. Skipped.';
END
GO
