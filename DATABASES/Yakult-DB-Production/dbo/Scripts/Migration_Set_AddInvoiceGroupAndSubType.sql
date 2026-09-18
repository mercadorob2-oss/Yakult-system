-- Adds the new Invoice Group / Invoice Sub-Type link columns to dbo.[Set].
-- Both nullable: existing invoices stay ungrouped/untyped. "Mandatory for new invoices"
-- is enforced at the application layer (SoftwareServiceSetDialog / InvoiceRepository /
-- QuickCreateInvoicePage / InvoiceImportDialog validation), not via NOT NULL here, so this
-- migration never breaks historical rows.
--
-- GO separators are required between each ADD COLUMN and the statements that reference
-- that column (constraints/indexes) — SQL Server binds a batch's column references
-- against the schema as of the start of the batch, so a column added earlier in the same
-- batch is not yet visible without a batch break.
IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceGroupId'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD InvoiceGroupId INT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Set_InvoiceGroup'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT FK_Set_InvoiceGroup FOREIGN KEY (InvoiceGroupId) REFERENCES dbo.InvoiceGroup (InvoiceGroupId);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.[Set]') AND name = 'InvoiceSubType'
)
BEGIN
    ALTER TABLE dbo.[Set] ADD InvoiceSubType NVARCHAR (50) NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.check_constraints WHERE name = 'CK_Set_InvoiceSubType'
)
BEGIN
    ALTER TABLE dbo.[Set]
        ADD CONSTRAINT CK_Set_InvoiceSubType CHECK (
            InvoiceSubType IN ('Contract', 'Subscription', 'License', 'Services')
            OR InvoiceSubType IS NULL
        );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_Set_InvoiceGroupId' AND object_id = OBJECT_ID('dbo.[Set]')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_Set_InvoiceGroupId
        ON dbo.[Set] (InvoiceGroupId ASC)
        INCLUDE (SetCode, InvoiceSubType);
END
GO
