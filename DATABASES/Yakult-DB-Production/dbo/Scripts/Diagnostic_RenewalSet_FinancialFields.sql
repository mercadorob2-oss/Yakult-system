-- ==========================================================================
-- SCRIPT: Diagnostic_RenewalSet_FinancialFields.sql
-- PURPOSE: Identify the Set and its financial field values (Subtotal,
--          VatAmount, WhtAmount, DiscountAmount, TotalAmountDue) for the
--          3 renewal items visible in the report:
--            - Duo sub Cisco Subscription
--            - Duo-Beyond Cisco Standard Duo Beyond Edition
--            - Svs-Duo-Sup-B Cisco Duo Basic Support
--
--          Also checks whether the financial columns exist on dbo.[Set]
--          (if they don't, that explains the #Error in the RDLC report).
-- DATE:    2026-03-23
-- ==========================================================================

-- --------------------------------------------------------------------------
-- PART 1: Check whether the financial columns exist on dbo.[Set]
--         If any row is missing here, the ALTER VIEW will fail too.
-- --------------------------------------------------------------------------
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA = 'dbo'
  AND TABLE_NAME   = 'Set'
  AND COLUMN_NAME  IN ('Subtotal', 'VatAmount', 'WhtAmount', 'DiscountAmount', 'TotalAmountDue')
ORDER BY COLUMN_NAME;

-- --------------------------------------------------------------------------
-- PART 2: Find the Set(s) that contain the 3 items, and show all financial
--         field values directly from dbo.[Set].
-- --------------------------------------------------------------------------
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber,
    ISNULL(com.Name, 'N/A')   AS CompanyName,
    s.TotalAmountDue,
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.StartDate,
    s.EndDate
FROM dbo.[Set] s
LEFT JOIN dbo.Company com ON com.ComId = s.ComId
WHERE s.SetId IN
(
    SELECT DISTINCT si.SetId
    FROM dbo.SetItem si
    INNER JOIN dbo.Item i ON i.ItemId = si.ItemId
    WHERE i.[Name] IN (
        'Duo sub Cisco Subscription',
        'Duo-Beyond Cisco Standard Duo Beyond Edition',
        'Svs-Duo-Sup-B Cisco Duo Basic Support'
    )
)
ORDER BY s.SetId;

-- --------------------------------------------------------------------------
-- PART 3: Show the individual SetItem amounts for those same sets,
--         so we can see why ItemAmount shows 0.00 in the report.
-- --------------------------------------------------------------------------
SELECT
    s.SetId,
    s.SetCode,
    i.[Name]                  AS ItemName,
    si.Quantity,
    si.UnitPrice,
    si.Amount                 AS ItemAmount,
    si.LineStartDate,
    si.LineEndDate,
    si.RenewalStatus
FROM dbo.[Set] s
INNER JOIN dbo.SetItem si ON si.SetId  = s.SetId
INNER JOIN dbo.Item    i  ON i.ItemId  = si.ItemId
WHERE s.SetId IN
(
    SELECT DISTINCT si2.SetId
    FROM dbo.SetItem si2
    INNER JOIN dbo.Item i2 ON i2.ItemId = si2.ItemId
    WHERE i2.[Name] IN (
        'Duo sub Cisco Subscription',
        'Duo-Beyond Cisco Standard Duo Beyond Edition',
        'Svs-Duo-Sup-B Cisco Duo Basic Support'
    )
)
ORDER BY s.SetId, si.SetItemId;
