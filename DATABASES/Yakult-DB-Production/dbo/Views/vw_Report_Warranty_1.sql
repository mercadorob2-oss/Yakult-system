-- ==========================================================================
-- VIEW: vw_Report_Warranty
-- PURPOSE: Reporting view for the "View Warranty" module.
--          Mirrors the live query in ViewWarrantyPage.cs but adds
--          Year / MonthNumber / MonthName anchored to WarrantyEndDate
--          so the RDLC can group by "warranty expiry period."
--
-- DATE ANCHOR: COALESCE(Set.EndDate, Item.WarrantyEndDate)
--   — records with no effective end date are excluded.
--
-- COVERAGE:
--   • Software/License items: must have a Set EndDate OR an Item EndDate.
--   • Hardware / Services: must have WarrantyYears > 0 OR a Set EndDate.
-- ==========================================================================

CREATE VIEW [dbo].[vw_Report_Warranty]
AS
SELECT
    i.ItemId,
    i.Name                                                      AS ItemName,
    ISNULL(i.ItemType, 'Hardware')                              AS ItemType,
    ISNULL(i.SerialNumber, '')                                  AS SerialNumber,
    ISNULL(i.ModelNumber,  '')                                  AS ModelNumber,
    ISNULL(v.VendorName,   'N/A')                               AS VendorName,
    ISNULL(s.SetCode,      N'—')                                AS SetCode,

    -- ── Effective warranty dates ──────────────────────────────────────────
    --   Set dates override Item-level dates (Set represents the invoice contract).
    CONVERT(date,
        COALESCE(s.StartDate, i.WarrantyStartDate, i.DateCreated)
    )                                                           AS WarrantyStartDate,

    CONVERT(date,
        COALESCE(s.EndDate, i.WarrantyEndDate)
    )                                                           AS WarrantyEndDate,

    i.WarrantyYears,

    -- ── Days remaining (negative = expired) ──────────────────────────────
    DATEDIFF(DAY, GETDATE(),
        COALESCE(s.EndDate, i.WarrantyEndDate)
    )                                                           AS DaysRemaining,

    -- ── Status ────────────────────────────────────────────────────────────
    CASE
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) < 0
            THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) <= 30
            THEN 'Critical'
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.WarrantyEndDate)) <= 90
            THEN 'Warning'
        ELSE 'Active'
    END                                                         AS WarrantyStatus,

    -- ── Grouping columns anchored to WarrantyEndDate (never NULL here) ────
    YEAR(COALESCE(s.EndDate,  i.WarrantyEndDate))               AS [Year],
    MONTH(COALESCE(s.EndDate, i.WarrantyEndDate))               AS MonthNumber,
    DATENAME(MONTH, COALESCE(s.EndDate, i.WarrantyEndDate))     AS MonthName

FROM dbo.Item i
LEFT JOIN dbo.Vendor   v  ON i.VendorId = v.VendorId
LEFT JOIN dbo.SetItem  si ON i.ItemId   = si.ItemId
-- Only join the most recent Invoice Set for this item
LEFT JOIN dbo.[Set]    s  ON si.SetId   = s.SetId
                          AND s.IsInvoice = 1

WHERE
    i.Active = 1
    AND COALESCE(s.EndDate, i.WarrantyEndDate) IS NOT NULL
    AND
    (
        -- Software/License: must have an end date somewhere
        (i.ItemType = 'Software/License'
            AND (s.EndDate IS NOT NULL OR i.EndDate IS NOT NULL))

        -- Hardware / Services: must have warranty years OR a set end date
        OR (ISNULL(i.ItemType, '') <> 'Software/License'
            AND (i.WarrantyYears > 0 OR s.EndDate IS NOT NULL))
    );