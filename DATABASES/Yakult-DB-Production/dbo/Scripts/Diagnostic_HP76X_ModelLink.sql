-- Diagnostic: HP 76X specifically — confirm the two ConsumableModel rows and which
-- one (if either) Item 693 "HP LASERJET 76X (BLACK)" is currently linked to.
-- ViewItemsPage confirms there is only ONE physical Item row for this product, so
-- this is a pure ConsumableModel duplicate (typo'd "Lasejet" vs "Laserjet"), not a
-- duplicate Item catalog row like the earlier EPSON/HP CF276X cases.

SELECT ConsumableModelId, ModelNumber, Category, IsActive, CreatedAt
FROM dbo.ConsumableModel
WHERE ModelNumber LIKE '%76X%'
ORDER BY ModelNumber;

SELECT ItemId, Name, ModelNumber, Category, ConsumableModelId, StockOnHand, Active
FROM dbo.Item
WHERE ItemId = 693;
GO
