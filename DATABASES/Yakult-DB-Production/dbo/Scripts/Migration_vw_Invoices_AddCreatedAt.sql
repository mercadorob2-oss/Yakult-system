ALTER VIEW [dbo].[vw_Invoices]
AS
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.Status,

    CAST(
        s.DispatchDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS InvoiceDate,

    u.Name AS PreparedByName,
    s.DocumentNumber AS InvoiceNumber,
    s.ReferenceNumber AS PONumber,
    s.Site,

    CAST(
        s.StartDate AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS StartDate,

    CAST(
        s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time'
        AS datetime
    ) AS EndDate,

    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,

    s.ComId,
    c.Name AS CompanyName,
    c.Description AS CompanyDescription,
    s.Remarks,
    s.CreatedAt,

    DATEDIFF(
        DAY,
        CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                         AT TIME ZONE 'Singapore Standard Time' AS datetime),
        CAST(s.EndDate AT TIME ZONE 'UTC'
                       AT TIME ZONE 'Singapore Standard Time' AS datetime)
    ) AS DaysUntilExpiry,

    CASE
        WHEN CAST(s.EndDate AT TIME ZONE 'UTC'
                   AT TIME ZONE 'Singapore Standard Time' AS datetime)
             < CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                     AT TIME ZONE 'Singapore Standard Time' AS datetime)
        THEN 'Expired'

        WHEN DATEDIFF(
                DAY,
                CAST(GETUTCDATE() AT TIME ZONE 'UTC'
                                 AT TIME ZONE 'Singapore Standard Time' AS datetime),
                CAST(s.EndDate AT TIME ZONE 'UTC'
                               AT TIME ZONE 'Singapore Standard Time' AS datetime)
             ) <= 30
        THEN 'Expiring Soon'

        ELSE 'Active'
    END AS ExpiryStatus

FROM dbo.[Set] s
LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
WHERE s.IsInvoice = 1;
