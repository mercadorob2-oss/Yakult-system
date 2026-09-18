namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Static catalog of the Yakult Inventory WinForms app's systems and their
/// features, grouped by their portal entry point. Drives the /Home/Modules page.
/// </summary>
public static class SubsystemCatalog
{
    public static IReadOnlyList<SubsystemGroup> Groups { get; } = new List<SubsystemGroup>
    {
        new(
            Name: "Yakult Inventory App Module",
            Icon: "package",
            Summary: "The core inventory management desktop shell. Hamburger menu with master data, transactions, and history sections.",
            IsCardDriven: true,
            StartExpanded: true,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Home",
                    Description: "Splash/landing screen with the Yakult logo. Shown when 'Home' is picked from the hamburger menu."),
                new(Name: "Dashboard",
                    Description: "Inventory KPIs, category-level rollups, and the mobile-updates-pending counter that drives the bell badge."),
                new(Name: "Add Master Data",
                    Description: "Add new master-data records. 9 sub-items: Company, Branch, Department, Vendor, Employee, Item, Request, Request Set, Software/Service Set."),
                new(Name: "View Master Data",
                    Description: "Read-only list views. 13 sub-items: Inventory, Items, Repair Items, Categories, Employees, Companies, Branches, Branch Assignments, Departments, Vendors, Receipts, Fixed Assets, Assets."),
                new(Name: "View Transactions",
                    Description: "Operational transaction views. 7 sub-items: Requests, Send Notifications (Sets), Sets, Invoices, Renewals, Renewals (Grouped), Warranty."),
                new(Name: "View History",
                    Description: "Audit trail. 3 sub-items: Item Audit Trail, Updates, Archive."),
                new(Name: "My Account",
                    Description: "User's own profile and account settings. Also reachable from the WPF portal 'My Account' header and Admin Portal."),
                new(Name: "User Manual",
                    Description: "External link to the GitHub Pages-hosted user manual.")
            }),

        new(
            Name: "IT Call Monitoring",
            Icon: "clipboard",
            Summary: "Helpdesk ticket lifecycle, SLA tracking, escalations, SMTP-based email notifications, and live wallboard for active tickets.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "IT Call Monitoring",
                    Description: "Dashboard, Ticket List, Display Mode (Kanban wallboard), Email Notification, Reports, Diagnostics, Profiles. Plus 15+ dialogs (ticket details, escalation override, SMTP profile, etc.).")
            }),

        new(
            Name: "Requester Portal",
            Icon: "users",
            Summary: "In-WinForms portal for submitting and approving cartridge requests. Self-Request or Assisted Request (IT staff). Department account sessions are auto-routed here.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Requester Portal",
                    Description: "New Request, Assisted Request, Request History, Approvals (visible to positions in IsApprover list), Authorization History. Plus 3 dialogs: Request Detail, Authorization Detail, Notification Dropdown.")
            }),

        new(
            Name: "Cartridge Management",
            Icon: "tool",
            Summary: "Cartridge lifecycle: exchange, refill, fulfillment, batch tracking, disposal. Default landing page is the Cartridge Exchange WPF workspace.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Cartridge Management",
                    Description: "Cartridge Exchange, Send Notifications, View Cartridges, View Cartridge Sets, Partially Fulfilled, Unfulfilled Exchanges, Cartridge Tracking, Sold Cartridges, Disposed Cartridges, Damaged Empties, Outbound Batches, Non-Refillable Empties, Cartridge Refill Batch, Cartridge Models, Authorization Monitor. Plus 10+ dialogs (return, edit batch, outbound assignment, etc.).")
            }),

        new(
            Name: "Borrow Items",
            Icon: "layers",
            Summary: "Hardware borrow/return log. Records who borrowed which serial-numbered item. Supports backdating and a 'Resolve' flow for unknown items.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Borrow Items",
                    Description: "Board (visual kanban), Open (active borrows), History (returned items). Plus 6 dialogs: Add External, Borrow Details, Confirm Borrow, Confirm Return, Return Borrow, Quick Add Employee.")
            }),

        new(
            Name: "Reports",
            Icon: "bar-chart",
            Summary: "Generates and exports operational reports as RDLC preview windows (Excel/PDF). Default landing page is the View Invoice report.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Reports",
                    Description: "View Invoice, View Sets, View Renewals, View Renewals (Grouped), Cartridge Disposed/Sold, Export Picker. Plus 5 export sub-flows (Invoice, Sets, Renewal, Renewals Grouped, Cartridge Disposed/Sold).")
            }),

        new(
            Name: "Admin Portal",
            Icon: "database",
            Summary: "Administrative configuration: user accounts, employee records, role-based access, reference data, SMTP/email. Requires IsDeveloper or IsSuperAdmin. Default landing page is Employee Management.",
            IsCardDriven: false,
            StartExpanded: false,
            Subsystems: new List<Subsystem>
            {
                new(Name: "Admin Portal",
                    Description: "Account Management: Employee Management, Account Management, Department Accounts, Approver Management, User Activity, User Positions. Reference Data: Master Data Update, Holidays, Branch Acronyms, Dept Acronyms. Email & SMTP: SMTP Settings, Email Configuration, Email Logs. Developer Tools (IsDeveloper only): Portal Access, User Portal Access.")
            })
    };

    public static int TotalSubsystemCount =>
        Groups.Sum(g => g.Subsystems.Count);
}

public sealed record SubsystemGroup(
    string Name,
    string Icon,
    string Summary,
    bool IsCardDriven,
    bool StartExpanded,
    IReadOnlyList<Subsystem> Subsystems)
{
    public string Slug
    {
        get
        {
            var chars = Name.ToLowerInvariant()
                .Select(c => char.IsLetterOrDigit(c) ? c : '-')
                .ToArray();
            var slug = new string(chars);
            while (slug.Contains("--", StringComparison.Ordinal))
                slug = slug.Replace("--", "-", StringComparison.Ordinal);
            return slug.Trim('-');
        }
    }
}

public sealed record Subsystem(
    string Name,
    string Description);
