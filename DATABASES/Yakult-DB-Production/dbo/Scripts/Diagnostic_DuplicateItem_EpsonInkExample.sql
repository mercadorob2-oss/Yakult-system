-- Diagnostic: Find duplicate dbo.Item catalog rows sharing the same Name
-- Purpose : Requests are matched to "available stock" by grouping dbo.Item rows on
--           (Name, CategoryId). When a dummy/placeholder Item row is created as a
--           workaround (e.g. because the real row was "unselectable" due to an
--           existing linked Request), the two rows silently pool their StockOnHand
--           together for every future request against that Name/Category.
-- Usage   : Change the @ItemName filter below to any item you suspect has duplicates,
--           or remove the WHERE entirely to scan every Ink/Toner/Print Head item at once.

DECLARE @ItemName NVARCHAR(200) = N'EPSON 001 (BLUE)';

SELECT
    i.ItemId,
    i.Name,
    i.Category,
    i.CategoryId,
    i.ItemType,
    i.StockOnHand,
    i.Active,
    i.DateCreated,
    i.ModelNumber,
    i.SerialNumber,
    (SELECT COUNT(*) FROM dbo.Request r WHERE r.ItemId = i.ItemId) AS LinkedRequestCount,
    (SELECT COUNT(*) FROM dbo.Request r WHERE r.ItemId = i.ItemId AND r.Active = 1) AS ActiveLinkedRequestCount
FROM dbo.Item i
WHERE i.Name = @ItemName
   OR i.Name LIKE '%' + @ItemName + '%'
ORDER BY i.DateCreated;

-- Same scan, but across ALL Ink/Toner/Print Head items at once, to catch any other
-- duplicates created the same way (fuzzy match mirrors RequestRepository.cs's filter):
SELECT
    i.Name,
    i.Category,
    COUNT(*) AS DupRowCount,
    STRING_AGG(CAST(i.ItemId AS NVARCHAR(20)), ', ') AS ItemIds,
    SUM(i.StockOnHand) AS TotalStockOnHand
FROM dbo.Item i
WHERE i.Active = 1
  AND (
        REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%ink%'
     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%toner%'
     OR REPLACE(LOWER(ISNULL(i.Category, '')), ' ', '') LIKE '%printhead%'
      )
GROUP BY i.Name, i.Category
HAVING COUNT(*) > 1
ORDER BY i.Name;
GO
