-- ==========================================================================
-- VIEW: vw_Report_RenewalGroupsLatest
-- PURPOSE: Flat reporting view for the Renewals (Grouped) report.
--          Shows ONLY the latest (highest ChainLevel) set per renewal chain,
--          expanded to one row per SetItem.
--          "Renewed From" column shows the SetCode of the immediate parent
--          set (via RenewalOfSetId) so history is visible without flooding
--          the report with every historical set.
-- GROUPING: Year / Month anchored to the latest set's own coverage
--           StartDate, falling back to document/creation date when unset.
-- ==========================================================================

CREATE VIEW [dbo].[vw_Report_RenewalGroupsLatest]
AS
WITH RenewalChain AS
(
    -- Anchor: original sets (no parent)
    SELECT
        s.SetId,
        s.SetId  AS RootSetId,
        0        AS ChainLevel
    FROM dbo.[Set] s
    WHERE s.RenewalOfSetId IS NULL
      AND s.SetType IN ('Software/License', 'Service', 'Services')

    UNION ALL

    -- Recursive: renewal sets
    SELECT
        s.SetId,
        rc.RootSetId,
        rc.ChainLevel + 1
    FROM dbo.[Set] s
    INNER JOIN RenewalChain rc ON s.RenewalOfSetId = rc.SetId
),
LatestPerChain AS
(
    SELECT RootSetId, MAX(ChainLevel) AS MaxLevel
    FROM RenewalChain
    GROUP BY RootSetId
),
LatestSets AS
(
    SELECT rc.SetId, rc.RootSetId, rc.ChainLevel
    FROM RenewalChain rc
    INNER JOIN LatestPerChain lpc
        ON rc.RootSetId = lpc.RootSetId
       AND rc.ChainLevel = lpc.MaxLevel
)
SELECT
    -- ── Grouping columns (anchored to the latest set's own coverage
    --    StartDate, falling back to DispatchDate/CreatedAt when unset) ─────
    YEAR  (ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt)))         AS [Year],
    MONTH (ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt)))         AS MonthNumber,
    DATENAME(MONTH, ISNULL(s.StartDate, ISNULL(s.DispatchDate, s.CreatedAt))) AS MonthName,

    -- ── Chain info ────────────────────────────────────────────────────────
    ls.RootSetId,
    root.SetCode                                                 AS RootSetCode,
    ls.ChainLevel                                                AS RenewalCount,

    -- ── Latest set columns (show once per set via IIF in RDLC) ───────────
    s.SetId,
    s.SetCode,
    s.SetType,
    ISNULL(s.DispatchDate, s.CreatedAt)                          AS DocumentDate,
    ISNULL(s.DocumentNumber, '')                                 AS DocumentNumber,
    -- SetCode of the immediate parent (the set this one renews)
    ISNULL(prev.SetCode, '—')                                    AS RenewedFromSetCode,
    ISNULL(com.Name, 'N/A')                                      AS CompanyName,
    ISNULL(s.Site, '')                                           AS SiteName,

    -- ExpiryStatus
    CASE
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
    ISNULL(i.Description,    '')                                 AS ItemDescription,
    ISNULL(i.ModelNumber,  '')                                   AS ModelNumber,
    ISNULL(i.SerialNumber, '')                                   AS SerialNumber,
    i.ItemType,
    ISNULL(i.Category,     '')                                   AS Category,
    ISNULL(si.Quantity,    0)                                    AS Quantity,
    ISNULL(si.Amount,      0)                                    AS ItemAmount,
    si.LineStartDate,
    si.LineEndDate,
    ISNULL(si.RenewalStatus, 'Active')                           AS ItemRenewalStatus

FROM LatestSets ls
INNER JOIN dbo.[Set]      s    ON s.SetId    = ls.SetId
INNER JOIN dbo.[Set]      root ON root.SetId = ls.RootSetId
LEFT  JOIN dbo.[Set]      prev ON prev.SetId = s.RenewalOfSetId
LEFT  JOIN dbo.Company    com  ON com.ComId  = s.ComId
INNER JOIN dbo.SetItem    si   ON si.SetId   = s.SetId
INNER JOIN dbo.Item       i    ON i.ItemId   = si.ItemId;
