
-- Create the view with proper EntryType logic
CREATE VIEW dbo.vw_CategoryStockSummary
AS
SELECT 
    ISNULL(i.Category, '(Uncategorized)') AS CategoryName,
    -- Calculate total stock from Inventory entries using Positive/Negative
    ISNULL((
        SELECT SUM(
            CASE 
                WHEN inv.EntryType = 'Positive' THEN inv.Quantity
                WHEN inv.EntryType = 'Negative' THEN -inv.Quantity
                ELSE 0
            END
        )
        FROM dbo.Inventory inv
        INNER JOIN dbo.Item i2 ON inv.ItemId = i2.ItemId
        WHERE i2.Category = i.Category
    ), 0) AS TotalStock,
    COUNT(DISTINCT i.ItemId) AS TotalItems,
    SUM(CASE WHEN i.Active = 1 THEN i.StockOnHand ELSE 0 END) AS ActiveStock,
    COUNT(DISTINCT CASE WHEN i.SerialNumber IS NOT NULL AND LEN(i.SerialNumber) > 0 THEN i.ItemId END) AS SerializedItems
FROM dbo.Item i
WHERE i.Category IS NOT NULL
GROUP BY i.Category;
