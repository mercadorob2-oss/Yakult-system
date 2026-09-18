-- =============================================================================
-- Migration: Update trg_User_PrivilegeFlagsAudit to stop pushing Notification rows
-- Date: 2026-08-20
--
-- Purpose:
--   trg_User_PrivilegeFlagsAudit (see Migration_User_CreateTrigger_PrivilegeFlagsAudit.sql)
--   still logs every IsDeveloper/IsSuperAdmin change to dbo.UserActivityLog, but no
--   longer inserts into dbo.[Notification] — the bell notifications in the Request
--   Portal for "User privilege changed" were not requested and are being removed.
--   The audit trail (detective control) is preserved; only the real-time in-app
--   alert is dropped.
-- =============================================================================

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

    -- Rows whose IsDeveloper or IsSuperAdmin value actually changed
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

    -- Audit trail entry per affected user (kept)
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

    -- In-app Notification insert removed (was surfacing "User privilege changed"
    -- toasts in the Request Portal bell dropdown, which were not wanted).

    DROP TABLE #PrivChanges;
END;
GO

PRINT 'Trigger dbo.trg_User_PrivilegeFlagsAudit updated: Notification insert removed, audit log retained.';
GO
