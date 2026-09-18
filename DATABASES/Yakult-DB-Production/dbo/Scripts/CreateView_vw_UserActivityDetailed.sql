-- ============================================================
-- CREATE OR ALTER: dbo.vw_UserActivityDetailed
-- Safe to run on a fresh database (no view) or to re-apply.
-- Source: dbo.UserActivityLog only — no UNIONs, no synthetic rows.
-- ============================================================

GO

CREATE OR ALTER VIEW [dbo].[vw_UserActivityDetailed]
AS
SELECT
    a.ActivityId,
    a.UserId,
    u.Name            AS UserName,
    u.EmailAddress,
    a.ActionType,
    a.EntityType,
    a.EntityId,
    a.Description,
    a.CreatedDate     AS ActivityDate,
    'UserActivityLog' AS Source
FROM dbo.UserActivityLog a
LEFT JOIN dbo.[User] u ON a.UserId = u.UserId;

GO

PRINT 'dbo.vw_UserActivityDetailed created/updated.';
