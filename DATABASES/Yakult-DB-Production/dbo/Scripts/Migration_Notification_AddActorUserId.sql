-- Migration: Add dbo.Notification.ActorUserId + DetailsJson — support the Inventory System
--            "Activity" feed (see Wpf\NotificationCenter\ and Services\InventoryActivityNotifier).
--
-- ActorUserId — who performed the action. dbo.Notification rows have always answered
--   "who is this FOR" (UserId) but never "who DID this". Nullable — event types with no
--   actor (SecurityAlert, REQUEST_*, REPAIR_TICKET_*) leave it NULL.
--
-- DetailsJson — optional JSON payload for a notification that summarises several entities
--   (e.g. a batch "added N items" row) so the bell can render an expandable, per-item
--   clickable list. Shape for ITEM_ADDED batches: [{"id":123,"name":"...","type":"..."}].
--   Nullable — single-entity notifications leave it NULL.
--
-- Independent of the PortalId rollout (Migration_Notification_AddPortalId.sql /
-- _Finalize.sql) — can run in any order relative to those.
--
-- Idempotent: safe to run repeatedly.

SET NOCOUNT ON;
GO

-- ── 1. Add the column ─────────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Notification]') AND name = 'ActorUserId'
)
BEGIN
    ALTER TABLE dbo.[Notification] ADD [ActorUserId] INT NULL;
    PRINT 'Added dbo.Notification.ActorUserId.';
END
ELSE
BEGIN
    PRINT 'dbo.Notification.ActorUserId already exists — no changes made.';
END
GO

-- ── 1b. Add the DetailsJson column ───────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Notification]') AND name = 'DetailsJson'
)
BEGIN
    ALTER TABLE dbo.[Notification] ADD [DetailsJson] NVARCHAR(MAX) NULL;
    PRINT 'Added dbo.Notification.DetailsJson.';
END
ELSE
BEGIN
    PRINT 'dbo.Notification.DetailsJson already exists — no changes made.';
END
GO

-- ── 2. FK to dbo.[User] ───────────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Notification_ActorUser'
)
BEGIN
    ALTER TABLE dbo.[Notification] WITH CHECK
        ADD CONSTRAINT FK_Notification_ActorUser
        FOREIGN KEY ([ActorUserId]) REFERENCES dbo.[User]([UserId]);
    PRINT 'Added FK_Notification_ActorUser.';
END
ELSE
BEGIN
    PRINT 'FK_Notification_ActorUser already exists — no changes made.';
END
GO

PRINT 'Migration_Notification_AddActorUserId complete.';
GO
