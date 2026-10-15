# Yakumi read-only coverage

Generated from the desktop capability, screen and guide registries. This describes implemented scope, not live database or model certification. See Desktop-AI-Review.md for validation and limits.

53 data capabilities. Lists show eight rows per page; counts and aggregate sums cover every matching accessible record. Details require a numeric saved ID. Summaries use only the groupings below. History is represented by its own source. Every factual response labels its source, filters, total and retrieval time.

## Data sources and permissions

Portal alternatives are joined with OR; listed page restrictions apply to the normal route. Explicit Admin, Developer and Super Admin guards apply in addition. Requester access is restricted to CreatedBy=current user unless Inventory/View Requests or Admin/Department History grants broader access. Category restrictions apply before aggregation wherever Category scope is listed. Credentials, signatures, QR tokens and attachment bytes are excluded.

| Module / capability | Data source | Filters | Groupings | SQL sums | Date field | Access / scope |
| --- | --- | --- | --- | --- | --- | --- |
| Inventory / items | dbo.ArchiveStatus, dbo.Item, dbo.Condition | Category, Condition, Status | Category, Condition, Status | Units | CreatedAt | InventorySystem, pages: ViewItemsPage, CategoryScope, archive excluded by default, default status: Active |
| Inventory / inventory | dbo.ArchiveStatus, dbo.Inventory, dbo.Item, dbo.[User] | Category, Status | Status, Category | Units | CreatedAt | InventorySystem, Reports, pages: ViewInventoryPage, CategoryScope, archive excluded by default |
| Inventory / assets | dbo.Asset, dbo.Item, dbo.Vendor | Category, Status | Category, Status | - | CreatedAt | InventorySystem, Reports, pages: ViewAssetPage, CategoryScope, default status: Active |
| Inventory / warranties | dbo.Item, dbo.ArchiveStatus | Category, Status | Category, Status | - | EndDate | InventorySystem, Reports, pages: ViewWarrantyPage, CategoryScope |
| Inventory / employees | dbo.Employee, dbo.Title, dbo.Department, dbo.Branch, dbo.Company | Department, Branch, Company, Status | Department, Branch, Company, Position, Status | - | CreatedAt | InventorySystem, pages: ViewEmployeesPage, or AdminPortal/EmployeeManagementPage, default status: Active |
| Inventory / companies | dbo.Company | Status | Status | - | CreatedAt | InventorySystem, pages: ViewCompaniesPage, default status: Active |
| Inventory / branches | dbo.Branch | Status | Status, BranchType | - | CreatedAt | InventorySystem, pages: ViewBranchesPage, default status: Active |
| Inventory / departments | dbo.Department | Status | Status, Section | - | CreatedAt | InventorySystem, pages: ViewDepartmentsPage, default status: Active |
| Inventory / vendors | dbo.Vendor | Status | Status | - | CreatedAt | InventorySystem, pages: ViewVendorsPage, default status: Active |
| Inventory / sets | dbo.ArchiveStatus, dbo.[Set], dbo.Request, dbo.Employee, dbo.Department, dbo.Branch, dbo.Company | Status, Employee, Department, Branch, Company | Status, SetType, Employee, Department, Branch, Company | - | CreatedAt | InventorySystem, Reports, pages: ViewSetsPage, archive excluded by default |
| Inventory / set-items | dbo.SetItem, dbo.[Set], dbo.Request, dbo.Item, dbo.ArchiveStatus | Category, Status | Category, Status | Units | CreatedAt | InventorySystem, Reports, pages: ViewSetsPage, CategoryScope |
| Requests / requests | dbo.Request, dbo.Item, dbo.[Set], dbo.Employee, dbo.Department, dbo.Branch | Category, Status, Employee, Department, Branch | Status, Category, Employee, Department, Branch | RequestedUnits, IssuedUnits, OutstandingUnits | CreatedAt | InventorySystem, RequesterPortal, pages: ViewRequestsPage, CategoryScope, requester own scope, or AdminPortal/DepartmentRequestHistoryPage |
| Consumables / consumables | dbo.ConsumableModel, dbo.Item, dbo.ArchiveStatus | Category, Status | Category, Status | Units | CreatedAt | InventorySystem, CartridgeManagement, pages: ConsumableModelsPage, CategoryScope, or AdminPortal/ConsumableStockMonitorPage |
| Consumables / cartridges | dbo.CartridgeModel, dbo.Item, dbo.ArchiveStatus | Category, Status | Brand, Status | Units, BrandNewUnits, RefilledUnits | CreatedAt | CartridgeManagement, pages: -, CategoryScope |
| Consumables / refills | dbo.VendorCartridgeBatch, dbo.Vendor, dbo.CartridgeModel | Status, Category | Status, Vendor, Purpose | SentUnits, ReturnedUnits | CreatedAt | CartridgeManagement, pages: -, CategoryScope |
| Consumables / exchanges | dbo.UnfulfilledCartridgeExchange, dbo.Employee, dbo.Branch, dbo.Department | Status, Employee, Branch, Department, Category | Status, Employee, Branch, Department | RequestedUnits, IssuedUnits, OutstandingUnits | CreatedAt | CartridgeManagement, pages: -, CategoryScope |
| Requests / approvals | dbo.CartridgeAuthorization, dbo.Employee, dbo.Department | Status, Employee, Department | Status, Employee, Department | - | CreatedAt | RequesterPortal, CartridgeManagement, pages: - |
| Tickets / tickets | dbo.CallTicket, dbo.Employee, dbo.Department, dbo.Branch, dbo.Company | Status, Priority, Employee, Department, Branch, Company | Status, Priority, Employee, Department, Branch, Company | - | CreatedAt | CallITMonitoring, pages: - |
| Tickets / ticket-history | dbo.CallTicketHistory, dbo.CallTicket, dbo.[User] | Status | Field, NewValue | - | CreatedAt | CallITMonitoring, pages: - |
| Borrowing / borrows | dbo.BorrowLog | Employee, Department, Status | Status, Employee, Department | - | CreatedAt | BorrowItems, pages: - |
| Repairs / repairs | dbo.RepairTicket, dbo.Employee, dbo.Department, dbo.Branch, dbo.Company | Status, Priority, Employee, Department, Branch, Company | Status, Priority, Employee, Department, Branch, Company | - | CreatedAt | RepairTechnicianPortal, pages: - |
| Repairs / repair-history | dbo.RepairTicketHistory, dbo.RepairTicket, dbo.[User] | Status | Field, NewValue | - | CreatedAt | RepairTechnicianPortal, pages: - |
| Repairs / repair-parts | dbo.RepairPart, dbo.RepairTicket | Status, Priority | Status, Priority | - | CreatedAt | RepairTechnicianPortal, pages: - |
| Reports / invoices | dbo.vw_Invoices, dbo.[Set], dbo.ArchiveStatus | Status, Company | Status, Company, Site | Subtotal, Vat, Withholding, Discount, AmountDue | CreatedAt | InventorySystem, Reports, pages: ViewInvoicePage |
| Reports / invoice-items | dbo.SetItem, dbo.[Set], dbo.Item, dbo.SetItemParentTagGroup, dbo.ArchiveStatus | Category, Status | SubType, ParentTag, Category, Status | Units, Amount | CreatedAt | InventorySystem, Reports, pages: ViewInvoicePage, CategoryScope |
| Reports / renewals | dbo.Renewals, dbo.Item | Category, Status | Category, Status | Amount | EndDate | InventorySystem, Reports, pages: ViewRenewalPage, CategoryScope, archive excluded by default |
| Reports / receipts | dbo.ReceiptSet | - | Vendor | - | CreatedAt | InventorySystem, Reports, pages: ViewReceiptsPage |
| Inventory / archive | dbo.ArchiveStatus | Status | EntityType | - | CreatedAt | InventorySystem, pages: ViewArchivePage |
| Admin / accounts | dbo.[User], dbo.Employee | Employee, Status | Status, AccessLevel | - | CreatedAt | AdminPortal, pages: UserAccountManagementPage, AdminOnly, SuperAdminOnly |
| Admin / roles | dbo.UserRole, dbo.[User], dbo.Role | Status | Role, Status | - | CreatedAt | AdminPortal, InventorySystem, pages: -, AdminOnly |
| Admin / audit | dbo.UserActivityLog, dbo.[User] | - | Action, EntityType | - | CreatedAt | AdminPortal, InventorySystem, pages: -, AdminOnly |
| Workspace / policies | dbo.CompanyPolicy | Category | Category | - | CreatedAt | InventorySystem, RequesterPortal, CallITMonitoring, BorrowItems, CartridgeManagement, RepairTechnicianPortal, Reports, AdminPortal, pages: - |
| Inventory / item-history | dbo.ItemAuditTrail, dbo.Item | Category, Employee, Department, Branch, Status | Action, Status, Employee, Department, Branch, Category | - | CreatedAt | InventorySystem, pages: ViewItemMovementAuditPage, CategoryScope |
| Inventory / ownership-history | dbo.AuditTrail | Employee, Department, Branch, Status | Employee, Department, Branch | - | CreatedAt | InventorySystem, pages: ViewOwnershipHistoryPage |
| Inventory / set-updates | dbo.SetItemUpdate | Status | Status, Processing | - | CreatedAt | InventorySystem, pages: ViewUpdatesPage |
| Inventory / item-repairs | dbo.ItemRepairHistory, dbo.Item | Category, Status, Condition | Status, Condition, Category | - | CreatedAt | InventorySystem, pages: ViewRepairedItemsPage, CategoryScope |
| Inventory / categories | dbo.ItemCategory | Status | Status | - | CreatedAt | InventorySystem, pages: ViewCategoriesPage, default status: Active |
| Inventory / conditions | dbo.Condition | - | - | - | - | InventorySystem, pages: ViewItemsPage |
| Reports / invoice-preparation | dbo.InvoicePreparation | Status | SubType, Status | - | CreatedAt | InventorySystem, Reports, pages: ViewInvoicePreparationPage |
| Reports / license-review | dbo.vw_InvoiceNonLicensedItems | Category, Company, Department, Employee, Status | Company, Department, Category, Status | Units, Amount | CreatedAt | InventorySystem, Reports, pages: InvoiceLicenseReviewPage, SuperAdminOnly, CategoryScope |
| Reports / nonlicensed | dbo.vw_ConfirmedNonLicensedInvoiceItems | Category, Company, Department, Employee, Status | Company, Department, Category, Status | Units, Amount | CreatedAt | InventorySystem, Reports, pages: NonLicensedInvoicesPage, SuperAdminOnly, CategoryScope |
| Tickets / ticket-email | dbo.CallEmailLog, dbo.CallTicket | Status | Status, EmailType | - | CreatedAt | CallITMonitoring, pages: - |
| Admin / department-accounts | dbo.DepartmentAccount | Company, Department, Branch, Status | Status, Company, Department, Branch | - | CreatedAt | AdminPortal, InventorySystem, pages: DepartmentAccountsPage, AdminOnly, default status: Active |
| Admin / holidays | dbo.CompanyHoliday | Status | Status, HolidayType | - | StartDate | AdminPortal, pages: HolidayManagementPage, AdminOnly, default status: Active |
| Admin / permissions | dbo.UserPermissionItem, dbo.[User], dbo.PermissionItem | Status | PermissionType, Status | - | CreatedAt | AdminPortal, pages: UserPortalAccessPage, AdminOnly, DeveloperOnly |
| Admin / system-email | dbo.SystemEmailLog, dbo.[User] | Status | Status, Template, EntityType | - | CreatedAt | AdminPortal, pages: EmailLogsPage, AdminOnly |
| Admin / user-positions | dbo.vw_UserPositionSummary | Company, Branch | Company, Branch | Employees | - | AdminPortal, pages: UserPositionsPage, AdminOnly |
| Admin / approver-titles | dbo.ApprovalRoleTitle | Status | Role, Status | - | CreatedAt | AdminPortal, pages: AccountPermissionsPage, AdminOnly |
| Admin / branch-acronyms | dbo.Branch | Status | Status | - | - | AdminPortal, pages: BranchAcronymPage, AdminOnly |
| Admin / department-acronyms | dbo.Department | Status | Status | - | - | AdminPortal, pages: DepartmentAcronymPage, AdminOnly |
| Admin / role-portals | dbo.RolePortalAccess, dbo.Role, dbo.Portal | Status | Portal, Status | - | - | AdminPortal, pages: PortalAccessPage, AdminOnly, DeveloperOnly |
| Admin / user-portals | dbo.UserPortalAccess, dbo.[User], dbo.Portal | Status | Portal, Status | - | - | AdminPortal, pages: UserPortalAccessPage, AdminOnly, DeveloperOnly |
| Admin / role-permissions | dbo.RolePermissionItem, dbo.Role, dbo.PermissionItem | Status | PermissionType, Status | - | CreatedAt | AdminPortal, pages: UserPortalAccessPage, AdminOnly, DeveloperOnly |

