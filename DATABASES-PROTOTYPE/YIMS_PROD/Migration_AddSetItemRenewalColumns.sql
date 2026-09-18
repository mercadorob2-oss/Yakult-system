-- ============================================================
-- Migration: Add per-item renewal tracking columns to SetItem
-- Run once on the live database before deploying app changes.
-- ============================================================

-- 1. Add RenewalStatus column (Active / Expired / Renewed / Archived)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SetItem')
      AND name = 'RenewalStatus'
)
BEGIN
    ALTER TABLE dbo.SetItem
        ADD [RenewalStatus] NVARCHAR(20) NULL;
    PRINT 'Added column dbo.SetItem.RenewalStatus';
END
ELSE
    PRINT 'Column dbo.SetItem.RenewalStatus already exists — skipped.';

-- 2. Add RenewalReferenceId (FK to the new SetItemId created during renewal)
IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SetItem')
      AND name = 'RenewalReferenceId'
)
BEGIN
    ALTER TABLE dbo.SetItem
        ADD [RenewalReferenceId] INT NULL;
    PRINT 'Added column dbo.SetItem.RenewalReferenceId';
END
ELSE
    PRINT 'Column dbo.SetItem.RenewalReferenceId already exists — skipped.';

GO

-- 3. Refresh vw_RenewalStatus using CREATE OR ALTER (preserves permissions, no momentary removal).
--    The view now produces ONE row per Set (correlated subqueries replace JOIN fan-out)
--    and adds: TotalActiveItems, ExpiredItemsCount, RenewedItemsCount, ArchivedItemsCount, SetLevelStatus.

CREATE OR ALTER VIEW [dbo].[vw_RenewalStatus]
AS
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.DispatchDate AS DocumentDate,
    s.Status,
    s.Remarks,

    s.StartDate,
    s.EndDate,

    CASE
        WHEN s.EndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
    END AS DaysUntilExpiry,

    CASE
        WHEN s.EndDate IS NULL THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    s.ComId,
    ISNULL(c.Name, 'N/A') AS CompanyName,
    s.Site AS SiteName,

    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,

    -- 🔹 Asset Info (correlated subquery — no fan-out)
    (SELECT TOP 1 a.ModelNumber
     FROM dbo.SetItem si2
     INNER JOIN dbo.Renewals rn2 ON si2.ItemId = rn2.ItemId
     INNER JOIN dbo.Asset a ON rn2.AssetId = a.AssetId
     WHERE si2.SetId = s.SetId AND rn2.IsArchived = 0
     ORDER BY rn2.CreatedAt DESC) AS PartNumber,

    (SELECT TOP 1 a.SerialNumber
     FROM dbo.SetItem si2
     INNER JOIN dbo.Renewals rn2 ON si2.ItemId = rn2.ItemId
     INNER JOIN dbo.Asset a ON rn2.AssetId = a.AssetId
     WHERE si2.SetId = s.SetId AND rn2.IsArchived = 0
     ORDER BY rn2.CreatedAt DESC) AS AssetSerialNumber,

    ISNULL(s.Subtotal, 0) AS Subtotal,
    ISNULL(s.VatAmount, 0) AS VatAmount,
    ISNULL(s.WhtAmount, 0) AS WhtAmount,
    ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
    ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

    -- 🔹 Item counts (correlated subqueries — no fan-out)
    (SELECT COUNT(*) FROM dbo.SetItem si3 WHERE si3.SetId = s.SetId) AS ItemCount,

    (SELECT COUNT(*) FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId AND ISNULL(si3.RenewalStatus, 'Active') = 'Active') AS TotalActiveItems,

    (SELECT COUNT(*) FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Expired') AS ExpiredItemsCount,

    (SELECT COUNT(*) FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Renewed') AS RenewedItemsCount,

    (SELECT COUNT(*) FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Archived') AS ArchivedItemsCount,

    -- 🔹 Set-level status accounting for partial renewals
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId) = 0
            THEN 'No Items'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND ISNULL(RenewalStatus, 'Active') = 'Active') = 0
          AND (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND RenewalStatus = 'Renewed') > 0
            THEN 'Fully Renewed'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND RenewalStatus = 'Renewed') > 0
          AND (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND ISNULL(RenewalStatus, 'Active') = 'Active') > 0
            THEN 'Partially Renewed'
        WHEN s.EndDate IS NULL THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS SetLevelStatus,

    s.CreatedBy,
    s.CreatedAt AS CreatedDate

FROM dbo.[Set] s
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId

WHERE s.SetType IN ('Software/License', 'Service', 'Services');
GO

PRINT 'Migration complete.';
