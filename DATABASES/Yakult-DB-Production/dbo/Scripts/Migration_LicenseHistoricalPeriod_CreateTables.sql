-- Previous coverage periods are historical snapshots, independent of active renewals.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID('dbo.LicenseHistoricalPeriod', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LicenseHistoricalPeriod (
        HistoricalPeriodId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_LicenseHistoricalPeriod PRIMARY KEY,
        AnchorSetId INT NOT NULL,
        StartDate DATE NOT NULL,
        EndDate DATE NOT NULL,
        ReferenceNumber NVARCHAR(100) NULL,
        Amount DECIMAL(18,2) NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedBy INT NOT NULL,
        CreatedAt DATETIME2(7) NOT NULL
            CONSTRAINT DF_LicenseHistoricalPeriod_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_LicenseHistoricalPeriod_Set FOREIGN KEY (AnchorSetId) REFERENCES dbo.[Set](SetId),
        CONSTRAINT FK_LicenseHistoricalPeriod_User FOREIGN KEY (CreatedBy) REFERENCES dbo.[User](UserId),
        CONSTRAINT CK_LicenseHistoricalPeriod_Dates CHECK (StartDate <= EndDate),
        CONSTRAINT CK_LicenseHistoricalPeriod_Amount CHECK (Amount IS NULL OR Amount >= 0)
    );
END;

IF OBJECT_ID('dbo.LicenseHistoricalPeriodItem', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.LicenseHistoricalPeriodItem (
        HistoricalPeriodItemId INT IDENTITY(1,1) NOT NULL
            CONSTRAINT PK_LicenseHistoricalPeriodItem PRIMARY KEY,
        HistoricalPeriodId INT NOT NULL,
        ItemId INT NULL,
        ItemName NVARCHAR(4000) NOT NULL,
        ItemCode NVARCHAR(800) NULL,
        Quantity DECIMAL(18,2) NOT NULL,
        DisplayOrder INT NOT NULL,
        CONSTRAINT FK_LicenseHistoricalPeriodItem_Period FOREIGN KEY (HistoricalPeriodId)
            REFERENCES dbo.LicenseHistoricalPeriod(HistoricalPeriodId),
        CONSTRAINT FK_LicenseHistoricalPeriodItem_Item FOREIGN KEY (ItemId) REFERENCES dbo.Item(ItemId),
        CONSTRAINT CK_LicenseHistoricalPeriodItem_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_LicenseHistoricalPeriodItem_Name CHECK (LEN(LTRIM(RTRIM(ItemName))) > 0)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.LicenseHistoricalPeriod') AND name = 'UQ_LicenseHistoricalPeriod_Set_Dates')
BEGIN
    CREATE UNIQUE INDEX UQ_LicenseHistoricalPeriod_Set_Dates
        ON dbo.LicenseHistoricalPeriod(AnchorSetId, StartDate, EndDate);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.LicenseHistoricalPeriodItem') AND name = 'IX_LicenseHistoricalPeriodItem_Period')
BEGIN
    CREATE INDEX IX_LicenseHistoricalPeriodItem_Period
        ON dbo.LicenseHistoricalPeriodItem(HistoricalPeriodId, DisplayOrder);
END;

COMMIT TRANSACTION;
