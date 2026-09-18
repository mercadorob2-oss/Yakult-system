IF NOT EXISTS (
    SELECT 1
    FROM sys.tables
    WHERE name = 'ReceiptSetDocumentImage'
)
BEGIN
    CREATE TABLE dbo.ReceiptSetDocumentImage (
        ImageId INT IDENTITY(1,1) NOT NULL,
        ReceiptSetId INT NOT NULL,
        DocType NVARCHAR(10) NOT NULL,
        ImagePath NVARCHAR(500) NULL,
        ImageBytes VARBINARY(MAX) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_ReceiptSetDocumentImage_SortOrder DEFAULT (0),
        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_ReceiptSetDocumentImage_CreatedAt DEFAULT (GETDATE()),
        CreatedBy INT NULL,

        CONSTRAINT PK_ReceiptSetDocumentImage PRIMARY KEY CLUSTERED (ImageId),
        CONSTRAINT FK_ReceiptSetDocumentImage_ReceiptSet
            FOREIGN KEY (ReceiptSetId)
            REFERENCES dbo.ReceiptSet (ReceiptSetId)
    );
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE name = 'IX_ReceiptSetDocumentImage_ReceiptSetId'
      AND object_id = OBJECT_ID('dbo.ReceiptSetDocumentImage')
)
BEGIN
    CREATE NONCLUSTERED INDEX IX_ReceiptSetDocumentImage_ReceiptSetId
        ON dbo.ReceiptSetDocumentImage (ReceiptSetId)
        INCLUDE (DocType, SortOrder);
END
GO
