-- Sub-Type Groups: a user bundles several dbo.Item rows into a named group (Contract/
-- Subscription/License/Services) directly from the Items Page — BEFORE any invoice
-- exists — via one popup asking for a Reference Code (Contract Code/Subscription ID/
-- License ID/Service ID) and a Begin/End Date that override the member items' own
-- dates. dbo.InvoicePreparation is the group header (one row per group); dbo.Item stays
-- the only master item table, referenced only via InvoicePreparationItem.ItemId.
--
-- The Invoice Preparation page lists these groups. Selecting several and clicking
-- "Create an Invoice From Them" hands their combined items to SoftwareServiceSetDialog,
-- which builds the real invoice (dbo.[Set] with IsInvoice=1 + dbo.SetItem).
--
-- This script is idempotent and also cleans up artifacts from earlier abandoned designs
-- for this same feature (a per-item SubType/ReferenceCode/date-range attribute model,
-- and before that, repurposing dbo.Contract/Subscription/License/ServiceDetail as
-- standalone group headers with dbo.ContractItem/SubscriptionItem/LicenseItem/
-- ServiceDetailItem link tables) — safe to run regardless of which of those, if any,
-- were ever applied to this database.
--
-- GO separators are required between each CREATE TABLE/constraint and any statement
-- that references it in the same script — SQL Server resolves column/object references
-- against the schema as of the start of the batch.

-- ── 1. Clean up the abandoned "standalone detail table" design, if present ─────────
IF OBJECT_ID('dbo.ContractItem', 'U') IS NOT NULL DROP TABLE dbo.ContractItem;
GO
IF OBJECT_ID('dbo.SubscriptionItem', 'U') IS NOT NULL DROP TABLE dbo.SubscriptionItem;
GO
IF OBJECT_ID('dbo.LicenseItem', 'U') IS NOT NULL DROP TABLE dbo.LicenseItem;
GO
IF OBJECT_ID('dbo.ServiceDetailItem', 'U') IS NOT NULL DROP TABLE dbo.ServiceDetailItem;
GO

DELETE FROM dbo.Contract WHERE SetId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contract') AND name = 'SetId' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.Contract ALTER COLUMN SetId INT NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Contract_SetId')
BEGIN
    ALTER TABLE dbo.Contract ADD CONSTRAINT UQ_Contract_SetId UNIQUE (SetId);
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contract') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.Contract DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Contract') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.Contract DROP COLUMN EndDate;
END
GO

DELETE FROM dbo.Subscription WHERE SetId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'SetId' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.Subscription ALTER COLUMN SetId INT NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_Subscription_SetId')
BEGIN
    ALTER TABLE dbo.Subscription ADD CONSTRAINT UQ_Subscription_SetId UNIQUE (SetId);
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.Subscription') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.Subscription DROP COLUMN EndDate;
END
GO

DELETE FROM dbo.License WHERE SetId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.License') AND name = 'SetId' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.License ALTER COLUMN SetId INT NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_License_SetId')
BEGIN
    ALTER TABLE dbo.License ADD CONSTRAINT UQ_License_SetId UNIQUE (SetId);
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.License') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.License DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.License') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.License DROP COLUMN EndDate;
END
GO

DELETE FROM dbo.ServiceDetail WHERE SetId IS NULL;
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceDetail') AND name = 'SetId' AND is_nullable = 1)
BEGIN
    ALTER TABLE dbo.ServiceDetail ALTER COLUMN SetId INT NOT NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'UQ_ServiceDetail_SetId')
BEGIN
    ALTER TABLE dbo.ServiceDetail ADD CONSTRAINT UQ_ServiceDetail_SetId UNIQUE (SetId);
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceDetail') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.ServiceDetail DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.ServiceDetail') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.ServiceDetail DROP COLUMN EndDate;
END
GO

-- ── 2. Retire the per-item-attribute aggregation view, if present ──────────────────
IF OBJECT_ID('dbo.vw_InvoicePreparationSubTypeGroups', 'V') IS NOT NULL
    DROP VIEW dbo.vw_InvoicePreparationSubTypeGroups;
GO

-- ── 3. dbo.InvoicePreparation — the group header ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.InvoicePreparation') AND type = 'U')
BEGIN
    CREATE TABLE dbo.InvoicePreparation (
        PreparationId  INT IDENTITY(1,1) NOT NULL,
        SubType        NVARCHAR(20)    NOT NULL,
        ReferenceCode  NVARCHAR(100)   NULL,
        SupplierId     INT             NULL,
        BeginDate      DATE            NULL,
        EndDate        DATE            NULL,
        Status         NVARCHAR(20)    NOT NULL CONSTRAINT DF_InvoicePreparation_Status DEFAULT ('Draft'),
        GeneratedSetId INT             NULL,
        CreatedBy      INT             NOT NULL,
        CreatedAt      DATETIME2(7)    NOT NULL CONSTRAINT DF_InvoicePreparation_CreatedAt DEFAULT (sysutcdatetime()),
        ModifiedBy     INT             NULL,
        ModifiedAt     DATETIME2(7)    NULL,
        CONSTRAINT PK_InvoicePreparation PRIMARY KEY CLUSTERED (PreparationId ASC)
    );
END
GO

-- Drop columns/index left over from the original "one preparation per supplier
-- document" shape, if this table was created by an earlier version of this script.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_InvoicePreparation_DocumentNumber' AND object_id = OBJECT_ID('dbo.InvoicePreparation'))
BEGIN
    DROP INDEX UQ_InvoicePreparation_DocumentNumber ON dbo.InvoicePreparation;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'DocumentNumber')
