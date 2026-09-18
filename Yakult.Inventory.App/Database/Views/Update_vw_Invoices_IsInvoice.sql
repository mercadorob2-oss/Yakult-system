-- =============================================
-- Update vw_Invoices to use IsInvoice flag
-- Date: 2026-02-09
-- =============================================
-- This view now filters by IsInvoice = 1 instead of SetType
-- Supports Hardware, Software, Service, and mixed-item invoices
-- =============================================

IF OBJECT_ID('dbo.vw_Invoices', 'V') IS NOT NULL
    DROP VIEW dbo.vw_Invoices
GO

CREATE VIEW [dbo].[vw_Invoices]
AS
SELECT
    s.SetId,
    s.SetCode,
    s.SetType,
    s.Status,
    s.DispatchDate AS InvoiceDate,
    u.Name AS PreparedByName,
    s.DocumentNumber AS InvoiceNumber,
    s.ReferenceNumber AS PONumber,
    s.Site,
    s.StartDate,
    s.EndDate,
    s.Subtotal,
    s.VatAmount,
    s.WhtAmount,
    s.DiscountAmount,
    s.TotalAmountDue,
    s.ComId,
    c.Name AS CompanyName,
    c.Description AS CompanyDescription,
    s.Remarks,
    DATEDIFF(DAY, GETDATE(), s.EndDate) AS DaysUntilExpiry,
    CASE
        WHEN s.EndDate < GETDATE() THEN 'Expired'
        WHEN DATEDIFF(DAY, GETDATE(), s.EndDate) <= 30 THEN 'Expiring Soon'
        ELSE 'Active'
    END AS ExpiryStatus
FROM dbo.[Set] s
LEFT JOIN dbo.[User] u ON s.CreatedBy = u.UserId
LEFT JOIN dbo.Company c ON s.ComId = c.ComId
WHERE s.IsInvoice = 1;
GO

PRINT 'vw_Invoices view recreated successfully!'
PRINT 'Key changes:'
PRINT '1. Now filters by IsInvoice = 1 instead of SetType'
PRINT '2. Supports Hardware, Software, Service, and mixed-item invoices'
PRINT ''

-- Test the view
SELECT TOP 10
    SetId,
    InvoiceNumber,
    CompanyName,
    InvoiceDate,
    SetType,
    Status,
    TotalAmountDue
FROM vw_Invoices
ORDER BY InvoiceDate DESC

PRINT ''
PRINT 'If results show Hardware, Software, and Service invoices, the update is successful!'
