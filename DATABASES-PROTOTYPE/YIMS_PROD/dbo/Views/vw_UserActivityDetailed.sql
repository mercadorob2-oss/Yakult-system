
CREATE   VIEW [dbo].[vw_UserActivityDetailed]
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

