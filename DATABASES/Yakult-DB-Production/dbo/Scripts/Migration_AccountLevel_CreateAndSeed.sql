-- =============================================================================
-- Migration: Create dbo.AccountLevel and seed predefined hierarchy levels
-- Date: 2026-04-10
-- Description:
--   Introduces a numeric hierarchy table used to enforce rank-based approval.
--   LevelRank is used for comparison: approver.LevelRank > requester.LevelRank.
--   IT (LevelRank = 999) is the override level — auto-approves all requests.
-- =============================================================================

-- ── 1. Create table (idempotent) ─────────────────────────────────────────────
IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.AccountLevel')
)
BEGIN
    CREATE TABLE dbo.AccountLevel (
        [LevelId]    INT            IDENTITY (1, 1) NOT NULL,
        [LevelName]  NVARCHAR (50)  NOT NULL,
        [LevelRank]  INT            NOT NULL,
        CONSTRAINT PK_AccountLevel PRIMARY KEY CLUSTERED ([LevelId] ASC),
        CONSTRAINT UQ_AccountLevel_LevelName UNIQUE ([LevelName]),
        CONSTRAINT UQ_AccountLevel_LevelRank UNIQUE ([LevelRank])
    );

    PRINT 'dbo.AccountLevel created.';
END
ELSE
BEGIN
    PRINT 'dbo.AccountLevel already exists — skipping CREATE.';
END
GO

-- ── 2. Seed predefined levels (idempotent per LevelName) ─────────────────────
IF NOT EXISTS (SELECT 1 FROM dbo.AccountLevel WHERE LevelName = 'Employee')
    INSERT INTO dbo.AccountLevel (LevelName, LevelRank) VALUES ('Employee',    1);

IF NOT EXISTS (SELECT 1 FROM dbo.AccountLevel WHERE LevelName = 'Coordinator')
    INSERT INTO dbo.AccountLevel (LevelName, LevelRank) VALUES ('Coordinator', 2);

IF NOT EXISTS (SELECT 1 FROM dbo.AccountLevel WHERE LevelName = 'Supervisor')
    INSERT INTO dbo.AccountLevel (LevelName, LevelRank) VALUES ('Supervisor',  3);

IF NOT EXISTS (SELECT 1 FROM dbo.AccountLevel WHERE LevelName = 'Manager')
    INSERT INTO dbo.AccountLevel (LevelName, LevelRank) VALUES ('Manager',     4);

IF NOT EXISTS (SELECT 1 FROM dbo.AccountLevel WHERE LevelName = 'IT')
    INSERT INTO dbo.AccountLevel (LevelName, LevelRank) VALUES ('IT',        999);

PRINT 'dbo.AccountLevel seeded.';
GO

-- ── 3. Verify ─────────────────────────────────────────────────────────────────
SELECT LevelId, LevelName, LevelRank
FROM   dbo.AccountLevel
ORDER  BY LevelRank;
GO
