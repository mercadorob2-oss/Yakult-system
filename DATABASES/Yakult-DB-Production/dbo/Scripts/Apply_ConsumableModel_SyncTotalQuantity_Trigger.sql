-- Completes Migration_ConsumableModel_AddTotalQuantity.sql on YIMS_PROD.
-- Steps 1 (add TotalQuantity column) and 2 (backfill from Item.StockOnHand)
-- were already applied and re-run clean on 2026-08-22. Only Step 3 — the
-- trigger that keeps TotalQuantity in sync going forward — was missing.
-- This script applies just that step. Safe to re-run (drops the trigger
-- first if it somehow already exists).

IF OBJECT_ID(N'dbo.trg_Item_ConsumableModel_SyncTotalQuantity', 'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Item_ConsumableModel_SyncTotalQuantity;
GO

CREATE TRIGGER dbo.trg_Item_ConsumableModel_SyncTotalQuantity
ON dbo.Item
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH AffectedModels AS (
        SELECT ConsumableModelId FROM inserted WHERE ConsumableModelId IS NOT NULL
        UNION
        SELECT ConsumableModelId FROM deleted WHERE ConsumableModelId IS NOT NULL
    )
    UPDATE cm
    SET cm.TotalQuantity = ISNULL((
        SELECT SUM(i.StockOnHand)
        FROM dbo.Item i
        WHERE i.ConsumableModelId = cm.ConsumableModelId
          AND i.Active = 1
    ), 0)
    FROM dbo.ConsumableModel cm
    INNER JOIN AffectedModels am ON am.ConsumableModelId = cm.ConsumableModelId;
END;
GO

PRINT 'Trigger trg_Item_ConsumableModel_SyncTotalQuantity created on dbo.Item.';
GO

-- Verification: should return one row.
SELECT name AS TriggerName, create_date, modify_date
FROM sys.triggers
WHERE name = 'trg_Item_ConsumableModel_SyncTotalQuantity';
GO
