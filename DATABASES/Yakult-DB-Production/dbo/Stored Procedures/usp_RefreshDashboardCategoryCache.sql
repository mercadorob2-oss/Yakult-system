-- ============================================================
-- Stored Procedure: usp_RefreshDashboardCategoryCache
-- Purpose  : Truncates and repopulates DashboardCategoryCache
--            from vw_CategoryCombinedSummary.
--            Called on app startup and on dashboard Refresh.
-- ============================================================

CREATE OR ALTER PROCEDURE dbo.usp_RefreshDashboardCategoryCache
AS
BEGIN
    SET NOCOUNT ON;

    TRUNCATE TABLE dbo.DashboardCategoryCache;

    INSERT INTO dbo.DashboardCategoryCache
    (
        CategoryId, CategoryName, [Description], CategoryActive,
        TotalStock, TotalItems_All, TotalActiveItems,
        ActiveStock, SerializedItems, GoodCount, DamagedCount,
        LastRefreshed
    )
    SELECT
        CategoryId,
        CategoryName,
        [Description],
        CategoryActive,
        ISNULL(TotalStock,       0),
        ISNULL(TotalItems_All,   0),
        ISNULL(TotalActiveItems, 0),
        ISNULL(ActiveStock,      0),
        ISNULL(SerializedItems,  0),
        ISNULL(GoodCount,        0),
        ISNULL(DamagedCount,     0),
        GETDATE()
    FROM dbo.vw_CategoryCombinedSummary;
END
