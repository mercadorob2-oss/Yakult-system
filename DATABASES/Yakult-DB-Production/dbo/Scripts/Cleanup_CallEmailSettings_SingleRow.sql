/* =====================================================================================
   Cleanup: Collapse dbo.CallEmailSettings to a single row.
   - SaveEmailSettingsAsync appends a row per save; the reader takes
     TOP 1 ... ORDER BY SettingsId DESC, so older rows are dormant history.
   - Keeps ONLY the newest row, and only if it is the corporate sender
     (192.168.80.41). Otherwise aborts with an error and deletes nothing.
   - Re-run safe.
   ===================================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.CallEmailSettings', 'U') IS NULL
BEGIN
    RAISERROR('dbo.CallEmailSettings is not installed. Nothing to do.', 10, 1);
    RETURN;
END

DECLARE @KeepId INT = (SELECT MAX(SettingsId) FROM dbo.CallEmailSettings);

IF NOT EXISTS (
    SELECT 1 FROM dbo.CallEmailSettings
    WHERE SettingsId = @KeepId AND SmtpServer = '192.168.80.41'
)
BEGIN
    RAISERROR('Newest CallEmailSettings row is not the corporate sender. Aborting; nothing deleted.', 16, 1);
    RETURN;
END

DELETE FROM dbo.CallEmailSettings WHERE SettingsId <> @KeepId;

SELECT 'CallEmailSettings_rows:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallEmailSettings;
SELECT 'Kept_sender:' + ISNULL(SmtpServer, '(none)') + ' from:' + ISNULL(FromEmail, '(none)') FROM dbo.CallEmailSettings;
