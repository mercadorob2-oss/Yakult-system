-- Quick check: which database/server is this session actually connected to?
-- Run this first if a search against dbo.ConsumableModel or dbo.Item returns nothing
-- unexpectedly — it's usually because the script ran against the wrong DB (e.g. the
-- prototype/dev mirror instead of the one Yakult.Inventory.App's connection string points to).
SELECT
    DB_NAME()      AS CurrentDatabase,
    @@SERVERNAME   AS CurrentServer,
    (SELECT COUNT(*) FROM dbo.ConsumableModel) AS ConsumableModelRowCount,
    (SELECT COUNT(*) FROM dbo.Item)            AS ItemRowCount;
GO
