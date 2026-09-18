-- =============================================================================
-- Migration: Create dbo.PageNavEntry and seed the Portal -> Menu -> Page catalog
-- Purpose:   Previously the mapping of each restrictable "Page" PermissionItem to
--            the Portal/Menu it lives under in the app's navbar was hardcoded in
--            C# (PageAccessPanel.PageCatalog), duplicating the SQL seed data. This
--            table makes it the single source of truth: the admin "Pages" tab now
--            reads Portal/Menu/DisplayName straight from here.
--
--            One PermissionItem (PermissionType='Page') can have MORE THAN ONE
--            PageNavEntry row when the same underlying page is reachable from more
--            than one portal/menu (e.g. View Invoices appears under both Reports
--            and Inventory System) — restricting it blocks it everywhere, but the
--            admin UI shows it in each place it's actually reachable from.
--
--            Adding a new restrictable page going forward is now a data-only
--            change: seed one dbo.PermissionItem row (if new) plus one or more
--            dbo.PageNavEntry rows here — no C# admin-UI change needed. The actual
--            enforcement guard (PermissionResolver.HasPageAccess("Key") call) still
--            has to be added in code at the page's entry point; that part can't be
--            data-driven since nothing scans the codebase for new pages.
-- Run after: Migration_PermissionItem_CreateTables.sql, Migration_PermissionItem_SeedNavPages.sql
-- =============================================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.objects
    WHERE object_id = OBJECT_ID('dbo.PageNavEntry') AND type = 'U'
)
BEGIN
    CREATE TABLE dbo.PageNavEntry (
        PageNavEntryId   INT IDENTITY (1, 1) NOT NULL,
        PermissionItemId INT NOT NULL,
        PortalKey        VARCHAR (50)  NOT NULL,   -- matches dbo.Portal.PortalKey
        MenuGroup        VARCHAR (100) NOT NULL,   -- the navbar section header, e.g. "Report Monitoring"
        DisplayName      VARCHAR (150) NOT NULL,   -- label shown at this nav entry point (may differ per portal)
        SortOrder        INT      NOT NULL DEFAULT (0),
        IsActive         BIT      NOT NULL DEFAULT (1),
        DateCreated      DATETIME NOT NULL DEFAULT (getdate()),

        CONSTRAINT PK_PageNavEntry PRIMARY KEY CLUSTERED (PageNavEntryId),
        CONSTRAINT FK_PageNavEntry_PermissionItem FOREIGN KEY (PermissionItemId) REFERENCES dbo.PermissionItem (PermissionItemId),
        CONSTRAINT UQ_PageNavEntry UNIQUE (PermissionItemId, PortalKey, MenuGroup)
    );
END

-- Seed: (ItemKey, PortalKey, MenuGroup, DisplayName, SortOrder)
-- ItemKey is resolved to PermissionItemId via join; rows are skipped if already present.

DECLARE @Nav TABLE (ItemKey VARCHAR(100), PortalKey VARCHAR(50), MenuGroup VARCHAR(100), DisplayName VARCHAR(150), SortOrder INT);