## Registered screen context

The shared Ask Yakumi window is available from all nine desktop module families and Ctrl+Shift+Y. Screen detection uses known control types; selected-record context reads only allowlisted positive numeric persisted keys. Editor inputs and arbitrary control text are never read. A screen mapped to a related lookup does not imply the lookup reproduces every UI tab/filter. Acronym wrappers share one control type, so ask for the branch/department source explicitly. Screens without a live adapter receive curated guidance or an explicit unsupported answer.

| Module | Page | Data capability | Context |
| --- | --- | --- | --- |
| Inventory | ViewItemsPage | items | registered saved-record lookup |
| Inventory | ViewInventoryPage | inventory | registered saved-record lookup |
| Inventory | ViewAssetPage | assets | registered saved-record lookup |
| Inventory | ViewFixedAssetsPage | assets | registered saved-record lookup |
| Inventory | ViewWarrantyPage | warranties | registered saved-record lookup |
| Inventory | ViewEmployeesPage | employees | registered saved-record lookup |
| Inventory | ViewCompaniesPage | companies | registered saved-record lookup |
| Inventory | ViewBranchesPage | branches | registered saved-record lookup |
| Inventory | ViewDepartmentsPage | departments | registered saved-record lookup |
| Inventory | ViewVendorsPage | vendors | registered saved-record lookup |
| Inventory | ViewSetsPage | sets | registered saved-record lookup |
| Inventory | ViewArchivePage | archive | registered saved-record lookup |
| Inventory | ViewItemMovementAuditPage | item-history | registered saved-record lookup |
| Inventory | ViewOwnershipHistoryPage | ownership-history | registered saved-record lookup |
| Inventory | ViewRepairedItemsPage | item-repairs | registered saved-record lookup |
| Inventory | ViewCategoriesPage | categories | registered saved-record lookup |
| Inventory | ViewUpdatesPage | set-updates | registered saved-record lookup |
| Inventory | SetDispatchNotificationPage | sets | registered saved-record lookup |
| Requests | ViewRequestsPage | requests | registered saved-record lookup |
| Requests | PartiallyFulfilledRequestsPage | requests | registered saved-record lookup |
| Requests | UnfulfilledRequestsPage | requests | registered saved-record lookup |
| Requests | DepartmentRequestHistoryPage | requests | registered saved-record lookup |
| Consumables | ConsumableModelsPage | consumables | registered saved-record lookup |
| Consumables | ViewCartridgeModelsPage | cartridges | registered saved-record lookup |
| Consumables | ViewCartridgeRequestsPage | exchanges | registered saved-record lookup |
| Consumables | UnfulfilledCartridgeExchangesPage | exchanges | registered saved-record lookup |
| Consumables | VendorCartridgeRefillPage | refills | registered saved-record lookup |
| Consumables | ViewCartridgeTrackingPage | refills | registered saved-record lookup |
| Reports | ViewInvoicePage | invoices | registered saved-record lookup |
| Reports | ViewInvoicePreparationPage | invoice-preparation | registered saved-record lookup |
| Reports | InvoiceLicenseReviewPage | license-review | registered saved-record lookup |
| Reports | NonLicensedInvoicesPage | nonlicensed | registered saved-record lookup |
| Reports | ViewReceiptsPage | receipts | registered saved-record lookup |
| Reports | ViewRenewalPage | renewals | registered saved-record lookup |
| Reports | ViewRenewalGroupPage | renewals | registered saved-record lookup |
| Borrowing | BorrowItemsDashboard | borrows | registered saved-record lookup |
| Tickets | WpfCallMonitoringWorkspace | tickets | registered saved-record lookup |
| Tickets | WpfCallMonitoringProfilesHostControl | employees | registered saved-record lookup |
| Tickets | WpfEmailLogView | ticket-email | registered saved-record lookup |
| Repairs | RepairTicketListView | repairs | registered saved-record lookup |
| Repairs | RepairPartsView | repair-parts | registered saved-record lookup |
| Admin | UserAccountManagementPage | accounts | registered saved-record lookup |
| Admin | AccountPermissionsPage | approver-titles | registered saved-record lookup |
| Admin | UserActivityPage | audit | registered saved-record lookup |
| Admin | RoleManagementPage | roles | registered saved-record lookup |
| Admin | EmployeeManagementPage | employees | registered saved-record lookup |
| Admin | DepartmentAccountsPage | department-accounts | registered saved-record lookup |
| Admin | UserPositionsPage | user-positions | registered saved-record lookup |
| Admin | PortalAccessPage | role-portals | registered saved-record lookup |
| Admin | UserPortalAccessPage | user-portals | registered saved-record lookup |
| Admin | HolidayManagementPage | holidays | registered saved-record lookup |
| Admin | BranchAcronymPage | branch-acronyms | registered saved-record lookup |
| Admin | DepartmentAcronymPage | department-acronyms | registered saved-record lookup |
| Admin | SmtpSettingsPage | - | guidance only; no data adapter |
| Admin | EmailConfigurationPage | - | guidance only; no data adapter |
| Admin | EmailLogsPage | system-email | registered saved-record lookup |
| Admin | ConsumableStockMonitorPage | consumables | registered saved-record lookup |
| Admin | ConnectedDevicesPage | - | guidance only; no data adapter |
| Admin | MyAccountPage | - | guidance only; no data adapter |
| Admin | QrImageBackfillPage | - | guidance only; no data adapter |
| Admin | ReceiptBackfillPage | - | guidance only; no data adapter |
| Inventory | AddItemPage | items | workflow only; unsaved fields excluded |
| Inventory | AddEmployeePage | employees | workflow only; unsaved fields excluded |
| Inventory | AddRequestPage | requests | workflow only; unsaved fields excluded |
| Inventory | AddCompanyDialog | companies | workflow only; unsaved fields excluded |
| Inventory | AddBranchDialog | branches | workflow only; unsaved fields excluded |
| Inventory | AddDepartmentDialog | departments | workflow only; unsaved fields excluded |
| Inventory | AddVendorDialog | vendors | workflow only; unsaved fields excluded |
| Inventory | AddSalesInvoiceSet | invoices | workflow only; unsaved fields excluded |
| Inventory | AddRequestSet | sets | workflow only; unsaved fields excluded |

