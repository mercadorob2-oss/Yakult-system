-- Migration: Backfill Inventory System "Activity" rows for requests / renewals that were
--            created BEFORE the REQUEST_CREATED / RENEWAL_CREATED hooks were deployed.
--
-- Synthesises one dbo.Notification row per (audience user) for each recent dbo.Request and
-- dbo.Renewals that has no matching Activity notification yet. Fan-out audience = every active
-- user with InventorySystem portal access (role via dbo.UserRole -> dbo.RolePortalAccess, OR an
-- explicit dbo.UserPortalAccess grant; IsGranted = 0 denies). The actor's own copy is written
-- read (IsRead = 1), matching NotificationRepository.CreateForPortalAudience.
--
-- CreatedDate is stamped with the ENTITY's real creation time (converted to UTC), NOT "now" —
-- so a backfilled renewal from weeks ago sorts and filters as weeks ago, not today. A second
-- pass repairs the CreatedDate of rows an earlier run of this script mis-stamped as "now".
--
-- Scope is time-boxed by @SinceUtc (default: last 7 days) — EDIT it before running to widen /
-- narrow. Idempotent: rows that already have their Activity notification are skipped (their
-- CreatedDate is still corrected).
--
-- Prerequisites: Migration_Notification_AddActorUserId.sql, Migration_Portal_CreateAndSeed.sql.

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

DECLARE @SinceUtc DATETIME2 = DATEADD(DAY, -7, SYSUTCDATETIME());   -- <-- adjust as needed

-- Server local-to-UTC offset (assumed stable across the backfill window; DST edges may be off by 1h).
DECLARE @OffsetMinutes INT = DATEDIFF(MINUTE, SYSUTCDATETIME(), SYSDATETIME());

