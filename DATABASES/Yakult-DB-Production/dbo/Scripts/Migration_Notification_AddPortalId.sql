-- Migration: Add dbo.Notification.PortalId — explicit owning-portal FK
--
-- Why: dbo.Notification is shared by every portal (Request Portal web, Repair
-- Technician Portal, the WinForms desktop app's security alerts). Until now the
-- ONLY thing marking which portal a row belonged to was a convention on the
-- NotificationType string, and each app filtered its bell with a hardcoded
-- IN-list of type strings. Any new type not added to that list became invisible
-- (and un-clearable) in the bell — e.g. the "TEST" rows.
--
-- PortalId makes ownership explicit and data-driven: an app reads its bell with
--   WHERE PortalId = <its portal>
-- instead of maintaining a type list.
--
-- Transition plan (this script is step 1):
--   1. Add PortalId NULL + FK, seed the RepairPortal row, backfill existing rows
--      from their NotificationType.                                  <-- here
--   2. Every writer (Request Portal, Repair Portal, desktop app) stamps PortalId
--      on INSERT. Request Portal reads with
--          PortalId = @me OR (PortalId IS NULL AND NotificationType IN (...))
--      so not-yet-migrated writers' NULL rows stay visible.
--   3. Once all writers stamp PortalId: backfill any stragglers, make the column
--      NOT NULL, and delete the NotificationType IN-list fallback from app code.
--
-- Idempotent: safe to run repeatedly.

SET NOCOUNT ON;
GO

-- ── 1. Ensure the RepairPortal portal row exists ────────────────────────────
-- (Migration_Portal_CreateAndSeed.sql seeds the others but not this one.)
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.Portal'))
   AND NOT EXISTS (SELECT 1 FROM dbo.Portal WHERE PortalKey = 'RepairPortal')
BEGIN
    INSERT INTO dbo.Portal (PortalKey, DisplayName)
    VALUES ('RepairPortal', 'Repair Technician Portal');
    PRINT 'Seeded dbo.Portal row: RepairPortal.';
END
GO

-- ── 2. Add the column ──────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Notification]') AND name = 'PortalId'
)
BEGIN
    ALTER TABLE dbo.[Notification] ADD [PortalId] INT NULL;
    PRINT 'Added dbo.Notification.PortalId.';
END
GO

-- ── 3. FK to dbo.Portal ────────────────────────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('dbo.Portal'))
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Notification_Portal')
BEGIN
    ALTER TABLE dbo.[Notification] WITH CHECK
        ADD CONSTRAINT FK_Notification_Portal
        FOREIGN KEY ([PortalId]) REFERENCES dbo.Portal([PortalId]);
    PRINT 'Added FK_Notification_Portal.';
END
GO

-- ── 4. Backfill existing rows from NotificationType ────────────────────────
-- RequesterPortal: authorization + request events + the Test Notification button.
UPDATE n
SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'RequesterPortal') p
WHERE n.PortalId IS NULL
  AND ( n.NotificationType LIKE 'AUTHORIZATION[_]%'
     OR n.NotificationType LIKE 'REQUEST[_]%'
     OR n.NotificationType = 'TEST' );

-- RepairPortal: repair ticket events.
UPDATE n
SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'RepairPortal') p
WHERE n.PortalId IS NULL
  AND n.NotificationType LIKE 'REPAIR[_]TICKET[_]%';

-- Security / privilege alerts belong to the Admin Portal.
UPDATE n
SET n.PortalId = p.PortalId
FROM dbo.[Notification] n
CROSS JOIN (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'AdminPortal') p
WHERE n.PortalId IS NULL
  AND n.NotificationType = 'SecurityAlert';

PRINT 'Backfilled dbo.Notification.PortalId from NotificationType.';
GO

-- ── 5. Index for the PortalId-scoped bell read ────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_Notification_UserId_PortalId_CreatedDate'
      AND object_id = OBJECT_ID('dbo.[Notification]')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Notification_UserId_PortalId_CreatedDate
        ON dbo.[Notification] ([UserId], [PortalId], [CreatedDate] DESC)
        INCLUDE ([IsRead], [NotificationType]);
    PRINT 'Created IX_Notification_UserId_PortalId_CreatedDate.';
END
GO

PRINT 'Migration_Notification_AddPortalId complete.';
GO
