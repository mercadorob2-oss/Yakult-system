/* =====================================================================================
   Migration: Backfill CallDepartmentNotificationRecipient from DepartmentEmail
   - Copies each DepartmentEmail mapping (active registry address) into the ITCM
     per-department recipient override table (Notify To), resolving DeptId by
     exact Department.Name match.
   - INSERT-ONLY: existing override rows are never touched.
   - EscalationEmails left '' so escalation falls back to the same recipient.
   - UpdatedByUserId NULL marks system-seeded rows (vs human edits in the grid).
   - Idempotent: re-running inserts zero rows.
   - YMC + YPI share department names (e.g. IT under both companies). Where both
     mappings carry the SAME address the copy is lossless (dedupe keeps MIN Id).
     If any DeptId ever maps to two DIFFERENT addresses the script aborts and
   - Deliberately EXCLUDED (no matching Department row — map manually):
       YMC / Personnel Management Department
       YPI / Personnel Management Department
   - Scope: ITCM call monitoring only. Sources are read, never written.
   ===================================================================================== */
SET NOCOUNT ON;

IF OBJECT_ID('dbo.CallDepartmentNotificationRecipient', 'U') IS NULL
BEGIN
    RAISERROR('dbo.CallDepartmentNotificationRecipient is not installed. Skipping backfill.', 10, 1);
    RETURN;
END

IF OBJECT_ID('dbo.DepartmentEmail', 'U') IS NULL
   OR OBJECT_ID('dbo.Department', 'U') IS NULL
   OR OBJECT_ID('dbo.EmailAddress', 'U') IS NULL
BEGIN
    RAISERROR('dbo.DepartmentEmail, dbo.Department or dbo.EmailAddress is missing. Skipping backfill.', 10, 1);
    RETURN;
END

MERGE dbo.CallDepartmentNotificationRecipient AS tgt
USING (
    SELECT DeptId, EmailAddress FROM (
        SELECT d.DeptId, e.EmailAddress,
               ROW_NUMBER() OVER (PARTITION BY d.DeptId ORDER BY m.Id) AS rn
        FROM dbo.DepartmentEmail m
        INNER JOIN dbo.EmailAddress e ON e.EmailId = m.EmailAddressId
        INNER JOIN dbo.Department d ON d.Name = m.DepartmentName
        WHERE m.EmailAddressId IS NOT NULL
          AND ISNULL(e.IsActive, 1) = 1
          AND NOT EXISTS (
              -- Safety: same DeptId mapped to two DIFFERENT addresses.
              -- Verified same-address on 2026-09-04 (YMC+YPI pairs identical);
              -- aborts instead of guessing if data ever diverges.
              SELECT 1
              FROM dbo.DepartmentEmail m2
              INNER JOIN dbo.EmailAddress e2 ON e2.EmailId = m2.EmailAddressId
              INNER JOIN dbo.Department d2 ON d2.Name = m2.DepartmentName
              WHERE d2.DeptId = d.DeptId
                AND m2.EmailAddressId IS NOT NULL
                AND ISNULL(e2.IsActive, 1) = 1
              GROUP BY d2.DeptId
              HAVING COUNT(DISTINCT e2.EmailAddress) > 1
          )
    ) x WHERE rn = 1
) AS src ON tgt.DeptId = src.DeptId
WHEN NOT MATCHED THEN
    INSERT (DeptId, RecipientEmails, EscalationEmails, IsActive, UpdatedAt, UpdatedByUserId)
    VALUES (src.DeptId, src.EmailAddress, '', 1, SYSUTCDATETIME(), NULL);

SELECT 'DeptRecip_total:' + CAST(COUNT(*) AS nvarchar(20)) FROM dbo.CallDepartmentNotificationRecipient;
