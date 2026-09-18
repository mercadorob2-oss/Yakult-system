-- ==========================================================================
-- VIEW: vw_Report_Renewals
-- PURPOSE: Reporting view for the "View Renewals" module.
--          Wraps vw_RenewalStatus and adds Year / MonthNumber / MonthName
--          for RDLC Year → Month grouping.
-- DATE ANCHOR: COALESCE(DocumentDate, CreatedDate) — never NULL.
-- SET TYPES: Software/License, Service, Services  (inherited from vw_RenewalStatus)
-- ==========================================================================

CREATE VIEW [dbo].[vw_Report_Renewals]
AS
SELECT
    -- ── Grouping columns (never NULL) ─────────────────────────────────────
    YEAR(ISNULL(rs.DocumentDate, rs.CreatedDate))                AS [Year],
    MONTH(ISNULL(rs.DocumentDate, rs.CreatedDate))               AS MonthNumber,
    DATENAME(MONTH, ISNULL(rs.DocumentDate, rs.CreatedDate))     AS MonthName,

    -- ── Set identity ──────────────────────────────────────────────────────
    rs.SetId,
    rs.SetCode,
    rs.SetType,
    ISNULL(rs.DocumentDate, rs.CreatedDate)                      AS DocumentDate,
    ISNULL(rs.DocumentNumber,  '')                               AS DocumentNumber,
    ISNULL(rs.ReferenceNumber, '')                               AS ReferenceNumber,
    ISNULL(rs.Status,          '')                               AS Status,
    ISNULL(rs.Remarks,         '')                               AS Remarks,

    -- ── Renewal period ────────────────────────────────────────────────────
    rs.StartDate,
    rs.EndDate,
    rs.DaysUntilExpiry,
    rs.ExpiryStatus,
    rs.SetLevelStatus,

    -- ── Company / Location ────────────────────────────────────────────────
    rs.CompanyName,
    rs.SiteName,
    rs.CurrentBranchName,
    rs.CurrentDepartmentName,

    -- ── Asset reference ───────────────────────────────────────────────────
    ISNULL(rs.PartNumber,         '')                            AS PartNumber,
    ISNULL(rs.AssetSerialNumber,  '')                            AS AssetSerialNumber,

    -- ── Financials ────────────────────────────────────────────────────────
    rs.Subtotal,
    rs.VatAmount,
    rs.WhtAmount,
    rs.DiscountAmount,
    rs.TotalAmountDue,

    -- ── Item counts ───────────────────────────────────────────────────────
    rs.ItemCount,
    rs.TotalActiveItems,
    rs.ExpiredItemsCount,
    rs.RenewedItemsCount,
    rs.ArchivedItemsCount,

    -- ── Renewal chain reference ───────────────────────────────────────────
    rs.RenewalOfSetId,

    -- ── Audit ─────────────────────────────────────────────────────────────
    rs.CreatedBy,
    rs.CreatedDate

FROM dbo.vw_RenewalStatus rs

-- Only include records that have a valid anchor date for grouping
WHERE ISNULL(rs.DocumentDate, rs.CreatedDate) IS NOT NULL;
