-- =============================================================================
-- Migration: Assign User.LevelId overrides (OPTIONAL)
-- Date: 2026-04-10
-- Run AFTER:
--   1. Migration_AccountLevel_CreateAndSeed.sql
--   2. Migration_User_AddLevelId.sql
--
-- IMPORTANT — read before running:
--
--   LevelRank is now computed DYNAMICALLY at login and at approval time from
--   Employee.Position. You do NOT need to run this script for the system to work.
--
--   User.LevelId is an ADMIN OVERRIDE only. The only case where you need to
--   set it explicitly is to assign IT (999) to a specific account — for example,
--   a developer account that has no Employee record or whose position does not
--   reflect their actual authority.
--
--   For all other accounts (Managers, Supervisors, Coordinators, Employees),
--   the rank is derived live from Employee.Position — no stored LevelId needed.
--
-- To manually assign the IT override to a specific user:
--   UPDATE dbo.[User]
--   SET    LevelId = (SELECT LevelId FROM dbo.AccountLevel WHERE LevelName = 'IT')
--   WHERE  UserId = <target UserId>;
--
-- To clear an override and return to dynamic computation:
--   UPDATE dbo.[User] SET LevelId = NULL WHERE UserId = <target UserId>;
-- =============================================================================

-- ── Verify current state ──────────────────────────────────────────────────────
-- Run this to see which User accounts have an explicit LevelId set:
SELECT
    u.UserId,
    u.Name        AS UserName,
    e.Position    AS EmployeePosition,
    al.LevelName  AS OverrideLevel,
    al.LevelRank  AS OverrideRank
FROM       dbo.[User]       u
LEFT JOIN  dbo.AccountLevel al ON u.LevelId  = al.LevelId
LEFT JOIN  dbo.Employee     e  ON u.EmpId    = e.EmpId
WHERE      u.LevelId IS NOT NULL
ORDER BY   al.LevelRank DESC, u.Name;
GO
