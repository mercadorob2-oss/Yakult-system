CREATE TABLE [dbo].[User] (
    [UserId]              INT             IDENTITY (1, 1) NOT NULL,
    [Name]                NVARCHAR (100)  NOT NULL,
    [EmailAddress]        NVARCHAR (255)  NULL,
    [DateCreated]         DATETIME2 (2)   DEFAULT (sysutcdatetime()) NOT NULL,
    [RowVer]              ROWVERSION      NOT NULL,
    [Password]            VARBINARY (128) NULL,
    [IsDeveloper]         BIT             DEFAULT ((0)) NOT NULL,
    [EmpId]               INT             NULL,
    [PasswordHash]        VARBINARY (256) NULL,
    [PasswordSalt]        VARBINARY (128) NULL,
    [IsTemporaryPassword] BIT             DEFAULT ((0)) NOT NULL,
    [MustChangePassword]  BIT             DEFAULT ((0)) NOT NULL,
    [LastLoginDate]       DATETIME        CONSTRAINT [DF_User_LastLoginDate] DEFAULT ((sysdatetimeoffset() AT TIME ZONE 'Singapore Standard Time')) NULL,
    [IsActive]            BIT             DEFAULT ((1)) NOT NULL,
    [IsSuperAdmin]        BIT             DEFAULT ((0)) NOT NULL,
    [LevelId]             INT             NULL,
    PRIMARY KEY CLUSTERED ([UserId] ASC),
    CONSTRAINT [FK_User_AccountLevel] FOREIGN KEY ([LevelId]) REFERENCES [dbo].[AccountLevel] ([LevelId]),
    CONSTRAINT [FK_User_Employee] FOREIGN KEY ([EmpId]) REFERENCES [dbo].[Employee] ([EmpId])
);




GO
CREATE UNIQUE NONCLUSTERED INDEX [UX_User_Email]
    ON [dbo].[User]([EmailAddress] ASC) WHERE ([EmailAddress] IS NOT NULL);


GO
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

CREATE   TRIGGER dbo.trg_User_AssignLevelOnEmpLink
ON dbo.[User]
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Only process rows where:
    --   1. EmpId is set (user is linked to an employee)
    --   2. LevelId is currently NULL (no existing override to protect)
    UPDATE u
    SET    u.LevelId = al.LevelId
    FROM   dbo.[User]        u
    INNER JOIN inserted      i   ON  i.UserId   = u.UserId
    INNER JOIN dbo.Employee  e   ON  e.EmpId    = i.EmpId
    INNER JOIN dbo.AccountLevel al ON al.LevelName = CASE
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
    WHERE  u.LevelId  IS NULL
      AND  i.EmpId    IS NOT NULL;
END;