BEGIN
    ALTER TABLE dbo.InvoicePreparation DROP COLUMN DocumentNumber;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'InvoiceDate')
BEGIN
    ALTER TABLE dbo.InvoicePreparation DROP COLUMN InvoiceDate;
END
GO

-- Add the group-header columns if this table pre-dates them.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'SubType')
BEGIN
    ALTER TABLE dbo.InvoicePreparation ADD SubType NVARCHAR(20) NOT NULL CONSTRAINT DF_InvoicePreparation_SubType DEFAULT ('Contract');
    ALTER TABLE dbo.InvoicePreparation DROP CONSTRAINT DF_InvoicePreparation_SubType;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'ReferenceCode')
BEGIN
    ALTER TABLE dbo.InvoicePreparation ADD ReferenceCode NVARCHAR(100) NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.InvoicePreparation ADD BeginDate DATE NULL;
END
GO
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparation') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.InvoicePreparation ADD EndDate DATE NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_InvoicePreparation_Status')
BEGIN
    ALTER TABLE dbo.InvoicePreparation
        ADD CONSTRAINT CK_InvoicePreparation_Status CHECK (Status IN ('Draft', 'Completed', 'Cancelled'));
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_InvoicePreparation_SubType')
BEGIN
    ALTER TABLE dbo.InvoicePreparation
        ADD CONSTRAINT CK_InvoicePreparation_SubType CHECK (
            SubType IN ('Contract', 'Subscription', 'License', 'Services')
        );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoicePreparation_Supplier')
BEGIN
    ALTER TABLE dbo.InvoicePreparation
        ADD CONSTRAINT FK_InvoicePreparation_Supplier FOREIGN KEY (SupplierId) REFERENCES dbo.Vendor (VendorID);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoicePreparation_GeneratedSet')
BEGIN
    ALTER TABLE dbo.InvoicePreparation
        ADD CONSTRAINT FK_InvoicePreparation_GeneratedSet FOREIGN KEY (GeneratedSetId) REFERENCES dbo.[Set] (SetId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoicePreparation_CreatedBy')
BEGIN
    ALTER TABLE dbo.InvoicePreparation
        ADD CONSTRAINT FK_InvoicePreparation_CreatedBy FOREIGN KEY (CreatedBy) REFERENCES dbo.[User] (UserId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoicePreparation_SubType' AND object_id = OBJECT_ID('dbo.InvoicePreparation'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_InvoicePreparation_SubType
        ON dbo.InvoicePreparation (SubType ASC);
END
GO

-- ── 4. dbo.InvoicePreparationItem — group membership (no per-item SubType/dates; the
--        group header above is authoritative) ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.objects WHERE object_id = OBJECT_ID(N'dbo.InvoicePreparationItem') AND type = 'U')
BEGIN
    CREATE TABLE dbo.InvoicePreparationItem (
        PreparationItemId INT             IDENTITY(1,1) NOT NULL,
        PreparationId     INT             NOT NULL,
        ItemId            INT             NOT NULL,
        Quantity          DECIMAL(18,2)   NOT NULL CONSTRAINT DF_InvoicePreparationItem_Quantity DEFAULT (0),
        UnitPrice         DECIMAL(18,2)   NOT NULL CONSTRAINT DF_InvoicePreparationItem_UnitPrice DEFAULT (0),
        Remarks           NVARCHAR(400)   NULL,
        CreatedBy         INT             NOT NULL,
        CreatedAt         DATETIME2(7)    NOT NULL CONSTRAINT DF_InvoicePreparationItem_CreatedAt DEFAULT (sysutcdatetime()),
        CONSTRAINT PK_InvoicePreparationItem PRIMARY KEY CLUSTERED (PreparationItemId ASC)
    );
END
GO

-- Drop the per-item attribute columns from the abandoned design, if present — the
-- group header (dbo.InvoicePreparation) now carries SubType/ReferenceCode/BeginDate/EndDate.
IF EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = 'CK_InvoicePreparationItem_SubType')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem DROP CONSTRAINT CK_InvoicePreparationItem_SubType;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparationItem') AND name = 'SubType')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem DROP COLUMN SubType;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparationItem') AND name = 'ReferenceCode')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem DROP COLUMN ReferenceCode;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparationItem') AND name = 'BeginDate')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem DROP COLUMN BeginDate;
END
GO
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.InvoicePreparationItem') AND name = 'EndDate')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem DROP COLUMN EndDate;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoicePreparationItem_Preparation')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem
        ADD CONSTRAINT FK_InvoicePreparationItem_Preparation FOREIGN KEY (PreparationId)
            REFERENCES dbo.InvoicePreparation (PreparationId) ON DELETE CASCADE;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_InvoicePreparationItem_Item')
BEGIN
    ALTER TABLE dbo.InvoicePreparationItem
        ADD CONSTRAINT FK_InvoicePreparationItem_Item FOREIGN KEY (ItemId) REFERENCES dbo.Item (ItemId);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_InvoicePreparationItem_PreparationId' AND object_id = OBJECT_ID('dbo.InvoicePreparationItem'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_InvoicePreparationItem_PreparationId
        ON dbo.InvoicePreparationItem (PreparationId ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UQ_InvoicePreparationItem_PreparationId_ItemId' AND object_id = OBJECT_ID('dbo.InvoicePreparationItem'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UQ_InvoicePreparationItem_PreparationId_ItemId
        ON dbo.InvoicePreparationItem (PreparationId ASC, ItemId ASC);
END
GO
