-- Adds an explicit "employee this invoice is for" column to dbo.[Set].
--
-- Background: an invoice recorded from a Request-based dispatch Set only knew its requester
-- indirectly, through Set.ReqId -> dbo.Request -> dbo.Employee. Invoices are edited on
-- ViewInvoiceDetailPage as documents in their own right, so the requester needs to be stored
-- directly on the Set: editable independently of the originating Request, and settable on
-- invoice Sets that never came from a Request at all.
--
-- Idempotent: safe to run more than once.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceRequesterEmpId'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD InvoiceRequesterEmpId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Set_InvoiceRequester'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT FK_Set_InvoiceRequester FOREIGN KEY (InvoiceRequesterEmpId)
        REFERENCES dbo.Employee (EmpId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'IX_Set_InvoiceRequesterEmpId'
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Set_InvoiceRequesterEmpId
        ON dbo.[Set] (InvoiceRequesterEmpId ASC)
        WHERE (InvoiceRequesterEmpId IS NOT NULL);
END
GO

-- Backfill existing invoice Sets from their originating Request's employee. Idempotent:
-- only fills rows that are still NULL, so re-running is a no-op.
IF EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceRequesterEmpId'
)
BEGIN
    UPDATE s
        SET s.InvoiceRequesterEmpId = rq.EmpId
    FROM dbo.[Set] s
    INNER JOIN dbo.Request rq ON rq.ReqId = s.ReqId
    WHERE ISNULL(s.IsInvoice, 0) = 1
      AND s.InvoiceRequesterEmpId IS NULL
      AND rq.EmpId IS NOT NULL;
END
GO
