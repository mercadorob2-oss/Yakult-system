-- Fix: ConsumableModelId 12 "EPSON 057 (CYAN)" was miscategorized as Toner during
-- backfill (source Item.Category text must have matched the "toner" fuzzy check) —
-- it's an Epson ink cartridge, so correct it to Ink.

SELECT ConsumableModelId, ModelNumber, Category FROM dbo.ConsumableModel WHERE ConsumableModelId = 12;

-- Check the underlying Item(s) too — Item.Category (not ConsumableModel.Category) is what
-- the request picker's Category dropdown and RequestRepository's fuzzy filter actually key
-- off of, so if it's ALSO wrong ("Toner"/something toner-ish) this item would keep behaving
-- like a toner item elsewhere even after the ConsumableModel label above is corrected.
SELECT ItemId, Name, Category, CategoryId, ConsumableModelId
FROM dbo.Item
WHERE ConsumableModelId = 12;

UPDATE dbo.ConsumableModel
SET Category = 'Ink'
WHERE ConsumableModelId = 12;

-- If the SELECT above shows Item.Category as anything Toner-ish, uncomment and adjust:
-- (CategoryId must point to an actual Ink row in dbo.ItemCategory — check its Id first.)
/*
UPDATE dbo.Item
SET Category = 'Ink',
    CategoryId = (SELECT CategoryId FROM dbo.ItemCategory WHERE Name = 'Ink' AND Active = 1)
WHERE ConsumableModelId = 12;
*/

SELECT ConsumableModelId, ModelNumber, Category FROM dbo.ConsumableModel WHERE ConsumableModelId = 12;
GO
