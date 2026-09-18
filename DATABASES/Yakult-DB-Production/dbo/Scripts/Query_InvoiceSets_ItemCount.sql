-- Counts items across all Invoice Sets (dbo.[Set].IsInvoice = 1), broken down by
-- Hardware vs non-Hardware, per-set and in aggregate, including who created each set.

IF OBJECT_ID('tempdb..#InvoiceSetCounts') IS NOT NULL DROP TABLE #InvoiceSetCounts;

SELECT
    s.SetId,
    s.SetCode,
    s.DocumentNumber,
    s.CreatedAt,
    s.CreatedBy,
    u.Name AS CreatedByName,
    (SELECT COUNT(*)
     FROM dbo.SetItem si
     WHERE si.SetId = s.SetId) AS ItemCount,
    (SELECT COUNT(*)
     FROM dbo.SetItem si
     INNER JOIN dbo.Item i ON i.ItemId = si.ItemId
     WHERE si.SetId = s.SetId AND i.ItemType = 'Hardware') AS HardwareItemCount
INTO #InvoiceSetCounts
FROM dbo.[Set] s
LEFT JOIN dbo.[User] u ON u.UserId = s.CreatedBy
WHERE ISNULL(s.IsInvoice, 0) = 1;

-- 1. Per-set breakdown: total items, Hardware items, and who created the set.
SELECT
    SetId,
    SetCode,
    DocumentNumber,
    CreatedAt,
    CreatedBy,
    CreatedByName,
    ItemCount,
    HardwareItemCount,
    CASE
        WHEN HardwareItemCount = 0 THEN 'None'
        WHEN HardwareItemCount = ItemCount THEN 'All Hardware'
        ELSE 'Mixed'
    END AS HardwareComposition
FROM #InvoiceSetCounts
ORDER BY CreatedAt DESC;

-- 2. Grand totals across all Invoice Sets.
SELECT
    COUNT(*) AS InvoiceSetCount,
    SUM(ItemCount) AS TotalItemsAcrossAllInvoiceSets,
    SUM(HardwareItemCount) AS TotalHardwareItemsAcrossAllInvoiceSets
FROM #InvoiceSetCounts;

-- 3. Invoice Sets composed entirely of Hardware items.
SELECT
    SetId, SetCode, DocumentNumber, CreatedAt, CreatedBy, CreatedByName,
    ItemCount, HardwareItemCount
FROM #InvoiceSetCounts
WHERE ItemCount > 0 AND HardwareItemCount = ItemCount
ORDER BY CreatedAt DESC;

-- 4. Invoice Sets with a mix of at least one Hardware item and at least one non-Hardware item.
SELECT
    SetId, SetCode, DocumentNumber, CreatedAt, CreatedBy, CreatedByName,
    ItemCount, HardwareItemCount, (ItemCount - HardwareItemCount) AS NonHardwareItemCount
FROM #InvoiceSetCounts
WHERE HardwareItemCount > 0 AND HardwareItemCount < ItemCount
ORDER BY CreatedAt DESC;

-- 5. Summary counts: how many Invoice Sets fall into each composition bucket.
SELECT
    SUM(CASE WHEN HardwareItemCount = 0 THEN 1 ELSE 0 END) AS SetsWithNoHardware,
    SUM(CASE WHEN ItemCount > 0 AND HardwareItemCount = ItemCount THEN 1 ELSE 0 END) AS SetsAllHardware,
    SUM(CASE WHEN HardwareItemCount > 0 AND HardwareItemCount < ItemCount THEN 1 ELSE 0 END) AS SetsMixedHardware
FROM #InvoiceSetCounts;

DROP TABLE #InvoiceSetCounts;
