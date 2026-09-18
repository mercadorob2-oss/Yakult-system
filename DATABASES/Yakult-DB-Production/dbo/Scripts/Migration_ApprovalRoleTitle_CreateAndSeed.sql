-- ============================================================
-- Migration: Create and seed dbo.ApprovalRoleTitle
-- Purpose  : Defines which employee positions are auto-approved
--            when submitting a cartridge request via the portal.
-- Safe to run multiple times — skips existing rows.
-- ============================================================

-- 1. Create table if it does not exist
IF OBJECT_ID('dbo.ApprovalRoleTitle', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.ApprovalRoleTitle
    (
        TitleId         INT           IDENTITY(1,1) NOT NULL,
        PositionTitle   NVARCHAR(100) NOT NULL,
        ApprovalRole    NVARCHAR(20)  NOT NULL,
        RolePriority    INT           NOT NULL,
        IsActive        BIT           NOT NULL,
        DateCreated     DATETIME2(2)  NOT NULL,
        CreatedByUserId INT           NULL,
        CONSTRAINT PK_ApprovalRoleTitle              PRIMARY KEY CLUSTERED (TitleId ASC),
        CONSTRAINT UQ_ApprovalRoleTitle_PositionTitle UNIQUE (PositionTitle)
    );

    PRINT 'Created table dbo.ApprovalRoleTitle';
END
ELSE
BEGIN
    PRINT 'Table dbo.ApprovalRoleTitle already exists — skipping CREATE.';
END

-- 2. Seed supervisor / coordinator / manager positions.
--    Skips any PositionTitle that already exists.
--    ApprovalRole   : broad role category (max 20 chars)
--    RolePriority   : 1 = highest seniority
--    CreatedByUserId: NULL (system-seeded)

INSERT INTO dbo.ApprovalRoleTitle
    (PositionTitle,                  ApprovalRole,  RolePriority, IsActive, DateCreated, CreatedByUserId)
SELECT v.PositionTitle, v.ApprovalRole, v.RolePriority, 1, GETDATE(), NULL
FROM (VALUES
    ('MANAGER',                      'Manager',      1),
    ('ASST. MANAGER',                'Manager',      2),
    ('JR. ASST. MANAGER',            'Manager',      3),
    ('ACTING JR. ASST. MANAGER',     'Manager',      4),
    ('SUPERVISOR',                   'Supervisor',   5),
    ('COORDINATOR',                  'Coordinator',  6),
    ('ACCOUNT COORDINATOR',          'Coordinator',  7),
    ('ACTING ACCOUNT COORDINATOR',   'Coordinator',  8),
    ('ASST. COORDINATOR',            'Coordinator',  9),
    ('LADY COORDINATOR',             'Coordinator',  10)
) AS v (PositionTitle, ApprovalRole, RolePriority)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ApprovalRoleTitle t
    WHERE  UPPER(LTRIM(RTRIM(t.PositionTitle))) = UPPER(LTRIM(RTRIM(v.PositionTitle)))
);

-- Re-activate any that were previously deactivated
UPDATE dbo.ApprovalRoleTitle
SET    IsActive = 1
WHERE  IsActive = 0
  AND  UPPER(LTRIM(RTRIM(PositionTitle))) IN (
        'MANAGER', 'ASST. MANAGER', 'JR. ASST. MANAGER',
        'ACTING JR. ASST. MANAGER', 'SUPERVISOR', 'COORDINATOR',
        'ACCOUNT COORDINATOR', 'ACTING ACCOUNT COORDINATOR',
        'ASST. COORDINATOR', 'LADY COORDINATOR'
       );

PRINT 'ApprovalRoleTitle seeded with supervisor positions.';
