-- Migration: Backfill Inventory System "Activity" rows written before source-labelling /
--            SET_CREATED existed.
--
-- Pre-existing dbo.Notification rows of type ITEM_ADDED (InventorySystem portal) whose item
-- was created as part of a Set / Invoice are COLLAPSED into a single SET_CREATED row:
--   ReferenceId  -> the SetId
--   DetailsJson  -> {"source":"Request Set" | "Invoice" | "Set"}
--   Title / Message rewritten; the sibling ITEM_ADDED rows for that same Set are deleted.
--
-- ITEM_ADDED rows whose item is NOT in any Set (plain Add Item / Batch Add Items) are left
-- untouched — they are genuine item adds, not Set creations.
--
-- Idempotent: after the first run the collapsed rows are SET_CREATED and no longer match.
--
-- Prerequisites: Migration_Notification_AddActorUserId.sql (ActorUserId + DetailsJson columns)
-- and an app build that understands SET_CREATED. Pure relational SQL — no JSON functions,
-- so no minimum compatibility level.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns
               WHERE object_id = OBJECT_ID('dbo.[Notification]') AND name = 'DetailsJson')
BEGIN
    RAISERROR('dbo.Notification.DetailsJson is missing - run Migration_Notification_AddActorUserId.sql first.', 16, 1);
    RETURN;
END
GO

BEGIN TRANSACTION;

-- ── 1. Map each single-item ITEM_ADDED row to its originating Set ──────────
IF OBJECT_ID('tempdb..#Map') IS NOT NULL DROP TABLE #Map;

SELECT
    n.NotificationId,
    n.UserId,
    n.ActorUserId,
    x.SetId,
    CASE
        WHEN x.IsInvoice = 1 THEN 'Invoice'
        WHEN EXISTS (SELECT 1 FROM dbo.Request r WHERE r.SetId = x.SetId) THEN 'Request Set'
        ELSE 'Set'
    END AS Kind
INTO #Map
FROM dbo.[Notification] n
JOIN dbo.Portal p ON p.PortalId = n.PortalId AND p.PortalKey = 'InventorySystem'
CROSS APPLY
(
    -- The item can reach its Set two ways: directly via dbo.SetItem (Invoice / dispatch sets)
    -- or via its dbo.Request row (Request Sets bundle Requests, not Items).
    SELECT TOP 1 s.SetId, s.IsInvoice
    FROM dbo.[Set] s
    WHERE s.SetId IN (
              SELECT si.SetId FROM dbo.SetItem si WHERE si.ItemId = n.ReferenceId
              UNION
              SELECT r.SetId  FROM dbo.Request  r WHERE r.ItemId  = n.ReferenceId AND r.SetId IS NOT NULL
          )
    ORDER BY s.SetId ASC          -- earliest set the item was bundled into = its creation set
) x
WHERE n.NotificationType = 'ITEM_ADDED'
  AND n.ReferenceId IS NOT NULL;

PRINT CONCAT('Mapped ', @@ROWCOUNT, ' ITEM_ADDED row(s) to a Set.');

-- ── 2. One keeper per (UserId, SetId); rewrite it, delete the rest ────────
IF OBJECT_ID('tempdb..#Groups') IS NOT NULL DROP TABLE #Groups;

SELECT
    m.UserId,
    m.SetId,
    MIN(m.Kind)          AS Kind,
    MIN(m.ActorUserId)   AS ActorUserId,
    MIN(m.NotificationId) AS KeepId,
    COUNT(*)             AS ItemCount,
    CONVERT(BIT, CASE WHEN EXISTS (
        SELECT 1 FROM dbo.[Notification] sc
        WHERE sc.NotificationType = 'SET_CREATED'
          AND sc.ReferenceId = m.SetId
          AND sc.UserId = m.UserId
    ) THEN 1 ELSE 0 END) AS AlreadyHasSetRow
INTO #Groups
FROM #Map m
GROUP BY m.UserId, m.SetId;

-- 2a. Convert the keeper (only when no SET_CREATED row exists yet for that user + set).
UPDATE n
SET n.NotificationType = 'SET_CREATED',
    n.ReferenceId       = g.SetId,
    n.Title             = CONCAT(g.Kind, ' created'),
    n.Message           = CONCAT(
                              COALESCE(u.Name, 'Someone'), ' created ',
                              CASE WHEN LEFT(g.Kind, 1) IN ('A','E','I','O','U','a','e','i','o','u')
                                   THEN 'an ' ELSE 'a ' END,
                              g.Kind, ' with ', g.ItemCount, ' item(s).'),
    n.DetailsJson       = CONCAT(N'{"source":"', g.Kind, N'"}')
FROM dbo.[Notification] n
JOIN #Groups g ON g.KeepId = n.NotificationId AND g.AlreadyHasSetRow = 0
LEFT JOIN dbo.[User] u ON u.UserId = n.ActorUserId;

PRINT CONCAT('Converted ', @@ROWCOUNT, ' row(s) to SET_CREATED.');

-- 2b. Delete the other mapped ITEM_ADDED rows (and the keeper too when a
--     SET_CREATED row for that user + set already existed).
DELETE n
FROM dbo.[Notification] n
JOIN #Map m ON m.NotificationId = n.NotificationId
LEFT JOIN #Groups g ON g.KeepId = n.NotificationId
WHERE n.NotificationType = 'ITEM_ADDED'
   OR (g.KeepId IS NOT NULL AND g.AlreadyHasSetRow = 1);

PRINT CONCAT('Removed ', @@ROWCOUNT, ' now-redundant ITEM_ADDED row(s).');

DROP TABLE #Map;
DROP TABLE #Groups;

COMMIT TRANSACTION;
GO

PRINT 'Migration_Notification_BackfillActivitySources complete.';
GO
