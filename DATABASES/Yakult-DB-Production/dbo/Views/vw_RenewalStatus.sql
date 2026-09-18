 CREATE VIEW [dbo].[vw_RenewalStatus]
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

      -- DaysUntilExpiry:
      --   Superseded set (has a direct child renewal): frozen at child's StartDate so the
      --   countdown stops ticking once the set has been renewed.
      --   Active set (no child): live countdown against GETDATE().
      CASE
          WHEN s.EndDate IS NULL THEN NULL
          WHEN eff.RenewalStartDate IS NOT NULL
              THEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate)
          ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
      END AS DaysUntilExpiry,

      -- ExpiryStatus: same freeze logic as DaysUntilExpiry.
      CASE
          WHEN eff.RenewalStartDate IS NOT NULL THEN
              CASE
                  WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) < 0  THEN 'Expired'
                  WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 30 THEN 'Expiring Soon'
                  WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 90 THEN 'Warning'
                  ELSE 'Active'
              END
          WHEN s.EndDate IS NULL           THEN 'No Expiry Date'
          WHEN s.EndDate < GETDATE()       THEN 'Expired'
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

      -- Most recent renewal event recorded against any item still belonging to this Set
      -- (Renewals.ItemId keys off the item being renewed, i.e. the OLD Set's item — so this
      -- is where a superseded Set's own "when was it renewed" date actually lives).
      (SELECT TOP 1 rn2.RenewedDate
       FROM dbo.SetItem si2
       INNER JOIN dbo.Renewals rn2 ON si2.ItemId = rn2.ItemId
       WHERE si2.SetId = s.SetId AND rn2.IsArchived = 0
       ORDER BY rn2.CreatedAt DESC) AS RenewedDate,

      ISNULL(s.Subtotal, 0) AS Subtotal,
      ISNULL(s.VatAmount, 0) AS VatAmount,
      ISNULL(s.WhtAmount, 0) AS WhtAmount,
      ISNULL(s.DiscountAmount, 0) AS DiscountAmount,
      ISNULL(s.TotalAmountDue, 0) AS TotalAmountDue,

      (SELECT COUNT(*) FROM dbo.SetItem si3 WHERE si3.SetId = s.SetId) AS ItemCount,

      (SELECT COUNT(*) FROM dbo.SetItem si3
       WHERE si3.SetId = s.SetId AND ISNULL(si3.RenewalStatus, 'Active') = 'Active') AS TotalActiveItems,

      (SELECT COUNT(*) FROM dbo.SetItem si3
       WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Expired') AS ExpiredItemsCount,

      (SELECT COUNT(*) FROM dbo.SetItem si3
       WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Renewed') AS RenewedItemsCount,

      (SELECT COUNT(*) FROM dbo.SetItem si3
       WHERE si3.SetId = s.SetId AND si3.RenewalStatus = 'Archived') AS ArchivedItemsCount,

      CASE
          WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId) = 0
              THEN 'No Items'
          WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND ISNULL(RenewalStatus, 'Active') = 'Active') = 0
            AND (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND RenewalStatus = 'Renewed') > 0
              THEN 'Fully Renewed'
          WHEN (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND RenewalStatus = 'Renewed') > 0
            AND (SELECT COUNT(*) FROM dbo.SetItem WHERE SetId = s.SetId AND ISNULL(RenewalStatus, 'Active') = 'Active') > 
  0
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
      SELECT
          ISNULL(
              (SELECT TOP 1 s2.EndDate FROM dbo.[Set] s2
               WHERE s2.RenewalOfSetId = s.SetId ORDER BY s2.SetId DESC),
              s.EndDate
          ) AS EffEndDate,
          -- RenewalStartDate: StartDate of the earliest direct child, used to freeze the
          -- countdown for superseded sets at the moment the renewal was linked.
          (SELECT TOP 1 s2.StartDate FROM dbo.[Set] s2
           WHERE s2.RenewalOfSetId = s.SetId ORDER BY s2.SetId ASC) AS RenewalStartDate
  ) eff

  WHERE s.SetType IN ('Software/License', 'Service', 'Services');