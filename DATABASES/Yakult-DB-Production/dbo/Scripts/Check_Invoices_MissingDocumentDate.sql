-- Diagnostic query (read-only): lists invoices (dbo.[Set] rows with IsInvoice = 1)
-- that have no Document Date set. DocumentDate on the Invoice/Renewals pages is
-- sourced from dbo.[Set].DispatchDate (see dbo.vw_InvoiceItems), so "missing
-- Document Date" means DispatchDate IS NULL.
--
-- Queries dbo.[Set] directly rather than vw_InvoiceItems/vw_InvoiceItems to avoid
-- one row per line item — each invoice appears exactly once here.

SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.DocumentNumber,
    s.ReferenceNumber,
    s.Status,
    s.StartDate,
    s.EndDate,
    co.Name AS CompanyName,
    s.CreatedAt,
    s.CreatedBy
FROM dbo.[Set] s
LEFT JOIN dbo.Company co ON s.ComId = co.ComId
WHERE s.IsInvoice = 1
  AND s.DispatchDate IS NULL
ORDER BY s.CreatedAt DESC;
