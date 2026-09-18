-- ============================================================
-- Migration: dbo.PortalContentProgress — per-employee course progress
--
-- Prototype/testing migration. Stores per-user watch progress for
-- Course-type portal content: furthest-watched percent, resume
-- position, and permanent completion state. One row per (User, Content).
-- Safe to run repeatedly.
-- ============================================================

IF OBJECT_ID('dbo.PortalContentProgress','U') IS NULL
BEGIN
    CREATE TABLE dbo.PortalContentProgress(
        ProgressId      INT IDENTITY PRIMARY KEY,
        UserId          INT NOT NULL,
        ContentId       INT NOT NULL,
        ProgressPercent INT NOT NULL DEFAULT 0,
        LastPositionSec INT NOT NULL DEFAULT 0,
        DurationSec     INT NOT NULL DEFAULT 0,
        IsCompleted     BIT NOT NULL DEFAULT 0,
        CompletedAtUtc  DATETIME2(2) NULL,
        StartedAtUtc    DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
        UpdatedAtUtc    DATETIME2(2) NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT UX_PortalContentProgress_UserContent UNIQUE(UserId, ContentId),
        CONSTRAINT FK_PortalContentProgress_User    FOREIGN KEY(UserId)    REFERENCES dbo.[User](UserId),
        CONSTRAINT FK_PortalContentProgress_Content FOREIGN KEY(ContentId) REFERENCES dbo.PortalContent(ContentId));
    CREATE INDEX IX_PortalContentProgress_ContentId ON dbo.PortalContentProgress(ContentId);
    PRINT 'dbo.PortalContentProgress created.';
END
ELSE
BEGIN
    PRINT 'dbo.PortalContentProgress already exists - skipped.';
END
GO
