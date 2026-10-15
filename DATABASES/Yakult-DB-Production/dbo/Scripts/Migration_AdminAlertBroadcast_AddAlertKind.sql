-- Migration: Add dbo.AdminAlertBroadcast.AlertKind (prank templates)
-- Date: 2026-10-14
-- Purpose: Makes the STOP action modular. Each broadcast names one template:
--          StopSound (STOP dialog + alarm), StopSilent (STOP dialog, no
--          sound), Bsod (fake fullscreen blue screen), Maintenance
--          (maintenance notice dialog). Older rows behave as StopSound.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.AdminAlertBroadcast') AND name = 'AlertKind'
)
BEGIN
    ALTER TABLE dbo.AdminAlertBroadcast
        ADD AlertKind NVARCHAR(30) NOT NULL DEFAULT ('StopSound');

    PRINT 'Column dbo.AdminAlertBroadcast.AlertKind added.';
END
ELSE
BEGIN
    PRINT 'Column dbo.AdminAlertBroadcast.AlertKind already exists. Skipped.';
END
GO
