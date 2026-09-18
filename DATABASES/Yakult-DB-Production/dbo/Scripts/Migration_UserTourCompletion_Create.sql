-- Migration: Create UserTourCompletion table
-- Tracks which guided tours each user has completed or skipped.
-- TourKey values: 'user-new-request', 'user-my-requests' (expandable for future tours)

IF NOT EXISTS (
    SELECT 1 FROM sys.tables
    WHERE object_id = OBJECT_ID('dbo.UserTourCompletion')
)
BEGIN
    CREATE TABLE dbo.UserTourCompletion (
        Id          INT IDENTITY(1,1) NOT NULL,
        UserId      INT NOT NULL,
        TourKey     NVARCHAR(100) NOT NULL,
        CompletedAt DATETIME NOT NULL DEFAULT GETDATE(),
        CONSTRAINT PK_UserTourCompletion PRIMARY KEY (Id),
        CONSTRAINT UQ_UserTourCompletion UNIQUE (UserId, TourKey)
    );
END
