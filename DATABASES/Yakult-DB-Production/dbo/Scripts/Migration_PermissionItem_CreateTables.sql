-- =============================================================================
-- Migration: Create dbo.PermissionItem / dbo.UserPermissionItem
-- Purpose:   Generic, self-extending fine-grained permission model. Each row in
--            PermissionItem is one restrictable "thing" (a page, an item
--            category, a CRUD action, or any future PermissionType) identified
--            by (PermissionType, ItemKey). UserPermissionItem stores per-user
--            overrides against the default-allow behavior:
--              no row            -> allowed (default)
--              IsGranted = 0     -> explicitly denied for that user
--              IsGranted = 1     -> explicitly (re-)granted for that user
--            Adding a new restrictable Page/Action later only needs a new
--            PermissionItem row (via this script or an admin UI) -- no new
--            table. ItemCategory-type rows are not seeded here; they are
--            upserted on demand from dbo.ItemCategory.Name when an admin
--            restricts a category for a user.
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.PermissionItem') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.PermissionItem (
        PermissionItemId INT IDENTITY (1, 1) NOT NULL,
        PermissionType   VARCHAR (30)  NOT NULL,   -- 'Page' | 'ItemCategory' | 'Action' | future types
        ItemKey          VARCHAR (100) NOT NULL,   -- e.g. 'ViewInvoicePage', 'Ink', 'Item.Add'
        DisplayName      VARCHAR (150) NOT NULL,
        IsActive         BIT      NOT NULL DEFAULT (1),
        DateCreated      DATETIME NOT NULL DEFAULT (getdate()),

        CONSTRAINT PK_PermissionItem PRIMARY KEY CLUSTERED (PermissionItemId),
        CONSTRAINT UQ_PermissionItem_Type_Key UNIQUE (PermissionType, ItemKey)
    );
END

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.UserPermissionItem') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.UserPermissionItem (
        UserId           INT NOT NULL,
        PermissionItemId INT NOT NULL,
        IsGranted        BIT      NOT NULL DEFAULT (0),
        DateModified     DATETIME NOT NULL DEFAULT (getdate()),
        ModifiedByUserId INT NULL,

        CONSTRAINT PK_UserPermissionItem PRIMARY KEY CLUSTERED (UserId, PermissionItemId),
        CONSTRAINT FK_UserPermissionItem_User FOREIGN KEY (UserId) REFERENCES dbo.[User] (UserId),
        CONSTRAINT FK_UserPermissionItem_Item FOREIGN KEY (PermissionItemId) REFERENCES dbo.PermissionItem (PermissionItemId)
    );
END

-- Seed the pre-known restrictable Pages and Actions. ItemCategory entries are
-- intentionally not seeded here (see header comment).

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Page' AND ItemKey = 'ViewInvoicePage')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Page', 'ViewInvoicePage', 'View Invoices');

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Page' AND ItemKey = 'ViewRenewalPage')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Page', 'ViewRenewalPage', 'View Renewals');

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Page' AND ItemKey = 'ViewRenewalGroupPage')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Page', 'ViewRenewalGroupPage', 'View Renewals (Grouped)');

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Action' AND ItemKey = 'Item.Add')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Action', 'Item.Add', 'Add Item');

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Action' AND ItemKey = 'Item.Edit')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Action', 'Item.Edit', 'Edit Item');

IF NOT EXISTS (SELECT 1 FROM dbo.PermissionItem WHERE PermissionType = 'Action' AND ItemKey = 'Item.Delete')
    INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName) VALUES ('Action', 'Item.Delete', 'Delete Item');
