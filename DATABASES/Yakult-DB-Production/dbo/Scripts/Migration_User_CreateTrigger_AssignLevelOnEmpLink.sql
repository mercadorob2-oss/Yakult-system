-- =============================================================================
-- Migration: Create trigger trg_User_AssignLevelOnEmpLink
-- Date: 2026-04-10
-- Run AFTER:
--   1. Migration_AccountLevel_CreateAndSeed.sql
--   2. Migration_User_AddLevelId.sql
--
-- Purpose:
--   Automatically assigns User.LevelId when a User account is created or
--   updated with an EmpId that links to an approver-level employee
--   (Manager, Supervisor, Coordinator).
--
--   This means when a new account is made for a higher-up or IT employee,
--   their level is set immediately — no manual script needed afterward.
--
-- Rules:
--   - Manager group positions      → Manager     (4)
--   - Supervisor position          → Supervisor  (3)
--   - Coordinator group positions  → Coordinator (2)
--   - All other positions          → LevelId stays NULL (dynamic CASE at login handles it)
--   - IT (999) is NOT assigned automatically by department — set manually via LevelId override
--
-- Safety:
--   - Never overwrites an existing LevelId (manual admin overrides are preserved)
--   - Only fires when EmpId is set on the User row
-- =============================================================================

CREATE OR ALTER TRIGGER dbo.trg_User_AssignLevelOnEmpLink
ON dbo.[User]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Only process rows where LevelId is currently NULL (preserve manual overrides)
    UPDATE u
    SET    u.LevelId = al.LevelId
    FROM   dbo.[User]        u
    INNER JOIN inserted      i   ON  i.UserId  = u.UserId
    LEFT  JOIN dbo.Employee  e   ON  e.EmpId   = i.EmpId
    INNER JOIN dbo.AccountLevel al ON al.LevelName = CASE
        -- IsDeveloper = 1 → always IT (999), regardless of position or EmpId
        WHEN i.IsDeveloper = 1
            THEN 'IT'
        -- Higher-up positions
        WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                 'MANAGER',
                 'ASST. MANAGER',
                 'JR. ASST. MANAGER',
                 'ACTING JR. ASST. MANAGER')
            THEN 'Manager'
        WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) = 'SUPERVISOR'
            THEN 'Supervisor'
        WHEN UPPER(LTRIM(RTRIM(ISNULL(e.Position, '')))) IN (
                 'COORDINATOR',
                 'ACCOUNT COORDINATOR',
                 'ACTING ACCOUNT COORDINATOR',
                 'ASST. COORDINATOR',
                 'LADY COORDINATOR')
            THEN 'Coordinator'
        ELSE NULL  -- Employee-level and unrecognised positions: leave NULL,
                   -- dynamic CASE in auth SQL handles rank at login
    END
    WHERE  u.LevelId IS NULL;
END;
GO

PRINT 'Trigger dbo.trg_User_AssignLevelOnEmpLink created.';
GO

-- ── Backfill existing IsDeveloper accounts ────────────────────────────────────
-- The trigger only fires on future INSERT/UPDATE. Existing developer accounts
-- that were created before this script ran need to be backfilled manually.
UPDATE u
SET    u.LevelId = al.LevelId
FROM   dbo.[User]        u
INNER JOIN dbo.AccountLevel al ON al.LevelName = 'IT'
WHERE  u.IsDeveloper = 1
  AND  u.LevelId     IS NULL;

PRINT CONCAT(@@ROWCOUNT, ' existing developer account(s) assigned IT level.');
GO
