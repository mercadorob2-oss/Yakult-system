
CREATE VIEW [dbo].[vw_CategoryCombinedSummary]
AS
-- 1) Build canonical list of categories: prefer ItemCategory rows, but include
--    standalone Item.Category values when no ItemCategory exists for them.
WITH Categories AS (
    -- categories defined in ItemCategory (preferred; has CategoryId)
    SELECT
        ic.CategoryId,
        ic.Name AS CategoryName,
        ic.Description,
        ic.Active AS CategoryIsActive
    FROM dbo.ItemCategory ic

    UNION

    -- include any Item.Category text that doesn't have a matching ItemCategory.Name
    SELECT
        NULL AS CategoryId,
        i.Category AS CategoryName,
        NULL AS Description,
        NULL AS CategoryIsActive
    FROM dbo.Item i
    WHERE i.Category IS NOT NULL
      AND i.Category NOT IN (SELECT Name FROM dbo.ItemCategory)
      -- Exclude archived items from category list
      AND NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
    GROUP BY i.Category
),

-- 2) Aggregate inventory per category name (summing Positive/Negative)
InventoryAgg AS (
    SELECT
        COALESCE(ic.CategoryId, NULL) AS CategoryId, -- will be NULL for non-mapped names
        COALESCE(ic.Name, i.Category) AS CategoryName,
        SUM(
            CASE
                WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                ELSE 0
            END
        ) AS TotalStock
    FROM dbo.Inventory inv
    INNER JOIN dbo.Item i ON inv.ItemId = i.ItemId
    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
    -- Exclude archived items from inventory calculations
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
    GROUP BY COALESCE(ic.Name, i.Category), COALESCE(ic.CategoryId, NULL)
),

-- 3) Aggregate items per category (counts, active stock, serialized)
ItemAgg AS (
    SELECT
        COALESCE(ic.CategoryId, NULL) AS CategoryId,
        COALESCE(ic.Name, i.Category) AS CategoryName,
        COUNT(DISTINCT i.ItemId) AS TotalItems_All,
        SUM(CASE WHEN i.Active = 1 THEN 1 ELSE 0 END) AS TotalItems_Active,
        SUM(CASE WHEN i.Active = 1 THEN ISNULL(i.StockOnHand,0) ELSE 0 END) AS ActiveStock,
        COUNT(DISTINCT CASE WHEN i.SerialNumber IS NOT NULL AND LEN(i.SerialNumber) > 0 THEN i.ItemId END) AS SerializedItems
    FROM dbo.Item i
    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
    -- FIXED: Exclude archived items from all item counts and stock calculations
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
    GROUP BY COALESCE(ic.Name, i.Category), COALESCE(ic.CategoryId, NULL)
),

-- 4) Condition counts (counts only items; we don't double-count via inventory)
--    Now aggregated into GoodCount and DamagedCount (based on dbo.Condition.ConditionName)
ConditionAgg AS (
    SELECT
        COALESCE(ic.CategoryId, NULL) AS CategoryId,
        COALESCE(ic.Name, i.Category) AS CategoryName,
        SUM(CASE WHEN c.ConditionName = 'Good' THEN 1 ELSE 0 END) AS GoodCount,
        SUM(CASE WHEN c.ConditionName = 'Damaged' THEN 1 ELSE 0 END) AS DamagedCount
    FROM dbo.Item i
    LEFT JOIN dbo.ItemCategory ic ON i.CategoryId = ic.CategoryId
    LEFT JOIN dbo.[Condition] c ON i.ConditionId = c.ConditionId
    -- FIXED: Exclude archived items from condition counts
    WHERE NOT EXISTS (SELECT 1 FROM dbo.ArchiveStatus arch WHERE arch.EntityType = 'Item' AND arch.EntityId = i.ItemId AND arch.IsArchived = 1)
    GROUP BY COALESCE(ic.Name, i.Category), COALESCE(ic.CategoryId, NULL)
)

-- Final select: left-join the aggregates to the canonical category list.
SELECT
    -- Keep CategoryId when available (from ItemCategory), otherwise NULL
    cat.CategoryId,
    cat.CategoryName,
    cat.Description,
    cat.CategoryIsActive AS CategoryActive,

    -- Inventory totals (0 when no data)
    ISNULL(invAgg.TotalStock, 0) AS TotalStock,

    -- Item counts
    ISNULL(itemAgg.TotalItems_All, 0)     AS TotalItems_All,
    ISNULL(itemAgg.TotalItems_Active, 0)  AS TotalActiveItems,
    ISNULL(itemAgg.ActiveStock, 0)        AS ActiveStock,
    ISNULL(itemAgg.SerializedItems, 0)    AS SerializedItems,

    -- Condition breakdowns (Good / Damaged)
    ISNULL(condAgg.GoodCount, 0)          AS GoodCount,
    ISNULL(condAgg.DamagedCount, 0)       AS DamagedCount

FROM Categories cat
LEFT JOIN InventoryAgg invAgg
    ON ( (invAgg.CategoryId IS NOT NULL AND invAgg.CategoryId = cat.CategoryId)
         OR (invAgg.CategoryId IS NULL AND invAgg.CategoryName = cat.CategoryName) )
LEFT JOIN ItemAgg itemAgg
    ON ( (itemAgg.CategoryId IS NOT NULL AND itemAgg.CategoryId = cat.CategoryId)
         OR (itemAgg.CategoryId IS NULL AND itemAgg.CategoryName = cat.CategoryName) )
LEFT JOIN ConditionAgg condAgg
    ON ( (condAgg.CategoryId IS NOT NULL AND condAgg.CategoryId = cat.CategoryId)
         OR (condAgg.CategoryId IS NULL AND condAgg.CategoryName = cat.CategoryName) );