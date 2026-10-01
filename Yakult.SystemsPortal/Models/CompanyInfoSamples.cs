namespace Yakult.SystemsPortal.Models;

/// <summary>
/// Code-only company FAQs and policies used when the signed-in portal demo
/// view is enabled, or when the database tables are not yet deployed.
/// They are never written to the database.
/// </summary>
public static class CompanyInfoSamples
{
    public static IReadOnlyList<CompanyFaqItem> Faqs { get; } =
    [
        new() { FaqId = -2001, Question = "How do I request an employee account?", Answer = "Submit an account request through the Register page. An administrator reviews and approves it, and you can sign in once approved.", Category = "Accounts & Access", SortOrder = 1, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
        new() { FaqId = -2002, Question = "Where do I find company policies?", Answer = "Published company policies live under Company Policy in the portal. Use search or browse by category to find the policy you need.", Category = "Policies", SortOrder = 2, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
        new() { FaqId = -2003, Question = "How do I access the Yakult Inventory System?", Answer = "Open Services & Portals and follow the Yakult Inventory access instructions. You can launch via Remote Desktop or download the installer for local use.", Category = "Systems", SortOrder = 3, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
        new() { FaqId = -2004, Question = "Who do I contact for IT support?", Answer = "Start at the IT Help Center. It lists troubleshooting guides and the right support path for account, device, and access issues.", Category = "Support", SortOrder = 4, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
        new() { FaqId = -2005, Question = "How do I reset my portal password?", Answer = "Use the password recovery option on the Login page. If your account is locked, contact an administrator through the IT Help Center.", Category = "Accounts & Access", SortOrder = 5, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
        new() { FaqId = -2006, Question = "Where are learning materials located?", Answer = "Video lessons and guides are under Learning & Awareness. Signed-in employees can track progress from My Learning.", Category = "Learning", SortOrder = 6, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 12, 0, 0, 0, DateTimeKind.Utc) },
    ];

    public static IReadOnlyList<CompanyPolicyItem> Policies { get; } =
    [
        new() { PolicyId = -2101, Title = "Acceptable Technology Use", Slug = "sample-acceptable-technology-use", Summary = "Guidelines for responsible use of company devices, accounts, and network resources.", Body = """
            ## 1. Purpose

            This policy establishes the rules for responsible use of Yakult technology resources, including company devices, user accounts, business systems, and network access. It protects company information, keeps systems reliable, and ensures every employee knows what is expected when using technology at work.

            ## 2. Scope

            This policy applies to all employees, contractors, and temporary staff who use company-provided devices, accounts, email, network connections, or business systems, whether on-site or working remotely.

            ## 3. Acceptable use

            - Use company devices and accounts for legitimate business purposes. Limited personal use is permitted as long as it does not interfere with work, consume significant resources, or violate any other policy.
            - Protect accounts with strong, unique passwords and multi-factor authentication where available. Never share passwords, one-time codes, or security answers with anyone, including IT staff.
            - Lock your workstation (Windows + L) whenever you step away, even for a short break.
            - Install software only from approved sources. Requests for new business software go through your supervisor and IT for review and licensing.

            ## 4. Prohibited activities

            - Accessing, storing, or distributing unlawful, offensive, or discriminatory material on company systems.
            - Attempting to bypass access controls, elevate privileges, or use another person's account.
            - Connecting unauthorized network equipment, personal hotspots, or rogue access points to the company network.
            - Using company email or messaging to send spam, phishing content, or mass messages without authorization.
            - Copying, removing, or transmitting confidential company data to personal devices or external services without written approval.

            ## 5. Remote access and mobile devices

            Remote connections must use the approved VPN or published remote-access path. Public Wi-Fi may only be used through the company VPN. Lost or stolen devices must be reported to IT immediately so accounts and access can be secured.

            ## 6. Monitoring and privacy

            Company systems are provided for business use. IT may monitor systems and network traffic to protect security and ensure compliance. Employees should have no expectation of privacy for data stored on company devices or transmitted over the company network.

            ## 7. Incident reporting

            Suspected malware, phishing, account compromise, or data loss must be reported to the IT Help Center immediately. Quick reporting limits damage — do not attempt to investigate or remediate a suspected incident on your own.

            ## 8. Non-compliance

            Violations may result in restricted access, disciplinary action, and, where applicable, legal proceedings.

            This is sample content for portal preview purposes. Confirm the current official policy with the Information Technology team.
            """, Category = "IT & Security", EffectiveDate = new DateTime(2026, 1, 5), SortOrder = 1, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
        new() { PolicyId = -2102, Title = "Data Protection & Confidentiality", Slug = "sample-data-protection-confidentiality", Summary = "How employee and business information must be handled, stored, and shared.", Body = """
            ## 1. Purpose

            This policy defines how Yakult collects, classifies, stores, shares, and disposes of employee, customer, and business information. Proper handling keeps the company compliant, protects privacy, and preserves customer trust.

            ## 2. Scope

            Applies to all employees and contractors who create, receive, store, or transmit company or personal information in any format, including documents, email, databases, spreadsheets, and physical records.

            ## 3. Information classification

            - **Confidential:** Financial records, employee personal data, customer lists, contracts, credentials, and strategic plans. Share strictly on a need-to-know basis.
            - **Internal:** Operational reports, internal announcements, and working documents. May be shared within the company but not published externally.
            - **Public:** Approved marketing material, published notices, and job postings.

            When in doubt about classification, treat the information as Confidential and confirm with your supervisor.

            ## 4. Handling rules

            - Store confidential information only in approved systems. Never keep it on personal USB drives, personal email, or consumer cloud storage.
            - Encrypt sensitive files and use approved sharing links with expiry dates instead of email attachments where possible.
            - Redact personal identifiers (full names with addresses, ID numbers, account numbers) from reports and presentations unless the recipient is authorized to see them.
            - Verify the recipient before sending sensitive information, especially for first-time or external recipients.

            ## 5. Retention and disposal

            Keep records only for as long as business, legal, or regulatory requirements demand. Shred physical documents containing confidential data. Permanently delete electronic copies, including backups where feasible, once the retention period ends.

            ## 6. Breach response

            Any suspected loss, unauthorized access, or disclosure of confidential information is a data incident. Stop further sharing immediately, preserve evidence, and notify IT and your supervisor within the hour. Never attempt to conceal an incident.

            ## 7. Roles and responsibilities

            Managers ensure their teams understand this policy and complete required privacy learning. Every employee is responsible for applying these rules in daily work and completing assigned data-protection courses.

            This is sample content for portal preview purposes. Confirm the current official policy with the Compliance team.
            """, Category = "Compliance", EffectiveDate = new DateTime(2026, 1, 5), SortOrder = 2, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
        new() { PolicyId = -2103, Title = "Workplace Safety & Reporting", Slug = "sample-workplace-safety-reporting", Summary = "Reporting safety concerns and following workplace safety procedures.", Body = """
            ## 1. Purpose

            This policy ensures a safe and healthy workplace by defining safety responsibilities, reporting channels, and emergency procedures for all Yakult offices, plants, and field operations.

            ## 2. Scope

            Covers all employees, contractors, and visitors at company premises, as well as employees performing field work such as deliveries, service visits, and off-site assignments.

            ## 3. General safety rules

            - Keep walkways, exits, and fire equipment clear at all times.
            - Use personal protective equipment (PPE) where required and keep it in good condition.
            - Operate machinery, vehicles, and company equipment only when trained and authorized.
            - Report spills, damaged equipment, exposed wiring, or structural hazards immediately.

            ## 4. Reporting safety concerns

            - **Urgent danger:** Alert people nearby, leave the area if necessary, and call the local emergency number, then notify your supervisor.
            - **Non-urgent hazards:** File a report through your supervisor or the designated safety coordinator within the same working day.
            - **Near misses:** Report events that could have caused injury so preventive action can be taken. Near-miss reporting is encouraged and never punished.

            ## 5. Emergency procedures

            Learn the evacuation routes, assembly points, and location of fire extinguishers and first-aid kits for your work area. During drills or real emergencies, follow the instructions of floor marshals and emergency responders. Account for visitors in your area when evacuating.

            ## 6. Field work safety

            Plan routes in advance, confirm vehicle roadworthiness, carry a charged mobile phone, and inform your supervisor of your itinerary. Stop work and seek shelter during severe weather or unsafe road conditions.

            ## 7. Health and wellness

            Report work-related injuries or illnesses promptly so treatment and documentation can begin. Reasonable accommodations for medical needs can be requested through Human Resources.

            This is sample content for portal preview purposes. Confirm the current official policy with the Safety and HR teams.
            """, Category = "Workplace", EffectiveDate = new DateTime(2026, 1, 5), SortOrder = 3, IsPublished = true, UpdatedAtUtc = new DateTime(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc) },
    ];
}
