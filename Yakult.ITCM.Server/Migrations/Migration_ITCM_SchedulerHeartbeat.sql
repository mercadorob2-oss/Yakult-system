-- Migration: Add CallSchedulerHeartbeat table for the centralized ITCM server
-- This table logs every background job run (reminder + escalation processing)
-- so the dashboard, diagnostics, and API can report real run history.

IF NOT EXISTS (
    SELECT 1 FROM sys.tables t
    JOIN sys.schemas s ON s.schema_id = t.schema_id
    WHERE s.name = 'dbo' AND t.name = 'CallSchedulerHeartbeat'
)
BEGIN
    CREATE TABLE dbo.CallSchedulerHeartbeat (
        HeartbeatId          BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        StartedAtUtc         DATETIME2            NOT NULL,
        FinishedAtUtc        DATETIME2            NULL,
        Succeeded            BIT                  NULL,
        LockAcquired         BIT                  NOT NULL DEFAULT 0,
        ReminderCandidates   INT                  NOT NULL DEFAULT 0,
        RemindersSent        INT                  NOT NULL DEFAULT 0,
        EscalationCandidates INT                  NOT NULL DEFAULT 0,
        EscalationsApplied   INT                  NOT NULL DEFAULT 0,
        ErrorMessage         NVARCHAR(2000)       NULL,
        MachineName          NVARCHAR(255)        NOT NULL DEFAULT '',
        Version              NVARCHAR(50)         NOT NULL DEFAULT ''
    );

    -- Index for fast summary queries (last N heartbeats, latest success/failure)
    CREATE NONCLUSTERED INDEX IX_CallSchedulerHeartbeat_StartedAtUtc
        ON dbo.CallSchedulerHeartbeat (StartedAtUtc DESC)
        INCLUDE (Succeeded, LockAcquired, ReminderCandidates, RemindersSent,
                 EscalationCandidates, EscalationsApplied, ErrorMessage, MachineName);

    -- Index for status queries
    CREATE NONCLUSTERED INDEX IX_CallSchedulerHeartbeat_Succeeded
        ON dbo.CallSchedulerHeartbeat (Succeeded)
        INCLUDE (StartedAtUtc, FinishedAtUtc, ErrorMessage);

    PRINT 'Created dbo.CallSchedulerHeartbeat table.';
END
ELSE
BEGIN
    PRINT 'dbo.CallSchedulerHeartbeat table already exists.';
END
GO
