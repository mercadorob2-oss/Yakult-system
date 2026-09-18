-- Diagnostic: what the Consumable Stock Monitor's "Yesterday" / "Last 7 days" ranges
--             actually have to work with.
--
-- The monitor's history feed merges two sources for Ink / Toner / Printhead / Cartridge
-- items: dbo.Inventory (the ledger) and dbo.CartridgeMovement. Manual stock edits made
-- through Edit Item (a bare UPDATE dbo.Item SET StockOnHand = ...) are NOT logged to
-- either table, so they can never appear in the feed. Run this to see the split.
--
-- Read-only.

SET NOCOUNT ON;

DECLARE @From datetime2(2) = DATEADD(DAY, -7, SYSDATETIME());   -- last 7 days (server local)
DECLARE @To   datetime2(2) = SYSDATETIME();

------------------------------------------------------------------------
-- 1. dbo.Inventory rows in-window for consumable / cartridge items
------------------------------------------------------------------------
SELECT
    'dbo.Inventory' AS Source,
    COUNT(*)        AS RowsInWindow,
    MIN(inv.DatePosted) AS EarliestInWindow,
    MAX(inv.DatePosted) AS LatestInWindow
FROM dbo.Inventory inv
INNER JOIN dbo.Item i ON inv.ItemId = i.ItemId
WHERE inv.Active = 1
  AND inv.DatePosted >= @From AND inv.DatePosted < @To
  AND (
        i.ConsumableModelId IS NOT NULL OR i.CartridgeModelId IS NOT NULL OR i.ItemType = 'Cartridge'
     OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%ink%'
     OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%toner%'
     OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%printhead%'
     OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%cartridge%'
      )
UNION ALL
SELECT
    'dbo.CartridgeMovement',
    COUNT(*),
    MIN(cmv.CreatedAt),
    MAX(cmv.CreatedAt)
FROM dbo.CartridgeMovement cmv
INNER JOIN dbo.Item i ON cmv.ItemId = i.ItemId
WHERE cmv.CreatedAt >= @From AND cmv.CreatedAt < @To;

------------------------------------------------------------------------
-- 2. Overall date span of each table (is data just older than 7 days?)
------------------------------------------------------------------------
SELECT 'dbo.Inventory'        AS Tbl, MIN(DatePosted) AS Earliest, MAX(DatePosted) AS Latest, COUNT(*) AS TotalRows FROM dbo.Inventory
UNION ALL
SELECT 'dbo.CartridgeMovement', MIN(CreatedAt),       MAX(CreatedAt),       COUNT(*)             FROM dbo.CartridgeMovement;

------------------------------------------------------------------------
-- 3. Sample of the most recent 30 consumable/cartridge movements from either source
------------------------------------------------------------------------
;WITH Ledger AS (
    SELECT inv.DatePosted AS EventAt, i.ItemId, i.Name AS ItemName, i.Category, i.ItemType,
           inv.EntryType, inv.Quantity,
           CASE WHEN inv.EntryType = 'Negative' THEN -inv.Quantity ELSE inv.Quantity END AS SignedQty,
           inv.Description, 'dbo.Inventory' AS Source
    FROM dbo.Inventory inv
    INNER JOIN dbo.Item i ON inv.ItemId = i.ItemId
    WHERE inv.Active = 1
      AND (
            i.ConsumableModelId IS NOT NULL OR i.CartridgeModelId IS NOT NULL OR i.ItemType = 'Cartridge'
         OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%ink%'
         OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%toner%'
         OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%printhead%'
         OR REPLACE(LOWER(ISNULL(i.Category,'')),' ','') LIKE '%cartridge%'
          )
),
Movement AS (
    SELECT cmv.CreatedAt AS EventAt, i.ItemId, i.Name AS ItemName, i.Category, i.ItemType,
           cmv.MovementType AS EntryType, cmv.Quantity,
           CASE WHEN cmv.MovementType = 'Issued' THEN -cmv.Quantity ELSE cmv.Quantity END AS SignedQty,
           cmv.Remarks AS Description, 'dbo.CartridgeMovement' AS Source
    FROM dbo.CartridgeMovement cmv
    INNER JOIN dbo.Item i ON cmv.ItemId = i.ItemId
)
SELECT TOP (30) * FROM (SELECT * FROM Ledger UNION ALL SELECT * FROM Movement) x
ORDER BY EventAt DESC;
