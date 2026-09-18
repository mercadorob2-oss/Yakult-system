/* =====================================================================================
   Cleanup: Remove orphaned ITCM SMTP sprawl (branches/depts are recipients-only;
   the single global sender lives in Setup / dbo.CallEmailSettings).
   - Deletes the one-off Valenzuela test override (exact-match guard: deletes
     ONLY BranchId 112 with that exact address, nothing else).
   - Deletes CallSmtpProfile rows that have zero branch/dept links.
   - Deletes gmail test rows from CallEmailSettings (keeps corporate senders).
   - All deletes are guarded and followed by verification counts.
   - Scope: ITCM call monitoring only. Re-run safe (deletes zero rows).
   ===================================================================================== */
SET NOCOUNT ON;

-- 1) One-off test override (exact match only)
IF OBJECT_ID('dbo.CallBranchNotificationRecipient', 'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.CallBranchNotificationRecipient
    WHERE BranchId = 112
      AND RecipientEmails = 'tyronerussel77@gmail.com';
END

-- 2) Orphaned SMTP profiles (no branch or dept link references them)
IF OBJECT_ID('dbo.CallSmtpProfile', 'U') IS NOT NULL
BEGIN
    DELETE p
    FROM dbo.CallSmtpProfile p
    WHERE NOT EXISTS (
        SELECT 1 FROM dbo.CallBranchSmtpProfileLink l WHERE l.ProfileId = p.ProfileId
    )
    AND NOT EXISTS (
        SELECT 1 FROM dbo.CallDepartmentSmtpProfileLink l WHERE l.ProfileId = p.ProfileId
    );
END

-- 3) Gmail test sender rows (keeps corporate senders)
IF OBJECT_ID('dbo.CallEmailSettings', 'U') IS NOT NULL
BEGIN
    DELETE FROM dbo.CallEmailSettings
    WHERE SmtpServer = 'smtp.gmail.com';
END

SELECT 'ValenzuelaOverride_rows:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallBranchNotificationRecipient WHERE BranchId = 112;
SELECT 'CallSmtpProfile_rows:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallSmtpProfile;
SELECT 'CallEmailSettings_rows:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallEmailSettings;
