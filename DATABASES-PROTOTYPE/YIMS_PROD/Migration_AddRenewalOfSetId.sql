-- Migration: Add RenewalOfSetId self-reference to dbo.[Set]
-- Purpose: Preserve original invoice Set unchanged when renewing;
--          each renewal creates a NEW Set linked via RenewalOfSetId.
-- Run once against the target database.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'RenewalOfSetId'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD [RenewalOfSetId] INT NULL;
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT FK_Set_RenewalOfSet
        FOREIGN KEY (RenewalOfSetId) REFERENCES dbo.[Set](SetId);
    PRINT 'Added RenewalOfSetId to dbo.[Set]';
END
ELSE
    PRINT 'RenewalOfSetId already exists — skipped.';
GO

-- Refresh the view to expose the new column
CREATE OR ALTER VIEW [dbo].[vw_RenewalStatus] AS
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
        WHEN eff.EffEndDate IS NULL THEN NULL
        ELSE DATEDIFF(DAY, GETDATE(), eff.EffEndDate)
    END AS DaysUntilExpiry,

    CASE
        WHEN eff.EffEndDate IS NULL THEN 'No Expiry Date'
        WHEN eff.EffEndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), eff.EffEndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), eff.EffEndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END AS ExpiryStatus,

    s.ComId,
    ISNULL(c.Name, 'N/A') AS CompanyName,
    s.Site AS SiteName,

    s.CurrentBranchId,
    ISNULL(b.Name, 'N/A') AS CurrentBranchName,
    s.CurrentDepartmentId,
    ISNULL(d.Name, 'N/A') AS CurrentDepartmentName,

    -- Asset Info: first PartNumber/SerialNumber from active renewals for this set
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

    -- Item counts (one row per Set — no fan-out from SetItem JOIN)
    (SELECT COUNT(*)
     FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId) AS ItemCount,

    (SELECT COUNT(*)
     FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId
       AND ISNULL(si3.RenewalStatus, 'Active') = 'Active') AS TotalActiveItems,

    (SELECT COUNT(*)
     FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId
       AND si3.RenewalStatus = 'Expired') AS ExpiredItemsCount,

    (SELECT COUNT(*)
     FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId
       AND si3.RenewalStatus = 'Renewed') AS RenewedItemsCount,

    (SELECT COUNT(*)
     FROM dbo.SetItem si3
     WHERE si3.SetId = s.SetId
       AND si3.RenewalStatus = 'Archived') AS ArchivedItemsCount,

    -- Set-level status accounting for partial renewals
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

    s.RenewalOfSetId,

    s.CreatedBy,
    s.CreatedAt AS CreatedDate

FROM dbo.[Set] s
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
LEFT JOIN dbo.Branch b ON s.CurrentBranchId = b.BranchId
LEFT JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId
CROSS APPLY (
    SELECT ISNULL(
        (SELECT TOP 1 s2.EndDate FROM dbo.[Set] s2
         WHERE s2.RenewalOfSetId = s.SetId ORDER BY s2.SetId DESC),
        s.EndDate
    ) AS EffEndDate
) eff

WHERE s.SetType IN ('Software/License', 'Service', 'Services');
GO