DECLARE @InvPortalId INT = (SELECT PortalId FROM dbo.Portal WHERE PortalKey = 'InventorySystem');
IF @InvPortalId IS NULL
BEGIN
    RAISERROR('InventorySystem portal row not found - run Migration_Portal_CreateAndSeed.sql first.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- ── 0. Remove bogus RENEWAL_CREATED rows ─────────────────────────────────
-- dbo.Item add ALWAYS writes a baseline dbo.Renewals row (RenewalStatus 'Active',
-- RenewalCount 1) — and the table DEFAULTs NewStartDate / NewEndDate to "now", so it looks
-- like a real renewal to a naive scan. A real renewal is RenewalCount > 1 (it increments past
-- the baseline) or RenewalStatus 'Renewed'. Delete notifications whose item has neither.
DELETE n
FROM dbo.[Notification] n
WHERE n.NotificationType = 'RENEWAL_CREATED'
  AND NOT EXISTS (
        SELECT 1 FROM dbo.Renewals x
        WHERE x.ItemId = n.ReferenceId
          AND (x.RenewalCount > 1 OR x.RenewalStatus = 'Renewed'));

PRINT CONCAT('Removed bogus RENEWAL_CREATED rows (item-add baseline): ', @@ROWCOUNT, '.');

-- Requests that belong to a Set are represented by the Set's SET_CREATED ("Request Set") row,
-- not their own REQUEST_CREATED row — drop any a prior run created.
DELETE n
FROM dbo.[Notification] n
JOIN dbo.Request r ON r.ReqId = n.ReferenceId
WHERE n.NotificationType = 'REQUEST_CREATED' AND r.SetId IS NOT NULL;

PRINT CONCAT('Removed REQUEST_CREATED rows for set-bundled requests: ', @@ROWCOUNT, '.');

-- ── Audience: active users who can see the InventorySystem Activity feed ──
IF OBJECT_ID('tempdb..#Aud') IS NOT NULL DROP TABLE #Aud;
SELECT u.UserId
INTO #Aud
FROM dbo.[User] u
WHERE u.IsActive = 1
  AND (
        EXISTS (SELECT 1 FROM dbo.UserPortalAccess upa
                WHERE upa.UserId = u.UserId AND upa.PortalId = @InvPortalId AND upa.IsGranted = 1)
        OR (
            NOT EXISTS (SELECT 1 FROM dbo.UserPortalAccess upa
                        WHERE upa.UserId = u.UserId AND upa.PortalId = @InvPortalId AND upa.IsGranted = 0)
            AND EXISTS (SELECT 1 FROM dbo.UserRole ur
                        JOIN dbo.RolePortalAccess rpa ON rpa.RoleId = ur.RoleId
                        WHERE ur.UserId = u.UserId AND rpa.PortalId = @InvPortalId)
          )
      );

-- ── 1. REQUEST_CREATED ──────────────────────────────────────────────────
;WITH src AS
(
    SELECT
        r.ReqId,
        r.CreatedBy AS ActorUserId,
        DATEADD(MINUTE, -@OffsetMinutes, r.DateCreated) AS CreatedUtc,
        CONCAT(
            COALESCE(actor.Name, 'Someone'), ' created a request for ',
            CASE WHEN i.Name IS NULL THEN 'an item' ELSE CONCAT('"', i.Name, '"') END,
            CASE WHEN r.Quantity > 1 THEN CONCAT(N' ×', r.Quantity) ELSE '' END,
            '.'
        ) AS Msg
    FROM dbo.Request r
    LEFT JOIN dbo.Item   i     ON i.ItemId  = r.ItemId
    LEFT JOIN dbo.[User] actor ON actor.UserId = r.CreatedBy
    WHERE r.DateCreated >= @SinceUtc
      AND r.CreatedBy IS NOT NULL
      AND r.SetId IS NULL   -- set-bundled requests are covered by the Set's SET_CREATED row
      AND (r.Description IS NULL OR r.Description NOT LIKE '%[[]PORTAL]%')
      AND NOT EXISTS (
            SELECT 1 FROM dbo.[Notification] n
            WHERE n.NotificationType = 'REQUEST_CREATED' AND n.ReferenceId = r.ReqId)
)
INSERT INTO dbo.[Notification]
    (UserId, Title, Message, NotificationType, ReferenceId, ActorUserId, IsRead, PortalId, CreatedDate)
SELECT
    a.UserId, 'Request created', s.Msg, 'REQUEST_CREATED', s.ReqId, s.ActorUserId,
    CASE WHEN a.UserId = s.ActorUserId THEN 1 ELSE 0 END,
    @InvPortalId, s.CreatedUtc
FROM src s
CROSS JOIN #Aud a;

PRINT CONCAT('Inserted REQUEST_CREATED rows: ', @@ROWCOUNT, ' (audience x requests).');

-- ── 2. RENEWAL_CREATED ──────────────────────────────────────────────────
-- One notification per (item, actor) — collapse multiple Renewals rows for the same item in
-- the window (a "Renew All" run) into a single Activity row, keyed on the item.
;WITH rn AS
(
    SELECT
        x.ItemId,
        x.CreatedBy       AS ActorUserId,
        MAX(x.NewEndDate) AS NewEndDate,
        MAX(x.CreatedAt)  AS CreatedLocal
    FROM dbo.Renewals x
    WHERE x.CreatedAt >= @SinceUtc
      AND x.CreatedBy IS NOT NULL
      AND (x.RenewalCount > 1 OR x.RenewalStatus = 'Renewed')   -- exclude the item-add baseline row
    GROUP BY x.ItemId, x.CreatedBy
),
src AS
(
    SELECT
        rn.ItemId,
        rn.ActorUserId,
        DATEADD(MINUTE, -@OffsetMinutes, rn.CreatedLocal) AS CreatedUtc,
        CONCAT(
            COALESCE(actor.Name, 'Someone'), ' renewed ',
            CASE WHEN i.Name IS NULL THEN 'an item' ELSE CONCAT('"', i.Name, '"') END,
            CASE WHEN rn.NewEndDate IS NOT NULL
                 THEN CONCAT(' — new end date ', CONVERT(CHAR(10), rn.NewEndDate, 23))
                 ELSE '' END,
            '.'
        ) AS Msg
    FROM rn
    LEFT JOIN dbo.Item   i     ON i.ItemId    = rn.ItemId
    LEFT JOIN dbo.[User] actor ON actor.UserId = rn.ActorUserId
    WHERE NOT EXISTS (
            SELECT 1 FROM dbo.[Notification] n
            WHERE n.NotificationType = 'RENEWAL_CREATED' AND n.ReferenceId = rn.ItemId)
)
INSERT INTO dbo.[Notification]
    (UserId, Title, Message, NotificationType, ReferenceId, ActorUserId, IsRead, PortalId, CreatedDate)
SELECT
    a.UserId, 'Renewal created', s.Msg, 'RENEWAL_CREATED', s.ItemId, s.ActorUserId,
    CASE WHEN a.UserId = s.ActorUserId THEN 1 ELSE 0 END,
    @InvPortalId, s.CreatedUtc
FROM src s
CROSS JOIN #Aud a;

PRINT CONCAT('Inserted RENEWAL_CREATED rows: ', @@ROWCOUNT, ' (audience x items renewed).');

-- ── 3. Repair CreatedDate on rows a previous run stamped as "now" ────────
-- Only touches rows more than 1 hour off their entity's real (UTC) creation time, so live
-- rows written by the app (already correct to the second) are left alone.
;WITH tgt AS
(
    SELECT 'REQUEST_CREATED' AS T, r.ReqId AS RefId,
           DATEADD(MINUTE, -@OffsetMinutes, r.DateCreated) AS CreatedUtc
    FROM dbo.Request r
    UNION ALL
    SELECT 'RENEWAL_CREATED', x.ItemId,
           DATEADD(MINUTE, -@OffsetMinutes, MAX(x.CreatedAt))
    FROM dbo.Renewals x
    WHERE (x.RenewalCount > 1 OR x.RenewalStatus = 'Renewed')
    GROUP BY x.ItemId
)
UPDATE n
SET n.CreatedDate = tgt.CreatedUtc
FROM dbo.[Notification] n
JOIN tgt ON tgt.T = n.NotificationType AND tgt.RefId = n.ReferenceId
WHERE n.NotificationType IN ('REQUEST_CREATED', 'RENEWAL_CREATED')
  AND ABS(DATEDIFF(MINUTE, n.CreatedDate, tgt.CreatedUtc)) > 60;

PRINT CONCAT('Corrected CreatedDate on ', @@ROWCOUNT, ' mis-stamped row(s).');

DROP TABLE #Aud;
COMMIT TRANSACTION;
GO

PRINT 'Migration_Notification_BackfillRequestRenewalActivity complete.';
GO
