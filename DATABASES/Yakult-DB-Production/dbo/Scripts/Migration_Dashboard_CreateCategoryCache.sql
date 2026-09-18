-- ============================================================
-- Migration: Create DashboardCategoryCache pre-aggregation table
-- Purpose  : Stores pre-computed category summary so the
--            dashboard reads from a flat table instead of running
--            vw_CategoryCombinedSummary (4 CTEs + correlated
--            NOT EXISTS subqueries) on every page load.
-- Refresh  : usp_RefreshDashboardCategoryCache (called on app
--            startup and on dashboard Refresh click).
-- ============================================================

IF OBJECT_ID('dbo.DashboardCategoryCache', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.DashboardCategoryCache
    (
        CacheId          INT             IDENTITY(1,1) PRIMARY KEY,
        CategoryId       INT             NULL,           -- NULL for unmapped Item.Category text rows
        CategoryName     NVARCHAR(255)   NOT NULL,
        [Description]    NVARCHAR(500)   NULL,
        CategoryActive   BIT             NULL,
        TotalStock       INT             NOT NULL DEFAULT 0,
        TotalItems_All   INT             NOT NULL DEFAULT 0,
        TotalActiveItems INT             NOT NULL DEFAULT 0,
        ActiveStock      INT             NOT NULL DEFAULT 0,
        SerializedItems  INT             NOT NULL DEFAULT 0,
        GoodCount        INT             NOT NULL DEFAULT 0,
        DamagedCount     INT             NOT NULL DEFAULT 0,
        LastRefreshed    DATETIME        NOT NULL DEFAULT GETDATE()
    );

    PRINT 'Created table dbo.DashboardCategoryCache';
END
ELSE
BEGIN
    PRINT 'Table dbo.DashboardCategoryCache already exists — skipped.';
END
