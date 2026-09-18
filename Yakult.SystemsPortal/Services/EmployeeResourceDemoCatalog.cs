using Yakult.SystemsPortal.Models;

namespace Yakult.SystemsPortal.Services;

public interface IEmployeeResourceCatalog
{
    IReadOnlyList<EmployeeResourceItem> GetPublished();
    EmployeeResourceItem? FindPublishedBySlug(string slug);
}

/// <summary>
/// Frontend-first Employee Resources catalog. A future database repository can
/// replace this implementation without changing the public view contract.
/// </summary>
public sealed class EmployeeResourceDemoCatalog : IEmployeeResourceCatalog
{
    private static readonly IReadOnlyList<EmployeeResourceItem> Items =
    [
        new()
        {
            Slug = "employee-handbook-2026",
            Title = "IT Service Handbook 2026",
            Summary = "A practical reference for Yakult account, device, system access, and IT support procedures.",
            Overview = "Use this handbook as a high-level reference for Yakult IT services. Confirm the current request path with the IT Service Desk before submitting an access or support request.",
            Category = "IT Support",
            ResourceType = "Handbook",
            OwnerDepartment = "Information Technology",
            Version = "v1.0",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 1, 5),
            ReviewDateUtc = Utc(2026, 12, 31),
            Highlights = ["IT Service Desk and escalation paths", "Account, device, and system access guidance", "Security and acceptable technology use"],
            Attachments =
            [
                Attachment("IT-Service-Handbook-2026.pdf", "PDF", "2.4 MB", "IT service handbook (demo attachment)"),
                Attachment("IT-Service-Desk-Checklist.docx", "DOCX", "48 KB", "Support request checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "it-access-and-account-guide",
            Title = "IT Access and Account Guide",
            Summary = "Understand account activation, access requests, sign-in changes, and the right IT contact when access changes.",
            Overview = "Use this guide before requesting access or reporting a sign-in issue. Never include passwords, one-time codes, or security answers in an open request.",
            Category = "Systems & Access",
            ResourceType = "Guide",
            OwnerDepartment = "Information Technology",
            Version = "v2.1",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 2, 12),
            ReviewDateUtc = Utc(2026, 11, 30),
            Highlights = ["Request access through the approved system-owner path", "Report unexpected sign-in prompts to IT", "Use the account recovery process for locked accounts"],
            Attachments =
            [
                Attachment("IT-Access-and-Account-Guide.pdf", "PDF", "1.1 MB", "Account access guidance (demo attachment)"),
                Attachment("System-Access-Request-Form.docx", "DOCX", "72 KB", "Access request template (demo attachment)"),
                Attachment("Account-Recovery-Checklist.xlsx", "XLSX", "36 KB", "Recovery checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "it-access-request-forms",
            Title = "IT Access Request Forms",
            Summary = "Find the correct starting point for system access, software, device, and support requests.",
            Overview = "Choose the form that best matches the IT request and submit it through the approved service channel. Do not upload passwords, authentication codes, or sensitive personal data.",
            Category = "Systems & Access",
            ResourceType = "Form",
            OwnerDepartment = "Information Technology",
            Version = "v1.3",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 3, 4),
            ReviewDateUtc = Utc(2026, 10, 31),
            Highlights = ["System and application access", "Software and device requests", "Request details IT needs to respond"],
            Attachments =
            [
                Attachment("IT-Request-Index.pdf", "PDF", "620 KB", "Request selection guide (demo attachment)"),
                Attachment("System-Access-Request.docx", "DOCX", "54 KB", "Editable request template (demo attachment)"),
                Attachment("Device-Request-Checklist.xlsx", "XLSX", "29 KB", "Device request checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "it-workspace-and-device-safety-guide",
            Title = "IT Workspace and Device Safety Guide",
            Summary = "Secure workstation, device-handling, data-protection, and incident-reporting practices.",
            Overview = "Protect company devices and information while working. Lock screens, use approved equipment, report lost devices quickly, and contact IT when a security instruction is unclear.",
            Category = "Cybersecurity",
            ResourceType = "Policy",
            OwnerDepartment = "Information Technology",
            Version = "v3.0",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 2, 26),
            ReviewDateUtc = Utc(2027, 1, 31),
            Highlights = ["Protect devices and sign-in sessions", "Report lost or compromised equipment promptly", "Follow data protection and security guidance"],
            Attachments =
            [
                Attachment("IT-Workspace-and-Device-Safety.pdf", "PDF", "1.8 MB", "Device security policy (demo attachment)"),
                Attachment("Device-Security-Checklist.pdf", "PDF", "940 KB", "Security checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "it-support-quick-guide",
            Title = "IT Support Quick Guide",
            Summary = "Fast first steps for account access, password, device, network, and business-system issues.",
            Overview = "Capture the exact error, time, affected system, and device before opening an IT request. Never share a password, one-time code, or security answer in a ticket or message.",
            Category = "IT Support",
            ResourceType = "Guide",
            OwnerDepartment = "Information Technology",
            Version = "v1.4",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 4, 18),
            ReviewDateUtc = Utc(2026, 12, 15),
            Highlights = ["Confirm the correct Yakult sign-in page", "Record the exact error message", "Use IT Help Center when access remains unavailable"],
            Attachments =
            [
                Attachment("IT-Support-Quick-Guide.pdf", "PDF", "780 KB", "Troubleshooting guide (demo attachment)"),
                Attachment("New-Device-Setup-Checklist.docx", "DOCX", "41 KB", "Setup checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "new-device-setup-pack",
            Title = "New Device Setup Pack",
            Summary = "A grouped starting point for preparing a Yakult device, account, approved software, and secure working environment.",
            Overview = "Use this pack for IT-led device setup. Follow the Service Desk instructions and do not record passwords or recovery codes in the checklist.",
            Category = "IT Operations",
            ResourceType = "Checklist",
            OwnerDepartment = "Information Technology",
            Version = "v1.0",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 1, 20),
            ReviewDateUtc = Utc(2026, 9, 30),
            Highlights = ["Prepare device and account details", "Install approved software and updates", "Confirm security controls before use"],
            Attachments =
            [
                Attachment("New-Device-Setup-Pack.zip", "ZIP", "4.7 MB", "Grouped device setup files (demo attachment)"),
                Attachment("First-Login-and-Setup-Checklist.pdf", "PDF", "1.2 MB", "First-login checklist (demo attachment)")
            ]
        },
        new()
        {
            Slug = "it-service-contacts-and-outage-notices",
            Title = "IT Service Contacts and Outage Notices",
            Summary = "Reference IT support contacts, service interruption notices, escalation paths, and system-status guidance.",
            Overview = "Use this page during an IT service disruption. Check the latest notice, record the affected system and time, and follow the IT Service Desk escalation path.",
            Category = "IT Service Management",
            ResourceType = "Directory",
            OwnerDepartment = "Information Technology",
            Version = "v1.2",
            Status = "Published",
            LastUpdatedUtc = Utc(2026, 5, 2),
            ReviewDateUtc = Utc(2026, 12, 1),
            Highlights = ["IT Service Desk contact path", "System outage and maintenance notices", "Escalation details for urgent access issues"],
            Attachments =
            [
                Attachment("IT-Service-Contacts.pdf", "PDF", "530 KB", "IT contact sheet (demo attachment)"),
                Attachment("System-Status-Checklist.xlsx", "XLSX", "24 KB", "Service status checklist (demo attachment)")
            ]
        }
    ];

    public IReadOnlyList<EmployeeResourceItem> GetPublished() =>
        Items.Where(item => item.Status.Equals("Published", StringComparison.OrdinalIgnoreCase)).ToList();

    public EmployeeResourceItem? FindPublishedBySlug(string slug) =>
        GetPublished().FirstOrDefault(item => item.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    private static EmployeeResourceAttachment Attachment(string fileName, string format, string size, string description) =>
        new() { FileName = fileName, Format = format, SizeLabel = size, Description = description };

    private static DateTime Utc(int year, int month, int day) =>
        new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
}
