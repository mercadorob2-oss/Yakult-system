-- ==========================================================================
-- SCRIPT: AlterView_vw_Report_RenewalsWithItems_AddFinancialFields.sql
-- PURPOSE: Adds Subtotal, VatAmount, WhtAmount, DiscountAmount columns
--          from dbo.[Set] to support the Renewals Report summary rows
--          (Subtotal / VAT / WHT / Discount / Total Amount footer per set).
-- DATE:    2026-03-23
-- ==========================================================================

ALTER VIEW [dbo].[vw_Report_RenewalsWithItems]
AS
SELECT
    -- ── Grouping / banner columns ─────────────────────────────────────────
    YEAR  (ISNULL(s.DispatchDate, s.CreatedAt))                  AS [Year],
    MONTH (ISNULL(s.DispatchDate, s.CreatedAt))                  AS MonthNumber,
    DATENAME(MONTH, ISNULL(s.DispatchDate, s.CreatedAt))         AS MonthName,

    -- ── Set-level columns ─────────────────────────────────────────────────
    s.SetId,
    s.SetCode,
    s.SetType,
    ISNULL(s.DispatchDate, s.CreatedAt)                          AS DocumentDate,
    ISNULL(s.DocumentNumber, '')                                 AS DocumentNumber,
    ISNULL(com.Name, 'N/A')                                      AS CompanyName,
    ISNULL(s.Site, '')                                           AS SiteName,

    -- ExpiryStatus: freeze countdown if set has been renewed (child exists)
    CASE
        WHEN eff.RenewalStartDate IS NOT NULL THEN
            CASE
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) < 0   THEN 'Expired'
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 30 THEN 'Expiring Soon'
                WHEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate) <= 90 THEN 'Warning'
                ELSE 'Active'
            END
        WHEN s.EndDate IS NULL              THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()          THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END                                                          AS ExpiryStatus,

    -- SetLevelStatus
    CASE
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId) = 0
            THEN 'No Items'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND ISNULL(sx.RenewalStatus,'Active') = 'Active') = 0
         AND (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND sx.RenewalStatus = 'Renewed') > 0
            THEN 'Fully Renewed'
        WHEN (SELECT COUNT(*) FROM dbo.SetItem sx WHERE sx.SetId = s.SetId AND sx.RenewalStatus = 'Renewed') > 0
            THEN 'Partially Renewed'
        WHEN s.EndDate IS NULL              THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()          THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END                                                          AS SetLevelStatus,

    s.StartDate,
    s.EndDate,
    CASE
        WHEN s.EndDate IS NULL THEN NULL
        WHEN eff.RenewalStartDate IS NOT NULL
            THEN DATEDIFF(DAY, eff.RenewalStartDate, s.EndDate)
        ELSE DATEDIFF(DAY, GETDATE(), s.EndDate)
    END                                                          AS DaysUntilExpiry,
    ISNULL(s.TotalAmountDue,  0)                                 AS TotalAmountDue,
    ISNULL(s.Subtotal,        0)                                 AS Subtotal,
    ISNULL(s.VatAmount,       0)                                 AS VatAmount,
    ISNULL(s.WhtAmount,       0)                                 AS WhtAmount,
    ISNULL(s.DiscountAmount,  0)                                 AS DiscountAmount,

    -- Per-set item sequence (resets to 1 for each SetCode)
    ROW_NUMBER() OVER (
        PARTITION BY s.SetCode
        ORDER BY si.SetItemId
    )                                                            AS ItemNo,

    -- ── Item-level columns ────────────────────────────────────────────────
    si.SetItemId,
    i.[Name]                                                     AS ItemName,
    ISNULL(i.ModelNumber,  '')                                   AS ModelNumber,
    ISNULL(i.SerialNumber, '')                                   AS SerialNumber,
    i.ItemType,
    ISNULL(i.Category,     '')                                   AS Category,
    ISNULL(si.Quantity,    0)                                    AS Quantity,
    ISNULL(si.UnitPrice,   0)                                    AS UnitPrice,
    ISNULL(si.Amount,      0)                                    AS ItemAmount,
    si.LineStartDate,
    si.LineEndDate,
    ISNULL(si.RenewalStatus, 'Active')                           AS ItemRenewalStatus

FROM dbo.[Set]      s
LEFT  JOIN dbo.Company  com ON com.ComId  = s.ComId
INNER JOIN dbo.SetItem  si  ON si.SetId   = s.SetId
INNER JOIN dbo.Item     i   ON i.ItemId   = si.ItemId
CROSS APPLY (
    SELECT
        (SELECT TOP 1 s2.StartDate
         FROM dbo.[Set] s2
         WHERE s2.RenewalOfSetId = s.SetId
         ORDER BY s2.SetId ASC) AS RenewalStartDate
) eff

WHERE s.SetType IN ('Software/License', 'Service', 'Services');
