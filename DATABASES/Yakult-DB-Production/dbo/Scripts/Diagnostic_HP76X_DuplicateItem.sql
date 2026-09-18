-- Diagnostic: find both dbo.Item rows for HP 76X (correct spelling + "Lasejet" typo)
-- The BatchAddRequestDialog Item picker lists raw Item.Name values, so a typo'd
-- duplicate Item row shows up as a second, seemingly-identical entry. Searching
-- "LASERJET" on ViewItemsPage misses the typo'd "Lasejet" row entirely, which is
-- why it looked like there was only one.

SELECT ItemId, Name, ModelNumber, Category, ConsumableModelId, StockOnHand, Active, DateCreated,
       (SELECT COUNT(*) FROM dbo.Request r WHERE r.ItemId = i.ItemId) AS LinkedRequestCount
FROM dbo.Item i
WHERE i.Name LIKE '%76X%' AND i.Name LIKE '%Las%'
ORDER BY i.Name;
GO
