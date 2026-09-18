-- Diagnostic: Find duplicate/near-duplicate model text across ConsumableModel AND Item
-- Purpose : "HP 76X Black Lasejet Toner Cartridge" vs "...Laserjet..." showed up together
--           in a dropdown, but a search against dbo.ConsumableModel alone found nothing —
--           meaning the duplicate likely lives in dbo.Item.ModelNumber/Name instead (the
--           Model Number field's autocomplete draws from there, not from ConsumableModel).
--           This script checks BOTH places so we find where it actually is.
-- Usage   : Change @Search below to a fragment of the model name you're chasing.

DECLARE @Search NVARCHAR(200) = N'76X';

PRINT '--- dbo.ConsumableModel ---';
SELECT
    cm.ConsumableModelId,
    cm.ModelNumber,
    cm.Category,
    cm.IsActive,
    (SELECT COUNT(*) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId) AS LinkedItemCount,
    (SELECT ISNULL(SUM(i.StockOnHand), 0) FROM dbo.Item i WHERE i.ConsumableModelId = cm.ConsumableModelId AND i.Active = 1) AS TotalStock
FROM dbo.ConsumableModel cm
WHERE cm.ModelNumber LIKE '%' + @Search + '%'
ORDER BY cm.ModelNumber;

PRINT '--- dbo.Item (by Name or ModelNumber) ---';
SELECT
    i.ItemId,
    i.Name,
    i.ModelNumber,
    i.Category,
    i.ConsumableModelId,
    i.StockOnHand,
    i.Active,
    i.DateCreated
FROM dbo.Item i
WHERE i.Name LIKE '%' + @Search + '%'
   OR i.ModelNumber LIKE '%' + @Search + '%'
ORDER BY i.Name, i.ModelNumber;
GO