INSERT INTO @Nav (ItemKey, PortalKey, MenuGroup, DisplayName, SortOrder) VALUES
    -- Reports — "Report Monitoring"
    ('ViewInvoicePage',      'Reports', 'Report Monitoring', 'View Invoices',             1),
    ('ViewRenewalPage',      'Reports', 'Report Monitoring', 'View Renewals',             2),
    ('ViewRenewalGroupPage', 'Reports', 'Report Monitoring', 'View Renewals (Grouped)',   3),
    ('ViewSetsPage',         'Reports', 'Report Monitoring', 'View Sets',                 4),
    ('ExportReportPage',     'Reports', 'Report Monitoring', 'Export',                    5),
    ('OutboundBatchesPage',  'Reports', 'Report Monitoring', 'Cartridge Disposed/Sold',   6),

    -- Inventory System — "View History"
    ('ViewItemMovementAuditPage', 'InventorySystem', 'View History', 'View Item Audit Trail', 1),
    ('ViewUpdatesPage',           'InventorySystem', 'View History', 'View Updates',           2),
    ('ViewArchivePage',           'InventorySystem', 'View History', 'View Archive',           3),

    -- Inventory System — "View Transactions"
    ('ViewWarrantyPage',              'InventorySystem', 'View Transactions', 'View Warranty',                1),
    ('ViewRenewalGroupPage',          'InventorySystem', 'View Transactions', 'View Renewals (Grouped)',       2),
    ('ViewRenewalPage',               'InventorySystem', 'View Transactions', 'View Renewals',                3),
    ('ViewInvoicePage',               'InventorySystem', 'View Transactions', 'View Invoices',                4),
    ('ViewSetsPage',                  'InventorySystem', 'View Transactions', 'View Sets',                    5),
    ('SetDispatchNotificationPage',   'InventorySystem', 'View Transactions', 'Send Notifications (Sets)',    6),
    ('UnfulfilledRequestsPage',       'InventorySystem', 'View Transactions', 'Unfulfilled Requests',         7),
    ('PartiallyFulfilledRequestsPage','InventorySystem', 'View Transactions', 'Partially Fulfilled Requests', 8),
    ('ViewRequestsPage',              'InventorySystem', 'View Transactions', 'View Requests',                9),

    -- Inventory System — "View Master Data"
    ('NonLicensedInvoicesPage',  'InventorySystem', 'View Master Data', 'Non-Licensed Invoices',  1),
    ('InvoiceLicenseReviewPage', 'InventorySystem', 'View Master Data', 'Invoice License Review', 2),
    ('ViewAssetPage',            'InventorySystem', 'View Master Data', 'View Assets',            3),
    ('ConsumableModelsPage',     'InventorySystem', 'View Master Data', 'Consumable Models',      4),
    ('ViewFixedAssetsPage',      'InventorySystem', 'View Master Data', 'View Fixed Assets',      5),
    ('ViewReceiptsPage',         'InventorySystem', 'View Master Data', 'View Receipts',          6),
    ('ViewVendorsPage',          'InventorySystem', 'View Master Data', 'View Vendors',           7),
    ('ViewDepartmentsPage',      'InventorySystem', 'View Master Data', 'View Departments',       8),
    ('ViewBranchesPage',         'InventorySystem', 'View Master Data', 'View Branches',          9),
    ('ViewCompaniesPage',        'InventorySystem', 'View Master Data', 'View Companies',         10),
    ('ViewEmployeesPage',        'InventorySystem', 'View Master Data', 'View Employees',         11),
    ('ViewCategoriesPage',       'InventorySystem', 'View Master Data', 'View Categories',        12),
    ('ViewRepairedItemsPage',    'InventorySystem', 'View Master Data', 'View Repair Items',      13),
    ('ViewItemsPage',            'InventorySystem', 'View Master Data', 'View Items',             14),
    ('ViewInventoryPage',        'InventorySystem', 'View Master Data', 'View Inventory',         15),

    -- Inventory System — "Add Master Data"
    ('AddSalesInvoiceSet',  'InventorySystem', 'Add Master Data', 'Sales Invoice Set', 1),
    ('AddRequestSet',       'InventorySystem', 'Add Master Data', 'Request Set',        2),
    ('AddRequestPage',      'InventorySystem', 'Add Master Data', 'Request',            3),
    ('AddItemPage',         'InventorySystem', 'Add Master Data', 'Item',               4),
    ('AddEmployeePage',     'InventorySystem', 'Add Master Data', 'Employee',           5),
    ('AddVendorDialog',     'InventorySystem', 'Add Master Data', 'Vendor',             6),
    ('AddDepartmentDialog', 'InventorySystem', 'Add Master Data', 'Department',         7),
    ('AddBranchDialog',     'InventorySystem', 'Add Master Data', 'Branch',             8),
    ('AddCompanyDialog',    'InventorySystem', 'Add Master Data', 'Company',            9),

    -- Cartridge Management — "Cartridge Master Data"
    ('CartridgeModelsPage', 'CartridgeManagement', 'Cartridge Master Data', 'Cartridge Models', 1),
    ('ViewCartridgesPage',  'CartridgeManagement', 'Cartridge Master Data', 'View Cartridges',  2),

    -- Cartridge Management — "Cartridge Fulfillment"
    ('FulfilledCartridgesPage',           'CartridgeManagement', 'Cartridge Fulfillment', 'Fulfilled Cartridges',        1),
    ('PartiallyFulfilledCartridgePage',   'CartridgeManagement', 'Cartridge Fulfillment', 'Partially Fulfilled',         2),
    ('UnfulfilledCartridgeExchangesPage', 'CartridgeManagement', 'Cartridge Fulfillment', 'Unfulfilled Cartridges',      3),
    ('CartridgeExchangePage',             'CartridgeManagement', 'Cartridge Fulfillment', 'Cartridge Exchange',          4),

    -- Cartridge Management — "History & Monitoring"
    ('SoldCartridgesPage',        'CartridgeManagement', 'History & Monitoring', 'Sold Cartridges',       1),
    ('DisposedCartridgesPage',    'CartridgeManagement', 'History & Monitoring', 'Disposed Cartridges',   2),
    ('CartridgeTrackingPage',     'CartridgeManagement', 'History & Monitoring', 'Cartridge Tracking',    3),
    ('ViewCartridgeSetsPage',     'CartridgeManagement', 'History & Monitoring', 'View Cartridge Sets',   4),
    ('AuthorizationMonitorPage',  'CartridgeManagement', 'History & Monitoring', 'Authorization Monitor', 5),

    -- Cartridge Management — "Batch Operations"
    ('OutboundBatchesPage',       'CartridgeManagement', 'Batch Operations', 'Dispose / Sell Cartridges', 1),
    ('VendorCartridgeRefillPage', 'CartridgeManagement', 'Batch Operations', 'Cartridge Refill Batch',    2),

    -- Cartridge Management — "Disposal Categories"
    ('DamagedEmptyCartridgesPage',  'CartridgeManagement', 'Disposal Categories', 'Damaged Empty Cartridges',  1),
    ('NonRefillableCartridgesPage', 'CartridgeManagement', 'Disposal Categories', 'Non-Refillable Cartridges', 2),

    -- Cartridge Management — "General" (standalone navbar button)
    ('CartridgeFulfillmentNotificationPage', 'CartridgeManagement', 'General', 'Send Notifications', 1),

    -- Call IT Monitoring — "Call Monitoring" (flat nav bar)
    ('CallDashboardView',      'CallITMonitoring', 'Call Monitoring', 'Dashboard',           1),
    ('TicketListView',        'CallITMonitoring', 'Call Monitoring', 'Ticket List',          2),
    ('IncomingTicketsView',   'CallITMonitoring', 'Call Monitoring', 'Incoming Tickets',     3),
    ('DisplayModeView',       'CallITMonitoring', 'Call Monitoring', 'Display Mode',         4),
    ('EmailNotificationView', 'CallITMonitoring', 'Call Monitoring', 'Email Notification',   5),
    ('CallReportsView',       'CallITMonitoring', 'Call Monitoring', 'Reports',              6),
    ('DiagnosticsView',       'CallITMonitoring', 'Call Monitoring', 'Diagnostics',          7),
    ('CallProfilesView',      'CallITMonitoring', 'Call Monitoring', 'Profiles',             8);

INSERT INTO dbo.PageNavEntry (PermissionItemId, PortalKey, MenuGroup, DisplayName, SortOrder)
SELECT pi.PermissionItemId, n.PortalKey, n.MenuGroup, n.DisplayName, n.SortOrder
FROM @Nav n
INNER JOIN dbo.PermissionItem pi ON pi.PermissionType = 'Page' AND pi.ItemKey = n.ItemKey
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PageNavEntry existing
    WHERE existing.PermissionItemId = pi.PermissionItemId
      AND existing.PortalKey = n.PortalKey
      AND existing.MenuGroup = n.MenuGroup
);
