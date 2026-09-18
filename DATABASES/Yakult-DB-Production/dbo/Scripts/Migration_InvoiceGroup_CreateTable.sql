-- Creates dbo.InvoiceGroup, the parent bundle every invoice (dbo.[Set] with IsInvoice = 1)
-- can belong to. InvoiceGroupNum is user-typed free text (not a generated code), and is
-- unique so the app can resolve "type an existing number" back to the same group row.
IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'InvoiceGroup' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.InvoiceGroup (
        InvoiceGroupId   INT IDENTITY (1, 1) NOT NULL,
        InvoiceGroupNum  NVARCHAR (100)      NOT NULL,
        ComId            INT                 NULL,
        Remarks          NVARCHAR (400)      NULL,
        CreatedBy        INT                 NOT NULL,
        CreatedAt         DATETIME2 (7)       CONSTRAINT DF_InvoiceGroup_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_InvoiceGroup PRIMARY KEY CLUSTERED (InvoiceGroupId ASC),
        CONSTRAINT FK_InvoiceGroup_Company FOREIGN KEY (ComId) REFERENCES dbo.Company (ComId),
        CONSTRAINT FK_InvoiceGroup_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'UQ_InvoiceGroup_Num' AND object_id = OBJECT_ID('dbo.InvoiceGroup')
)
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_InvoiceGroup_Num
        ON dbo.InvoiceGroup (InvoiceGroupNum ASC);
END
GO
