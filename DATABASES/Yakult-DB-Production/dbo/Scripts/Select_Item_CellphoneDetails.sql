-- Editable grid view of Item records where Category = 'Cellphone'.
-- Run this in SSMS, then edit cells directly in the results grid (same as "Select Top 1000 Rows").
-- ItemId is included so SSMS can resolve the primary key for in-grid edits — leave it in the SELECT.
SELECT TOP (1000)
    [ItemId],
    [Name],
    [ModelNumber],
    [SerialNumber],
    [Description],
    [CellPhoneNumber],
    [IMEI1],
    [IMEI2]
FROM [dbo].[Item]
WHERE [Category] = 'Cellphone'
ORDER BY [Name];
