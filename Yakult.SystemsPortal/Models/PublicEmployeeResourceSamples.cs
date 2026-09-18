namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Code-only employee resources used only when the signed-in portal demo view is enabled.
/// They are never written to the database.
/// </summary>
public static class PublicEmployeeResourceSamples
{
    public static IReadOnlyList<PortalContentItem> Items { get; } =
    [
        new()
        {
            ContentId = -1001,
            ContentType = "IT Handbook",
            Slug = "sample-employee-handbook-2026",
            Title = "IT Service Handbook 2026",
            Summary = "A practical reference for Yakult account, device, system access, and IT support procedures.",
            Body = """
                ## Start with IT

                The IT Service Handbook is a quick reference for using Yakult technology safely and getting help when an account, device, or business system needs attention.

                ## Topics covered

                - IT Service Desk support and escalation paths
                - Account, device, and business-system access
                - Data protection, acceptable technology use, and confidentiality
                - Security reporting and lost-device response
                - Approved software, equipment, and remote-access guidance

                This sample resource is for portal preview purposes. Confirm current request forms and support channels with the Information Technology team before submitting a request.
                """,
            CategoryName = "IT Services",
            CategorySlug = "employee-resources",
            Status = "Published",
            SortOrder = 1,
            PublishStartUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        },
        new()
        {
            ContentId = -1002,
            ContentType = "IT Guide",
            Slug = "sample-leave-and-attendance-guide",
            Title = "IT Access and Account Guide",
            Summary = "Understand account activation, access requests, sign-in changes, and the right IT contact when access changes.",
            Body = """
                ## Account and access checklist

                Request access through the approved system-owner path and use the current Yakult sign-in page for the service.

                ## When access changes

                1. Confirm the affected system, account, device, and time of the issue.
                2. Use the approved account recovery process when an account is locked.
                3. Report unexpected sign-in prompts or suspected compromise to IT immediately.
                4. Never include a password, one-time code, or security answer in a request.

                Use the IT Service Desk for current access-request and account-support options.
                """,
            CategoryName = "IT Services",
            CategorySlug = "employee-resources",
            Status = "Published",
            SortOrder = 2,
            PublishStartUtc = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 6, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        },
        new()
        {
            ContentId = -1003,
            ContentType = "IT Guide",
            Slug = "sample-it-support-quick-guide",
            Title = "IT Support Quick Guide",
            Summary = "Fast first steps for account access, password, device, network, and business-system issues.",
            Body = """
                ## Try these first

                - Restart the affected application or device and check whether the issue is limited to one system.
                - Confirm that you are using the correct Yakult account and the current sign-in page for the service.
                - Capture the exact error message, the time it occurred, and the system or device involved.
                - Never share your password, one-time code, or security answers in a ticket or message.

                ## When to contact IT Help Center

                Submit an IT request when access remains unavailable, a device is lost or compromised, a business system behaves unexpectedly, or you need approved software or equipment. Include screenshots only when they do not expose passwords, personal information, or confidential business data.

                Use the IT Help Center for the current ticket and support contact options.
                """,
            CategoryName = "IT Services",
            CategorySlug = "employee-resources",
            Status = "Published",
            SortOrder = 3,
            PublishStartUtc = new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 7, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        },
        new()
        {
            ContentId = -1004,
            ContentType = "IT FAQ",
            Slug = "sample-benefits-and-wellness-faq",
            Title = "IT Access and Security FAQ",
            Summary = "Answers to common questions about account access, suspicious messages, device security, and confidential technology support.",
            Body = """
                ## Common questions

                **How can I confirm my current system access?**

                Open the approved system directory and contact IT when your assigned responsibilities require additional access.

                **What should I do with a suspicious message?**

                Do not click unknown links or provide credentials. Report the message through the approved IT security channel.

                **Who can see a personal technology question?**

                Use the approved confidential IT support channel for account, device, or security details. Do not place passwords, recovery codes, or sensitive information in a general portal comment.
                """,
            CategoryName = "IT Services",
            CategorySlug = "employee-resources",
            Status = "Published",
            SortOrder = 4,
            PublishStartUtc = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 8, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        },
        new()
        {
            ContentId = -1005,
            ContentType = "IT Policy",
            Slug = "sample-workplace-safety-guide",
            Title = "IT Workspace and Device Safety Guide",
            Summary = "Secure workstation, device-handling, data-protection, and incident-reporting practices.",
            Body = """
                ## Secure your IT workspace

                Lock your screen, use approved equipment, protect confidential information, and report lost or compromised devices promptly.

                ## If a technology incident occurs

                1. Disconnect or isolate the affected device when instructed by IT.
                2. Notify the IT Service Desk or security contact as soon as possible.
                3. Preserve relevant facts, including the system, time, device, and visible message.
                4. Follow the approved incident-response process and do not investigate beyond your instructions.

                Never bypass a security control to keep work moving. Stop and contact IT when an instruction or condition is unclear.
                """,
            CategoryName = "Cybersecurity",
            CategorySlug = "policies-faqs",
            Status = "Published",
            SortOrder = 5,
            PublishStartUtc = new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 9, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        },
        new()
        {
            ContentId = -1006,
            ContentType = "IT Guide",
            Slug = "sample-hr-forms-and-requests-guide",
            Title = "IT Access Request Forms Guide",
            Summary = "Find the right starting point for system access, software, device, and IT support requests.",
            Body = """
                ## Common IT requests

                Information Technology can help route requests for business-system access, approved software, device replacement, remote access, and technology support.

                ## Prepare before you submit

                - Choose the request type that best describes your need.
                - Identify the affected system, device, business need, and requested access.
                - Attach the current request form only through the approved IT channel.
                - Keep passwords, authentication codes, and sensitive personal data out of portal comments.

                If you are unsure which form applies, contact the IT Service Desk before sending files or access details.
                """,
            CategoryName = "IT Services",
            CategorySlug = "employee-resources",
            Status = "Published",
            SortOrder = 6,
            PublishStartUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            CreatedAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAtUtc = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            AuthorName = "Yakult Information Technology"
        }
    ];
}
