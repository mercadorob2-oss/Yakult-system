-- =============================================================================
-- Migration: Seed additional 'Page' PermissionItem rows for the Inventory System,
--            Reports, Cartridge Management, and Call IT Monitoring portal navbars.
-- Purpose:   Extends Migration_PermissionItem_CreateTables.sql (which seeded only
--            ViewInvoicePage / ViewRenewalPage / ViewRenewalGroupPage) to cover
--            every remaining navbar entry in these four portals, so each can be
--            individually restricted per user via the Pages tab (see
--            Yakult.Inventory.App/Pages/Admin/Security/PageAccessPanel.cs, whose
--            PageCatalog list must stay in sync with the ItemKey values below).
--
--            Requester Portal (tab-based, no ShowXxxPage() pattern) and Borrow
--            Items (single-screen portal) are intentionally NOT covered here.
-- Run after: Migration_PermissionItem_CreateTables.sql
-- =============================================================================

DECLARE @Pages TABLE (ItemKey VARCHAR(100), DisplayName VARCHAR(150));

INSERT INTO @Pages (ItemKey, DisplayName) VALUES
    -- Inventory System — "View History"
    ('ViewItemMovementAuditPage',        'View Item Audit Trail'),
    ('ViewUpdatesPage',                  'View Updates'),
    ('ViewArchivePage',                  'View Archive'),
    -- Inventory System — "View Transactions"
    ('ViewWarrantyPage',                 'View Warranty'),
    ('ViewSetsPage',                     'View Sets'),
    ('SetDispatchNotificationPage',      'Send Notifications (Sets)'),
    ('UnfulfilledRequestsPage',          'Unfulfilled Requests'),
    ('PartiallyFulfilledRequestsPage',   'Partially Fulfilled Requests'),
    ('ViewRequestsPage',                 'View Requests'),
    -- Inventory System — "View Master Data"
    ('NonLicensedInvoicesPage',          'Non-Licensed Invoices'),
    ('InvoiceLicenseReviewPage',         'Invoice License Review'),
    ('ViewAssetPage',                    'View Assets'),
    ('ConsumableModelsPage',             'Consumable Models'),
    ('ViewFixedAssetsPage',              'View Fixed Assets'),
    ('ViewReceiptsPage',                 'View Receipts'),
    ('ViewVendorsPage',                  'View Vendors'),
    ('ViewDepartmentsPage',              'View Departments'),
    ('ViewBranchesPage',                 'View Branches'),
    ('ViewCompaniesPage',                'View Companies'),
    ('ViewEmployeesPage',                'View Employees'),
    ('ViewCategoriesPage',               'View Categories'),
    ('ViewRepairedItemsPage',            'View Repair Items'),
    ('ViewItemsPage',                    'View Items'),
    ('ViewInventoryPage',                'View Inventory'),
    -- Inventory System — "Add Master Data"
    ('AddSalesInvoiceSet',               'Add Sales Invoice Set'),
    ('AddRequestSet',                    'Add Request Set'),
    ('AddRequestPage',                   'Add Request'),
    ('AddItemPage',                      'Add Item'),
    ('AddEmployeePage',                  'Add Employee'),
    ('AddVendorDialog',                  'Add Vendor'),
    ('AddDepartmentDialog',              'Add Department'),
    ('AddBranchDialog',                  'Add Branch'),
    ('AddCompanyDialog',                 'Add Company'),
    -- Reports — remaining "Report Monitoring" entries
    ('ExportReportPage',                 'Export Report'),
    ('OutboundBatchesPage',              'Cartridge Disposed/Sold'),
    -- Cartridge Management — "Cartridge Master Data"
    ('CartridgeModelsPage',              'Cartridge Models'),
    ('ViewCartridgesPage',               'View Cartridges'),
    -- Cartridge Management — "Cartridge Fulfillment"
    ('FulfilledCartridgesPage',          'Fulfilled Cartridges'),
    ('PartiallyFulfilledCartridgePage',  'Partially Fulfilled Cartridges'),
    ('UnfulfilledCartridgeExchangesPage','Unfulfilled Cartridges'),
    ('CartridgeExchangePage',            'Cartridge Exchange'),
    -- Cartridge Management — "History & Monitoring"
    ('SoldCartridgesPage',               'Sold Cartridges'),
    ('DisposedCartridgesPage',           'Disposed Cartridges'),
    ('CartridgeTrackingPage',            'Cartridge Tracking'),
    ('ViewCartridgeSetsPage',            'View Cartridge Sets'),
    ('AuthorizationMonitorPage',         'Authorization Monitor'),
    -- Cartridge Management — "Batch Operations"
    ('VendorCartridgeRefillPage',        'Cartridge Refill Batch'),
    -- Cartridge Management — "Disposal Categories"
    ('DamagedEmptyCartridgesPage',       'Damaged Empty Cartridges'),
    ('NonRefillableCartridgesPage',      'Non-Refillable Cartridges'),
    -- Cartridge Management — standalone
    ('CartridgeFulfillmentNotificationPage', 'Send Notifications (Cartridges)'),
    -- Call IT Monitoring
    ('CallDashboardView',                'Dashboard'),
    ('TicketListView',                   'Ticket List'),
    ('IncomingTicketsView',              'Incoming Tickets'),
    ('DisplayModeView',                  'Display Mode'),
    ('EmailNotificationView',            'Email Notification'),
    ('CallReportsView',                  'Reports'),
    ('DiagnosticsView',                  'Diagnostics'),
    ('CallProfilesView',                 'Profiles');

INSERT INTO dbo.PermissionItem (PermissionType, ItemKey, DisplayName)
SELECT 'Page', p.ItemKey, p.DisplayName
FROM @Pages p
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PermissionItem pi
    WHERE pi.PermissionType = 'Page' AND pi.ItemKey = p.ItemKey
);
