-- ==========================================================================
-- VIEW: vw_Report_RenewalGroups
-- PURPOSE: Reporting view for the "View Renewals (Grouped)" module.
--          Uses a recursive CTE to build the full renewal chain:
--            Original Set  → Renewal 1 → Renewal 2 → …
--
--          Each row includes:
--            • RootSetId  — identifies the chain (original Set)
--            • ChainLevel — 0 = Original, 1 = first renewal, …
--            • ChainRole  — human-readable label ("Original", "Renewal 1", …)
--
--          Grouping date (Year / MonthNumber / MonthName) is anchored
--          to the ROOT set's document date so that the entire chain
--          appears under a single Year → Month bucket.
--
-- SET TYPES: Software/License, Service, Services
-- ==========================================================================

CREATE VIEW [dbo].[vw_Report_RenewalGroups]
AS
WITH RenewalChain AS
(
    -- ── Anchor: root Sets that have NO parent (original Sets) ─────────────
    SELECT
        s.SetId,
        s.SetId                          AS RootSetId,
        0                                AS ChainLevel,
        CAST(N'Original' AS NVARCHAR(50)) AS ChainRole
    FROM dbo.[Set] s
    WHERE s.RenewalOfSetId IS NULL
      AND s.SetType IN ('Software/License', 'Service', 'Services')

    UNION ALL

    -- ── Recursive: renewal Sets that point to a parent ────────────────────
    SELECT
        s.SetId,
        rc.RootSetId,
        rc.ChainLevel + 1,
        CAST(N'Renewal ' + CAST(rc.ChainLevel + 1 AS NVARCHAR(5)) AS NVARCHAR(50))
    FROM dbo.[Set] s
    INNER JOIN RenewalChain rc ON s.RenewalOfSetId = rc.SetId
)
SELECT
    -- ── Grouping columns anchored to the ROOT set's date (never NULL) ─────
    YEAR(ISNULL(root.DispatchDate,  root.CreatedAt))            AS [Year],
    MONTH(ISNULL(root.DispatchDate, root.CreatedAt))            AS MonthNumber,
    DATENAME(MONTH, ISNULL(root.DispatchDate, root.CreatedAt))  AS MonthName,

    -- ── Chain identity ────────────────────────────────────────────────────
    rc.RootSetId,
    rc.ChainLevel,
    rc.ChainRole,

    -- ── Current Set in the chain ──────────────────────────────────────────
    s.SetId,
    s.SetCode,
    ISNULL(s.SetType, 'N/A')                                    AS SetType,
    CONVERT(date, ISNULL(s.DispatchDate, s.CreatedAt))          AS DocumentDate,
    ISNULL(s.DocumentNumber,  '')                               AS DocumentNumber,
    ISNULL(s.ReferenceNumber, '')                               AS ReferenceNumber,
    ISNULL(s.Status,          'Pending')                        AS Status,
    ISNULL(s.Remarks,         '')                               AS Remarks,

    -- ── Period ────────────────────────────────────────────────────────────
    CONVERT(date, s.StartDate)                                  AS StartDate,
    CONVERT(date, s.EndDate)                                    AS EndDate,

    -- ── Expiry ────────────────────────────────────────────────────────────
    CASE
        WHEN s.EndDate IS NULL          THEN 'No Expiry Date'
        WHEN s.EndDate < GETDATE()      THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 90 THEN 'Warning'
        ELSE 'Active'
    END                                                         AS ExpiryStatus,

    DATEDIFF(DAY, GETDATE(), s.EndDate)                         AS DaysUntilExpiry,

    -- ── Company / Location ────────────────────────────────────────────────
    ISNULL(c.Name, 'N/A')                                       AS CompanyName,
    ISNULL(s.Site, '')                                          AS SiteName,
    ISNULL(b.Name, 'N/A')                                       AS CurrentBranchName,
    ISNULL(d.Name, 'N/A')                                       AS CurrentDepartmentName,

    -- ── Financials ────────────────────────────────────────────────────────
    ISNULL(s.Subtotal,       0)                                 AS Subtotal,
    ISNULL(s.TotalAmountDue, 0)                                 AS TotalAmountDue,

    -- ── Parent reference ──────────────────────────────────────────────────
    s.RenewalOfSetId,

    -- ── Root Set reference code (for display) ─────────────────────────────
    root.SetCode                                                AS RootSetCode,

    -- ── Audit ─────────────────────────────────────────────────────────────
    s.CreatedBy,
    CONVERT(date, s.CreatedAt)                                  AS CreatedDate

FROM RenewalChain rc
INNER JOIN dbo.[Set]   s    ON rc.SetId     = s.SetId
INNER JOIN dbo.[Set]   root ON rc.RootSetId = root.SetId
LEFT  JOIN dbo.Company c    ON s.ComId               = c.ComId
LEFT  JOIN dbo.Branch  b    ON s.CurrentBranchId     = b.BranchId
LEFT  JOIN dbo.Department d ON s.CurrentDepartmentId = d.DeptId;
