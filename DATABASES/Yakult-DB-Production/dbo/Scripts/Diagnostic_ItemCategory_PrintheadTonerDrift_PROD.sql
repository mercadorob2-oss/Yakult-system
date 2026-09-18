-- Diagnostic (READ-ONLY): category-name drift for Printhead / Toner on YIMS_PROD.
--
-- Goal state: exactly one ItemCategory row named 'Printhead' and one named 'Toner',
--             with dbo.Item.Category text and dbo.ConsumableModel.Category all in line,
--             and no duplicate dbo.Item rows left pooling stock across the old spellings.
--
-- Run every result set and hand the output back before applying any merge.

SET NOCOUNT ON;

PRINT '========== 1. ItemCategory rows in the affected families ==========';
SELECT c.CategoryId, c.Name, c.Active,
       (SELECT COUNT(*) FROM dbo.Item i WHERE i.CategoryId = c.CategoryId)               AS ItemRows,
       (SELECT COUNT(*) FROM dbo.Item i WHERE i.CategoryId = c.CategoryId AND i.Active=1) AS ActiveItemRows,
       (SELECT SUM(i.StockOnHand) FROM dbo.Item i WHERE i.CategoryId = c.CategoryId AND i.Active=1) AS ActiveStock,
       (SELECT COUNT(*) FROM dbo.VendorItemCategory v WHERE v.CategoryId = c.CategoryId)  AS VendorCatRows
FROM dbo.ItemCategory c
WHERE REPLACE(LOWER(c.Name),' ','') IN ('printhead','printerhead','toner','tonercartridge')
ORDER BY REPLACE(LOWER(c.Name),' ',''), c.CategoryId;

PRINT '========== 2. dbo.Item.Category text vs its CategoryId (mismatch hunt) ==========';
SELECT i.Category AS ItemCategoryText, i.CategoryId, c.Name AS CategoryRowName,
       COUNT(*) AS Items, SUM(CASE WHEN i.Active=1 THEN 1 ELSE 0 END) AS ActiveItems,
       SUM(i.StockOnHand) AS Stock
FROM dbo.Item i
LEFT JOIN dbo.ItemCategory c ON c.CategoryId = i.CategoryId
WHERE REPLACE(LOWER(ISNULL(i.Category,'')),' ','') IN ('printhead','printerhead','toner','tonercartridge')
   OR REPLACE(LOWER(ISNULL(c.Name,'')),' ','')     IN ('printhead','printerhead','toner','tonercartridge')
GROUP BY i.Category, i.CategoryId, c.Name
ORDER BY i.CategoryId, i.Category;

PRINT '========== 3. dbo.ConsumableModel.Category values ==========';
SELECT Category, COUNT(*) AS Models,
       SUM(CASE WHEN IsActive=1 AND IsRequestable=1 THEN 1 ELSE 0 END) AS ActiveRequestable,
       SUM(TotalQuantity) AS TotalQty
FROM dbo.ConsumableModel
GROUP BY Category
ORDER BY Category;

PRINT '========== 4. Duplicate Item rows that would collide after a category merge ==========';
-- Groups keyed on Name + normalized-category: >1 row means their StockOnHand must be combined.
SELECT i.Name,
       REPLACE(LOWER(i.Category),' ','') AS NormCat,
       COUNT(*)                                              AS DupRows,
       STRING_AGG(CAST(i.ItemId AS VARCHAR(20)), ', ')       AS ItemIds,
       STRING_AGG(CAST(i.StockOnHand AS VARCHAR(20)), ', ')  AS StockEach,
       SUM(i.StockOnHand)                                    AS StockTotal,
       STRING_AGG(ISNULL(CAST(i.ConsumableModelId AS VARCHAR(20)),'NULL'), ', ') AS ModelIds
FROM dbo.Item i
WHERE i.Active = 1
  AND REPLACE(LOWER(ISNULL(i.Category,'')),' ','') IN ('printhead','printerhead','toner','tonercartridge')
GROUP BY i.Name, REPLACE(LOWER(i.Category),' ','')
HAVING COUNT(*) > 1
ORDER BY i.Name;

PRINT '========== 5. FK-reference load on the Item rows in those dup groups ==========';
-- For each Item that might be retired in the qty-combine: how many history rows point at it.
;WITH DupItems AS (
    SELECT i.ItemId, i.Name, i.Category, i.StockOnHand, i.ConsumableModelId, i.Active
    FROM dbo.Item i
    WHERE i.Active = 1
      AND REPLACE(LOWER(ISNULL(i.Category,'')),' ','') IN ('printhead','printerhead','toner','tonercartridge')
      AND EXISTS (
          SELECT 1 FROM dbo.Item j
          WHERE j.Active = 1 AND j.Name = i.Name
            AND REPLACE(LOWER(ISNULL(j.Category,'')),' ','') = REPLACE(LOWER(ISNULL(i.Category,'')),' ','')
            AND j.ItemId <> i.ItemId)
)
SELECT d.ItemId, d.Name, d.Category, d.StockOnHand, d.ConsumableModelId,
       (SELECT COUNT(*) FROM dbo.Request r              WHERE r.ItemId = d.ItemId) AS Request_,
       (SELECT COUNT(*) FROM dbo.SetItem s              WHERE s.ItemId = d.ItemId) AS SetItem_,
       (SELECT COUNT(*) FROM dbo.Renewals r             WHERE r.ItemId = d.ItemId) AS Renewals_,
       (SELECT COUNT(*) FROM dbo.Cartridge c            WHERE c.ItemId = d.ItemId) AS Cartridge_,
       (SELECT COUNT(*) FROM dbo.Inventory v            WHERE v.ItemId = d.ItemId) AS Inventory_,
       (SELECT COUNT(*) FROM dbo.BorrowLog b            WHERE b.ItemId = d.ItemId) AS BorrowLog_,
       (SELECT COUNT(*) FROM dbo.RepairTicket t         WHERE t.ItemId = d.ItemId) AS RepairTicket_,
       (SELECT COUNT(*) FROM dbo.InvoicePreparationItem p WHERE p.ItemId = d.ItemId) AS InvPrepItem_,
       (SELECT COUNT(*) FROM dbo.ItemInspectionLog l    WHERE l.ItemId = d.ItemId) AS InspectionLog_,
       (SELECT COUNT(*) FROM dbo.Asset a                WHERE a.ItemId = d.ItemId) AS Asset_
FROM DupItems d
ORDER BY d.Name, d.ItemId;
GO
