-- Migration: Finalize dbo.Notification.PortalId (step 3 of the PortalId rollout)
--
-- Prerequisites — every writer now stamps PortalId:
--   * Request Portal web  — NotificationRepository.CreateAsync            (step 1)
--   * WinForms desktop app — NotificationRepository.Create + PortalKeyFor (step 2)
--   * This script re-issues the dbo.[User] privilege-audit trigger so its
--     dbo.Notification INSERT also sets PortalId (-> AdminPortal).         (step 2)
--
-- What it does:
--   1. Backfill any dbo.Notification row still missing PortalId.
--   2. CREATE OR ALTER trg_User_PrivilegeFlagsAudit to write PortalId.
--   3. Make PortalId NOT NULL — but ONLY once no NULLs remain, so it is safe
--      to run before every app instance has been upgraded (re-run later to
--      complete). Once this succeeds, the "PortalId IS NULL" fallback branch
--      in the app read filters is dead and can be deleted.
--
-- Idempotent: safe to run repeatedly. Run AFTER Migration_Notification_AddPortalId.sql.

SET NOCOUNT ON;
GO

-- ── 1. Backfill stragglers ─────────────────────────────────────────────────
UPDATE n SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'RequesterPortal') p
WHERE n.PortalId IS NULL
  AND ( n.NotificationType LIKE 'AUTHORIZATION[_]%'
     OR n.NotificationType LIKE 'REQUEST[_]%'
     OR n.NotificationType = 'TEST' );

UPDATE n SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'RepairPortal') p
WHERE n.PortalId IS NULL
  AND n.NotificationType LIKE 'REPAIR[_]TICKET[_]%';

UPDATE n SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'AdminPortal') p
WHERE n.PortalId IS NULL
  AND n.NotificationType = 'SecurityAlert';

-- Catch-all: anything still unmapped goes to the Requester Portal (its bell is the
-- widest; a mis-file there is visible, a NULL is invisible everywhere).
UPDATE n SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'RequesterPortal') p
WHERE n.PortalId IS NULL;

PRINT 'Backfilled remaining dbo.Notification.PortalId values.';
GO

-- ── 2. Trigger: write PortalId on the security-alert Notification INSERT ────
CREATE OR ALTER TRIGGER dbo.trg_User_PrivilegeFlagsAudit
ON dbo.[User]
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT (UPDATE(IsDeveloper) OR UPDATE(IsSuperAdmin))
        RETURN;

    DECLARE @Actor NVARCHAR(128) = SUSER_SNAME();
    DECLARE @ActorNote NVARCHAR(200) =
        CASE WHEN @Actor = 'remote_user'
             THEN CONCAT('via application (login: ', @Actor, ')')
             ELSE CONCAT('directly in the database, bypassing the app (login: ', @Actor, ')')
        END;

    IF OBJECT_ID('tempdb..#PrivChanges') IS NOT NULL DROP TABLE #PrivChanges;

    SELECT
        i.UserId,
        i.Name,
        d.IsDeveloper   AS OldIsDeveloper,
        i.IsDeveloper   AS NewIsDeveloper,
        d.IsSuperAdmin  AS OldIsSuperAdmin,
        i.IsSuperAdmin  AS NewIsSuperAdmin
    INTO #PrivChanges
    FROM inserted i
    INNER JOIN deleted d ON d.UserId = i.UserId
    WHERE d.IsDeveloper  <> i.IsDeveloper
       OR d.IsSuperAdmin <> i.IsSuperAdmin;

    IF NOT EXISTS (SELECT 1 FROM #PrivChanges)
    BEGIN
        DROP TABLE #PrivChanges;
        RETURN;
    END

    -- Audit trail entry per affected user
    INSERT INTO dbo.UserActivityLog (UserId, ActionType, EntityType, EntityId, Description)
    SELECT
        pc.UserId,
        'Update',
        'UserPrivilege',
        pc.UserId,
        CONCAT(
            'Privilege flags changed for ', pc.Name, ': ',
            'IsDeveloper ', pc.OldIsDeveloper, ' -> ', pc.NewIsDeveloper, ', ',
            'IsSuperAdmin ', pc.OldIsSuperAdmin, ' -> ', pc.NewIsSuperAdmin,
            ' (', @ActorNote, ')'
        )
    FROM #PrivChanges pc;

    -- Real-time alert to every active developer/super-admin so a direct
    -- database edit is noticed immediately, not just discoverable in the log.
    -- PortalId -> AdminPortal (this notification belongs to the Admin Portal bell).
    INSERT INTO dbo.[Notification] (UserId, Title, Message, NotificationType, ReferenceId, PortalId)
    SELECT
        recipient.UserId,
        CASE WHEN @Actor = 'remote_user'
             THEN 'User privilege changed'
             ELSE 'SECURITY ALERT: User privilege changed outside the app'
        END,
        CONCAT(
            'Privilege flags changed for ', pc.Name, ': ',
            'IsDeveloper ', pc.OldIsDeveloper, ' -> ', pc.NewIsDeveloper, ', ',
            'IsSuperAdmin ', pc.OldIsSuperAdmin, ' -> ', pc.NewIsSuperAdmin,
            ' (', @ActorNote, ')'
        ),
        'SecurityAlert',
        pc.UserId,
        (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'AdminPortal')
    FROM #PrivChanges pc
    CROSS JOIN dbo.[User] recipient
    WHERE recipient.IsActive = 1
      AND (recipient.IsDeveloper = 1 OR recipient.IsSuperAdmin = 1);

    DROP TABLE #PrivChanges;
END;
GO

PRINT 'Trigger dbo.trg_User_PrivilegeFlagsAudit updated to set PortalId.';
GO

-- ── 3. Make PortalId NOT NULL (only when clean) ────────────────────────────
-- ALTER COLUMN cannot run while an index references the column, so the
-- IX_Notification_UserId_PortalId_CreatedDate index is dropped and rebuilt.
IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID('dbo.[Notification]') AND name = 'PortalId' AND is_nullable = 1)
BEGIN
    IF EXISTS (SELECT 1 FROM dbo.[Notification] WHERE PortalId IS NULL)
    BEGIN
        PRINT 'PortalId still has NULL rows — leaving the column nullable. '
            + 'Re-run this script once every app instance is upgraded.';
    END
    ELSE
    BEGIN
        IF EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = 'IX_Notification_UserId_PortalId_CreatedDate'
                     AND object_id = OBJECT_ID('dbo.[Notification]'))
            DROP INDEX IX_Notification_UserId_PortalId_CreatedDate ON dbo.[Notification];

        ALTER TABLE dbo.[Notification] ALTER COLUMN [PortalId] INT NOT NULL;

        CREATE NONCLUSTERED INDEX IX_Notification_UserId_PortalId_CreatedDate
            ON dbo.[Notification] ([UserId], [PortalId], [CreatedDate] DESC)
            INCLUDE ([IsRead], [NotificationType]);

        PRINT 'dbo.Notification.PortalId is now NOT NULL (index rebuilt). '
            + 'The "PortalId IS NULL" fallback in app read filters is now dead code.';
    END
END
ELSE
BEGIN
    PRINT 'dbo.Notification.PortalId is already NOT NULL — nothing to do.';
END
GO

PRINT 'Migration_Notification_PortalId_Finalize complete.';
GO
