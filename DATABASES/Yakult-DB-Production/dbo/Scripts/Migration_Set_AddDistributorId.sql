-- Migration: Add DistributorId to dbo.Set
-- Used by the Sales Invoice Set dialog (Distributor picker, replaces Branch selection)
-- and ViewInvoiceDetailPage (Distributor dropdown beside Company).

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'DistributorId'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD DistributorId INT NULL;
END

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Set_Distributor'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT FK_Set_Distributor FOREIGN KEY (DistributorId)
        REFERENCES dbo.[Distributor] (DistributorId);
END
