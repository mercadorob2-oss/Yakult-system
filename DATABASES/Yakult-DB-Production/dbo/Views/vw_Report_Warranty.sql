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
--   • Hardware / Services: must have WarrantyYears > 0, a Set EndDate, OR an
--     Item EndDate (e.g. a Hardware item tagged with a Sub-Type via
--     BatchAddItemDialog/EditItemDialog and given its own Start/End Date —
--     see Migration_Item_AddSubType.sql).
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
        COALESCE(s.StartDate, i.StartDate, i.WarrantyStartDate, i.DateCreated)
    )                                                           AS WarrantyStartDate,

    CONVERT(date,
        COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate)
    )                                                           AS WarrantyEndDate,

    i.WarrantyYears,

    -- ── Days remaining (negative = expired) ──────────────────────────────
    DATEDIFF(DAY, GETDATE(),
        COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate)
    )                                                           AS DaysRemaining,

    -- ── Status ────────────────────────────────────────────────────────────
    CASE
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate)) < 0
            THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate)) <= 30
            THEN 'Critical'
        WHEN DATEDIFF(DAY, GETDATE(), COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate)) <= 90
            THEN 'Warning'
        ELSE 'Active'
    END                                                         AS WarrantyStatus,

    -- ── Grouping columns anchored to WarrantyEndDate (never NULL here) ────
    YEAR(COALESCE(s.EndDate,  i.EndDate, i.WarrantyEndDate))               AS [Year],
    MONTH(COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate))               AS MonthNumber,
    DATENAME(MONTH, COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate))     AS MonthName

FROM dbo.Item i
LEFT JOIN dbo.Vendor   v  ON i.VendorId = v.VendorId
-- OUTER APPLY (not a plain JOIN) so an item that is now part of more than one
-- invoice Set (e.g. renewed into a new Set via RenewalOfSetId) contributes only
-- its single most recent invoice Set, not one row per Set.
OUTER APPLY (
    SELECT TOP (1) s2.SetId, s2.SetCode, s2.StartDate, s2.EndDate
    FROM dbo.SetItem si2
    INNER JOIN dbo.[Set] s2 ON s2.SetId = si2.SetId AND s2.IsInvoice = 1
    WHERE si2.ItemId = i.ItemId
    ORDER BY s2.SetId DESC
) s

WHERE
    i.Active = 1
    AND COALESCE(s.EndDate, i.EndDate, i.WarrantyEndDate) IS NOT NULL
    AND
    (
        -- Software/License: must have an end date somewhere
        (i.ItemType = 'Software/License'
            AND (s.EndDate IS NOT NULL OR i.EndDate IS NOT NULL))

        -- Hardware / Services: must have warranty years, a set end date, or its
        -- own Item.EndDate (Hardware tagged with a Sub-Type)
        OR (ISNULL(i.ItemType, '') <> 'Software/License'
            AND (i.WarrantyYears > 0 OR s.EndDate IS NOT NULL OR i.EndDate IS NOT NULL))
    );
