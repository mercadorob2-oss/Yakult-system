-- Creates the four Invoice Sub-Type detail tables: Contract, Subscription, License,
-- ServiceDetail. Each is linked 1:1 to the dbo.[Set] row (the invoice) that carries the
-- matching InvoiceSubType, and carries one user-typed reference code column distinct
-- from its own identity PK (e.g. Subscription.SubscriptionCode vs SubscriptionId).
--
-- Named ServiceDetail (not "Service") to avoid colliding with the existing
-- 'Service'/'Services' SetType string values already used elsewhere on dbo.[Set].

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Contract' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Contract (
        ContractId   INT IDENTITY (1, 1) NOT NULL,
        SetId        INT                 NOT NULL,
        ContractCode NVARCHAR (100)      NULL,
        CreatedBy    INT                 NOT NULL,
        CreatedAt    DATETIME2 (7)       CONSTRAINT DF_Contract_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_Contract PRIMARY KEY CLUSTERED (ContractId ASC),
        CONSTRAINT FK_Contract_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT FK_Contract_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId),
        CONSTRAINT UQ_Contract_SetId UNIQUE (SetId)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'Subscription' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.Subscription (
        SubscriptionId   INT IDENTITY (1, 1) NOT NULL,
        SetId            INT                 NOT NULL,
        SubscriptionCode NVARCHAR (100)      NULL,
        CreatedBy        INT                 NOT NULL,
        CreatedAt        DATETIME2 (7)       CONSTRAINT DF_Subscription_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_Subscription PRIMARY KEY CLUSTERED (SubscriptionId ASC),
        CONSTRAINT FK_Subscription_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT FK_Subscription_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId),
        CONSTRAINT UQ_Subscription_SetId UNIQUE (SetId)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'License' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.License (
        LicenseId   INT IDENTITY (1, 1) NOT NULL,
        SetId       INT                 NOT NULL,
        LicenseCode NVARCHAR (100)      NULL,
        CreatedBy   INT                 NOT NULL,
        CreatedAt   DATETIME2 (7)       CONSTRAINT DF_License_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_License PRIMARY KEY CLUSTERED (LicenseId ASC),
        CONSTRAINT FK_License_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT FK_License_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId),
        CONSTRAINT UQ_License_SetId UNIQUE (SetId)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.tables WHERE name = 'ServiceDetail' AND schema_id = SCHEMA_ID('dbo')
)
BEGIN
    CREATE TABLE dbo.ServiceDetail (
        ServiceDetailId INT IDENTITY (1, 1) NOT NULL,
        SetId           INT                 NOT NULL,
        ServiceCode     NVARCHAR (100)      NULL,
        CreatedBy       INT                 NOT NULL,
        CreatedAt       DATETIME2 (7)       CONSTRAINT DF_ServiceDetail_CreatedAt DEFAULT (sysutcdatetime()) NOT NULL,
        CONSTRAINT PK_ServiceDetail PRIMARY KEY CLUSTERED (ServiceDetailId ASC),
        CONSTRAINT FK_ServiceDetail_Set FOREIGN KEY (SetId) REFERENCES dbo.[Set] (SetId),
        CONSTRAINT FK_ServiceDetail_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId),
        CONSTRAINT UQ_ServiceDetail_SetId UNIQUE (SetId)
    );
END