Other page-permission keys in MainForm without a dedicated context adapter: -. The assistant entry still works, but these keys do not provide selected-record context.

## Guide references

Authored articles contain reviewed workflow prose with source paths. Navigation-only articles are generated from registered capabilities and do not claim a detailed workflow. At runtime the app loads only its embedded JSON; it does not crawl the repository, configuration files or database credentials.

| Guide | Module | Capability | Type | Reviewed references |
| --- | --- | --- | --- | --- |
| workspace | Workspace | - | authored guide | Yakult.Inventory.App/Wpf/Portal/WpfMainDashboardShell.cs, Yakult.Inventory.App/Pages/User/MainForm.cs |
| identity | Workspace | - | authored guide | Yakult.Inventory.App/Session/AppSession.cs, Yakult.Inventory.App/Security/PermissionResolver.cs |
| stock | Inventory | items | authored guide | Yakult.Inventory.App/Repositories/ItemRepository.cs, Yakult.Inventory.App/Services/AssistantCatalog.cs |
| movement | Inventory | inventory | authored guide | Yakult.Inventory.App/Repositories/ItemMovementAuditRepository.cs, Yakult.Inventory.App/Repositories/InventoryRepository.cs |
| ownership | Inventory | sets | authored guide | Yakult.Inventory.App/Repositories/SetRepository.cs, Yakult.Inventory.App/Pages/User/MainForm.cs |
| assets | Inventory | assets | authored guide | Yakult.Inventory.App/Repositories/AssetRepository.cs, Yakult.Inventory.App/Pages/User/MainForm.cs |
| warranty | Inventory | warranties | authored guide | Yakult.Inventory.App/Repositories/SearchRepository.cs, Yakult.Inventory.App/Pages/User/MainForm.cs |
| request-submit | Requests | requests | authored guide | Yakult.Inventory.App/Services/RequesterPortalService.cs, Yakult.Inventory.App/Repositories/RequestRepository.cs |
| request-fulfillment | Requests | requests | authored guide | Yakult.Inventory.App/Repositories/RequestRepository.cs |
| authorization | Requests | approvals | authored guide | Yakult.Inventory.App/Services/CartridgeApprovalService.cs, Yakult.Inventory.App/Services/AuthorizationService.cs |
| consumables | Consumables | consumables | authored guide | Yakult.Inventory.App/Repositories/ConsumableModelRepository.cs |
| cartridges | Consumables | cartridges | authored guide | Yakult.Inventory.App/Repositories/CartridgeManagementRepository.cs, Yakult.Inventory.App/Repositories/CartridgeModelRepository.cs |
| refills | Consumables | refills | authored guide | Yakult.Inventory.App/Repositories/CartridgeRefillRepository.cs, Yakult.Inventory.App/Services/CartridgeRefillService.cs |
| exchange | Consumables | exchanges | authored guide | Yakult.Inventory.App/Repositories/UnfulfilledCartridgeExchangeRepository.cs, Yakult.Inventory.App/Services/CartridgeExchangeService.cs |
| ticket-create | Tickets | tickets | authored guide | Yakult.Inventory.App/Forms/CallMonitoringDashboard.cs, Yakult.Inventory.App/Repositories/CallMonitoringRepository.Tickets.Workflow.cs |
| ticket-status | Tickets | tickets | authored guide | docs/itcm/reference/status-and-priority.md, Yakult.Inventory.App/Repositories/CallMonitoringRepository.Reports.SolvedSummary.cs |
| ticket-assignment | Tickets | tickets | authored guide | Yakult.Inventory.App/Repositories/CallMonitoringRepository.Tickets.Core.cs, Yakult.Inventory.App/Services/AssistantCatalog.cs |
| ticket-email | Tickets | tickets | authored guide | docs/itcm/explanation/escalation-and-notification-logic.md, docs/itcm/how-to/troubleshooting.md |
| borrow | Borrowing | borrows | authored guide | docs/borrow/how-to/operate-borrow-and-return.md, Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs |
| borrow-return | Borrowing | borrows | authored guide | docs/borrow/reference/policies-and-known-gaps.md, Yakult.Inventory.App/Repositories/BorrowItemsRepository.cs |
| repair | Repairs | repairs | authored guide | Yakult.Inventory.App/Repositories/RepairTicketRepository.Tickets.cs, Yakult.Inventory.App/Wpf/RepairPortal/Shell/Views/RepairPortalShellWindow.xaml.cs |
| repair-parts | Repairs | repair-parts | authored guide | Yakult.Inventory.App/Repositories/RepairTicketRepository.Parts.cs, Yakult.Inventory.App/Repositories/RepairTicketRepository.Report.cs |
| invoice | Reports | invoices | authored guide | Yakult.Inventory.App/Repositories/InvoiceRepository.cs, DATABASES/Yakult-DB-Production/dbo/Views/vw_Invoices.sql |
| invoice-groups | Reports | invoice-items | authored guide | Yakult.Inventory.App/Repositories/SetItemParentTagGroupHelper.cs, Yakult.Inventory.App/Repositories/InvoiceRepository.cs |
| invoice-preparation | Reports | invoices | authored guide | Yakult.Inventory.App/Repositories/InvoicePreparationRepository.cs, Yakult.Inventory.App/Repositories/InvoiceLicenseReviewRepository.cs, Yakult.Inventory.App/Repositories/NonLicensedInvoiceRepository.cs |
| renewal | Reports | renewals | authored guide | Yakult.Inventory.App/Repositories/RenewalRepository.cs |
| receipt | Reports | receipts | authored guide | Yakult.Inventory.App/Repositories/ReceiptSetRepository.cs |
| reports | Reports | invoices | authored guide | Yakult.Inventory.App/Helpers/ReportLauncher.cs, Yakult.Inventory.App/Repositories/CallMonitoringRepository.Reports.Analytics.cs |
| archive | Inventory | archive | authored guide | Yakult.Inventory.App/Repositories/ArchiveRepository.cs |
| master-data | Inventory | employees | authored guide | Yakult.Inventory.App/Repositories/EmployeeRepository.cs, Yakult.Inventory.App/Pages/User/MainForm.cs |
| accounts | Admin | accounts | authored guide | Yakult.Inventory.App/Security/PermissionResolver.cs, Yakult.Inventory.App/Forms/Admin/AdminPortalForm.cs |
| audit | Admin | audit | authored guide | Yakult.Inventory.App/Repositories/UserActivityRepository.cs, Yakult.Inventory.App/Services/ActivityLogger.cs |
| admin-settings | Admin | accounts | authored guide | Yakult.Inventory.App/Forms/Admin/AdminPortalForm.cs, docs/itcm/how-to/admin-runbook.md |
| lookup-companies | Inventory | companies | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-branches | Inventory | branches | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-departments | Inventory | departments | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-vendors | Inventory | vendors | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-set-items | Inventory | set-items | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-ticket-history | Tickets | ticket-history | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-repair-history | Repairs | repair-history | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-roles | Admin | roles | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-policies | Workspace | policies | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-item-history | Inventory | item-history | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-ownership-history | Inventory | ownership-history | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-set-updates | Inventory | set-updates | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-item-repairs | Inventory | item-repairs | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-categories | Inventory | categories | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-conditions | Inventory | conditions | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-invoice-preparation | Reports | invoice-preparation | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-license-review | Reports | license-review | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-nonlicensed | Reports | nonlicensed | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-ticket-email | Tickets | ticket-email | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-department-accounts | Admin | department-accounts | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-holidays | Admin | holidays | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-permissions | Admin | permissions | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-system-email | Admin | system-email | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-user-positions | Admin | user-positions | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-approver-titles | Admin | approver-titles | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-branch-acronyms | Admin | branch-acronyms | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-department-acronyms | Admin | department-acronyms | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-role-portals | Admin | role-portals | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-user-portals | Admin | user-portals | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
| lookup-role-permissions | Admin | role-permissions | navigation only | Yakult.Inventory.App/Services/AssistantCatalog.cs |
