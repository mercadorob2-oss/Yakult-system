-- Migration: Add TotalQuantity to dbo.ConsumableModel
-- Purpose : dbo.ConsumableModel groups consumables (Ink/Toner/Print Head) by model,
--           but until now the only place quantity was visible was on the individual
--           dbo.Item catalog rows (the item "codes") linked via ConsumableModelId.
--           This adds a TotalQuantity column directly on ConsumableModel, backfills
--           it from existing Item.StockOnHand, and keeps it in sync going forward
--           via a trigger on dbo.Item (mirrors the sum used by
--           IX_Item_ConsumableModel_Available, which only counts Active items).

-- Step 1: Add the column
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.ConsumableModel') AND name = N'TotalQuantity'
)
BEGIN
    ALTER TABLE dbo.ConsumableModel
        ADD [TotalQuantity] INT NOT NULL DEFAULT (0);

    PRINT 'Column TotalQuantity added to dbo.ConsumableModel.';
END
ELSE
BEGIN
    PRINT 'Column TotalQuantity already exists on dbo.ConsumableModel. Skipping.';
END
GO

-- Step 2: Backfill from existing Item rows (sum of StockOnHand for Active items
-- tied to each model, same scope as IX_Item_ConsumableModel_Available)
UPDATE cm
SET cm.TotalQuantity = ISNULL(sub.Total, 0)
FROM dbo.ConsumableModel cm
LEFT JOIN (
    SELECT i.ConsumableModelId, SUM(i.StockOnHand) AS Total
    FROM dbo.Item i
    WHERE i.ConsumableModelId IS NOT NULL
      AND i.Active = 1
    GROUP BY i.ConsumableModelId
) sub ON sub.ConsumableModelId = cm.ConsumableModelId;

PRINT 'dbo.ConsumableModel.TotalQuantity backfilled from dbo.Item.';
GO

-- Step 3: Keep TotalQuantity in sync whenever linked Item rows change
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
