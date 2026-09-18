/* =====================================================================================
   Migration: Backfill CallBranchNotificationRecipient from Branch.EmailId
   - Copies each active branch's registry email (Branch.EmailId -> EmailAddress)
     into the ITCM per-branch recipient override table (Notify To).
   - INSERT-ONLY: existing override rows are never touched.
   - EscalationEmails left '' so escalation falls back to the same recipient.
   - UpdatedByUserId NULL marks system-seeded rows (vs human edits in the grid).
   - Idempotent: re-running inserts zero rows.
   - Scope: ITCM call monitoring only. Sources are read, never written.
   ===================================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.CallBranchNotificationRecipient', 'U') IS NULL
BEGIN
    RAISERROR('dbo.CallBranchNotificationRecipient is not installed. Skipping backfill.', 10, 1);
    RETURN;
END

IF OBJECT_ID('dbo.Branch', 'U') IS NULL OR OBJECT_ID('dbo.EmailAddress', 'U') IS NULL
BEGIN
    RAISERROR('dbo.Branch or dbo.EmailAddress is missing. Skipping backfill.', 10, 1);
    RETURN;
END

MERGE dbo.CallBranchNotificationRecipient AS tgt
USING (
    SELECT b.BranchId, e.EmailAddress
    FROM dbo.Branch b
    INNER JOIN dbo.EmailAddress e ON e.EmailId = b.EmailId
    WHERE ISNULL(b.Active, 1) = 1
      AND ISNULL(e.IsActive, 1) = 1
) AS src ON tgt.BranchId = src.BranchId
WHEN NOT MATCHED THEN
    INSERT (BranchId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
    VALUES (src.BranchId, src.EmailAddress, '', 1, SYSUTCDATETIME(), NULL);

SELECT 'BranchRecip_total:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallBranchNotificationRecipient;
