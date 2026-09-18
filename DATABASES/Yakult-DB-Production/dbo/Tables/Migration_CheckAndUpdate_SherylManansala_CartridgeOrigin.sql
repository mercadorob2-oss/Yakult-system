-- ============================================================
-- CHECK: Cartridge requests by Sheryl Manansala
-- Shows each request, the issued cartridge item, and its
-- current origin (Brand New = NULL, Refilled = 'Available').
-- ============================================================
SELECT
    r.ReqId,
    r.Status            AS RequestStatus,
    r.DateRequested,
    e.Name              AS EmployeeName,
    i.ItemId,
    i.Name              AS CartridgeName,
    i.ModelNumber,
    cm.ModelNumber      AS CartridgeModel,
    i.StockOnHand,
    CASE
        WHEN i.RefillStatus = 'Available' THEN 'Refilled'
        ELSE 'Brand New'
    END                 AS CurrentOrigin,
    i.RefillStatus
FROM dbo.Request r
INNER JOIN dbo.Employee e  ON e.EmpId  = r.EmpId
INNER JOIN dbo.Item i      ON i.ItemId = r.ItemId
LEFT  JOIN dbo.CartridgeModel cm ON cm.CartridgeModelId = i.CartridgeModelId
WHERE e.Name     = 'Sheryl Manansala'
  AND i.Category = 'Cartridge'
ORDER BY r.DateRequested DESC;


-- ============================================================
-- UPDATE: Set origin to Refilled for any issued cartridges
-- that are still marked as Brand New (RefillStatus IS NULL).
-- ============================================================
UPDATE i
SET i.RefillStatus  = 'Available',
    i.DateModified  = GETDATE(),
    i.ModifiedBy    = 22          -- admin user ID
FROM dbo.Item i
INNER JOIN dbo.Request r  ON r.ItemId = i.ItemId
INNER JOIN dbo.Employee e ON e.EmpId  = r.EmpId
WHERE e.Name      = 'Sheryl Manansala'
  AND i.Category  = 'Cartridge'
  AND (i.RefillStatus IS NULL OR i.RefillStatus <> 'Available');

-- Confirm rows updated
SELECT @@ROWCOUNT AS RowsUpdated;
