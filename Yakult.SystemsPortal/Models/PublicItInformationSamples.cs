namespace Yakult.SystemsPortal.Models;

/// <summary>Curated employee summaries for the existing local demo. Originals are never served.</summary>
public static class PublicItInformationSamples
{
    public const string SlugPrefix = "demo-it-information-";
    public static IReadOnlyList<ItInformationResource> Resources { get; } =
    [
        new()
        {
            ContentId = -3101, Slug = SlugPrefix + "policy-and-guidelines", Kind = "Employee guidelines", Accent = "policy",
            Title = "IT Policy & Employee Guidelines",
            Summary = "Read the Workplace Guidelines, Profile Account requirements, Internet Access procedure, and IT Policy flowcharts.",
            SourceName = "IT Policy.docx",
            Sections =
            [
                new()
                {
                    Id = "workplace", Title = "Workplace Guidelines", Summary = "Use company equipment responsibly and protect your account and work information.",
                    SourceSection = "Workplace Guidelines",
                    Points = ["Internal movement of hardware and peripherals must be handled by IT staff with prior IT Department approval.", "Do not connect non-Yakult hardware to the Yakult network.", "Do not install nonstandard software, including games, utilities, media players, screensavers, or desktop themes.", "Ensure the computer's virus protection is kept up to date.", "Notify IT staff before relocating workstations or printers.", "Keep system units on top of the working area to help minimize dirt and dust entering the equipment.", "Take care of your assigned monitor, system unit, mouse, keyboard, and printer. Employees are responsible for company equipment in their care.", "Keep your username and password private; do not let others use your login profile.", "Lock your computer or laptop when leaving it unattended.", "IT security incidents require a written explanation and are referred to the Personnel Department for handling under the policy."]
                },
                new()
                {
                    Id = "accounts", Title = "Profile Account", Summary = "Creation of an Active Domain Account for users at the Active Directory Server.",
                    SourceSection = "Profile Account / Active Domain Account Process (document page 11)", SourceImageFile = "profile-account.svg",
                    Points = ["Employees using company computers must have their own account, created with their profile information and photo.", "Sharing usernames and passwords is not allowed. Each user is accountable for use of their login profile.", "Enable Standard Account Policy.", "Standard software installation must be performed by an IT Administrator only.", "Access the Yakult official website.", "Enable the PROXY network setup. The source specifies an open-web-access setup for the YMC Webinar Computer, which is not connected to the network.", "Set the Yakult screensaver."],
                    ProcessTitle = "Active Domain Account Process",
                    ProcessStages =
                    [
                        new() { Title = "Account requisition", Owner = "New user / Department Head", Points = ["Complete an account requisition form and obtain department-head approval.", "Forms are available at the IT Department."] },
                        new() { Title = "Account registration & password", Owner = "IT Administrator", Points = ["Register the new account on the Active Domain Server.", "Provide the username and temporary password.", "The source requires a password that does not contain the user's name, with a minimum of 7 characters combining alphanumeric and special characters.", "Give instructions on how to reset the password."] },
                        new() { Title = "Resignation & account closure", Owner = "IT Administrator / IT Manager", Points = ["Disable the account of a resigned employee.", "The employee's clearance must be signed by the IT Manager.", "Deactivate the AD account from the resignation date.", "Delete the AD account after 3–6 months, the retention period stated for backup files and other business-related transactions."] }
                    ]
                },
                new()
                {
                    Id = "internet", Title = "Internet Access", Summary = "Internet controls help protect company information, reduce harmful downloads, and maintain network performance.",
                    SourceSection = "Internet Access; Guidelines; Internet Access Process",
                    Points = ["Company internet access is subject to restrictions intended to reduce hacking, identity theft, data leakage, viruses, spam, and work interruptions.", "Some websites and file types are restricted for security and bandwidth management.", "Torrent and peer-to-peer downloads are restricted to help prevent dangerous or infected files."],
                    StepsTitle = "Request internet or website access",
                    Steps = ["Prepare a request identifying the requesting party, the website or access needed, and the business reason.", "Have the request noted or approved by your department head.", "Obtain the Comptroller's approval.", "Submit the approved request to the IT Department."]
                },
                new()
                {
                    Id = "flowcharts", Title = "Flowcharts", Summary = "Follow the process, see who handles each step, and check the approval or decision points.",
                    SourceSection = "Flowchart 1 / Hardware Deployment Process; Flowchart 2 / Internet Access Process; Flowchart 3 / File Sharing Folder Guidelines",
                    Flowcharts =
                    [
                        new()
                        {
                            Id = "hardware", Title = "Hardware Deployment Process", Number = 1,
                            Summary = "The original diagram preserves the IT Specialist, Department, and IT Secretary lanes, including connectors A and B.", ImageFile = "hardware-deployment.svg",
                            Paths =
                            [
                                new() { Title = "Deployment path", Steps = ["Start → IT Specialist receives hardware.", "Check whether deployment is approved. No loops back to the same approval decision.", "Yes → confirm location and assign FA number, if applicable.", "IT Secretary creates the transmittal form → connector B.", "Connector B returns to the IT Specialist → deploy the hardware.", "Department receives a copy of the transmittal form.", "IT Secretary files the transmittal form → adds or updates item monitoring → End."] },
                                new() { Title = "Item-monitoring path · connector A", Steps = ["From Received Hardware, connector A goes to the IT Secretary's Add / Update Item Monitoring step.", "That monitoring step leads to End."] }
                            ]
                        },
                        new()
                        {
                            Id = "internet-access", Title = "Internet Access Process", Number = 2,
                            Summary = "The original diagram separates the Department and Approver lanes and keeps both approvals in their source order.", ImageFile = "internet-access.svg",
                            Paths =
                            [
                                new() { Title = "Request and approval path", Steps = ["Start → Department provides a letter of request for Internet / Website / E-mail / others.", "Approver lane: Department Head approval → Comptroller approval.", "Return to Department lane: forward the request to the IT Department → End."] }
                            ]
                        },
                        new()
                        {
                            Id = "file-sharing", Title = "File Sharing Folder Guidelines", Number = 3,
                            Summary = "The original diagram includes the User and Administrator lanes, the validation loop, connector A, and both Administrator paths.", ImageFile = "file-sharing.png",
                            SourceNote = "The diagram labels file-access validation as 'within two months' and its backup branch as 'one month duration to delete'. The Data Security prose separately specifies a two-month retention period and notification before deletion. These timings are retained as written; their application needs confirmation from the policy owner.",
                            Paths =
                            [
                                new() { Title = "User lane · sharing decisions", Steps = ["Start → share the file to the respective folder → Allowed? (File Sharing Policy).", "No → Error Prompt → End.", "Yes → save the file to the folder → other users get a copy → Still Needed?", "No → Delete File → End.", "Yes → Retain File → File Access Validation (within two months).", "The user lane continues to Notify by Administrator and loops back to Still Needed?."] },
                                new() { Title = "Administrator lane · notification path", Steps = ["File Access Validation also connects through A to Received Mail Notification in the Administrator lane.", "Administrator Start also connects to Received Mail Notification.", "Received Mail Notification → Notify User/s → Apply Changes → End."] },
                                new() { Title = "Administrator lane · backup path", Steps = ["Administrator Start → Daily Backup → One month duration to Delete → End."] }
                            ]
                        }
                    ]
                }
            ]
        },
        new()
        {
            ContentId = -3102, Slug = SlugPrefix + "meet-it-and-business-systems", Kind = "Department & systems", Accent = "team",
            Title = "Meet IT & Our Business Systems",
            Summary = "Get to know the IT Department, understand each team's responsibilities, and explore the systems that support daily work.",
            SourceName = "IT Organization and Infrastructure.pptx",
            Sections =
            [
                new()
                {
                    Id = "purpose", Title = "What IT does", Summary = "The IT Department supports the technology people use across the business.",
                    SourceSection = "Objectives (slide 2)",
                    Points = ["Support effective and appropriate use of IT resources.", "Help keep company information and technology resources secure.", "Coordinate technology procurement, replacement, and infrastructure support.", "Provide IT training and support for employees."]
                },
                new()
                {
                    Id = "teams", Title = "Meet the IT functions", Summary = "A quick overview of responsibilities to help you understand who handles what.",
                    SourceSection = "IT Organization; Duties and Responsibilities (slides 4–10)",
                    Cards =
                    [
                        new() { Label = "Direction", Title = "IT Management", Text = "Oversees IT policies, planning, resources, and coordination with the business." },
                        new() { Label = "Infrastructure", Title = "IT Technical", Text = "Supports network infrastructure, equipment, and technical operations, including on-site assistance." },
                        new() { Label = "Applications", Title = "IT Development", Text = "Develops and supports company applications, improves systems, and maintains technical documentation." },
                        new() { Label = "Protection", Title = "Cybersecurity", Text = "Helps protect company technology and information, investigates security concerns, and provides awareness training." },
                        new() { Label = "Employee assistance", Title = "IT Support", Text = "Helps with computer, email, internet, network, and application issues, and coordinates further assistance when needed." },
                        new() { Label = "Coordination", Title = "IT Administration", Text = "Coordinates IT records, requisitions, documents, and administrative requirements." }
                    ]
                },
                new()
                {
                    Id = "systems", Title = "Business systems at a glance", Summary = "These systems support different parts of the business. This overview explains their purpose.",
                    SourceSection = "System Applications: ERP, Sales System, HRM (slides 12–14)",
                    Cards =
                    [
                        new() { Label = "ERP · Navision", Title = "Business operations", Text = "Supports finance, inventory, supply chain, manufacturing, and sales processes." },
                        new() { Label = "Sales · YDS", Title = "Field sales", Text = "Supports field representatives with sales, payments, customer visits, routes, and booking information." },
                        new() { Label = "HR · Cadena", Title = "People & attendance", Text = "Supports human resources information, time and attendance, payroll, and employee self-service." }
                    ]
                }
            ]
        }
    ];

    public static IReadOnlyList<PortalContentItem> Items { get; } = Resources.Select(resource => resource.Item).ToArray();
    public static bool IsSampleSlug(string? slug) => slug?.StartsWith(SlugPrefix, StringComparison.OrdinalIgnoreCase) == true;
    public static ItInformationResource? Find(string slug) => Resources.FirstOrDefault(resource => resource.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    public static IReadOnlyList<ItInformationResource> Search(string? query)
    {
        var term = NormalizeQuery(query);
        return Resources.Where(resource => resource.SearchText.Contains(term, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public static string NormalizeQuery(string? query)
    {
        var term = (query ?? "").Trim();
        return term.Length > 200 ? term[..200] : term;
    }
}

public sealed class ItInformationResource
{
    public required int ContentId { get; init; }
    public required string Slug { get; init; }
    public required string Kind { get; init; }
    public required string Accent { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string SourceName { get; init; }
    public required IReadOnlyList<ItInformationSection> Sections { get; init; }
    public string SearchText => string.Join(" ", new[] { Title, Summary, Kind, SourceName }.Concat(Sections.Select(section => section.SearchText)));
    public PortalContentItem Item => new()
    {
        ContentId = ContentId, Slug = Slug, Title = Title, Summary = Summary, ContentType = "Article",
        Body = SearchText, CategoryName = Kind, CategorySlug = "learning", Status = "Draft", IsFeatured = true
    };
}

public sealed class ItInformationSection
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string SourceSection { get; init; }
    public IReadOnlyList<string> Points { get; init; } = [];
    public string? StepsTitle { get; init; }
    public IReadOnlyList<string> Steps { get; init; } = [];
    public IReadOnlyList<ItInformationCard> Cards { get; init; } = [];
    public IReadOnlyList<ItInformationFlowchart> Flowcharts { get; init; } = [];
    public string? ProcessTitle { get; init; }
    public IReadOnlyList<ItInformationProcessStage> ProcessStages { get; init; } = [];
    public string? SourceImageFile { get; init; }
    public string SearchText => string.Join(" ", new[] { Title, Summary, SourceSection, StepsTitle ?? "", ProcessTitle ?? "" }.Concat(Points).Concat(Steps).Concat(Cards.Select(card => $"{card.Label} {card.Title} {card.Text}")).Concat(ProcessStages.Select(stage => stage.SearchText)).Concat(Flowcharts.Select(flowchart => flowchart.SearchText)));
}

public sealed class ItInformationProcessStage
{
    public required string Title { get; init; }
    public required string Owner { get; init; }
    public required IReadOnlyList<string> Points { get; init; }
    public string SearchText => string.Join(" ", new[] { Title, Owner }.Concat(Points));
}

public sealed class ItInformationFlowchart
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required int Number { get; init; }
    public required string Summary { get; init; }
    public required string ImageFile { get; init; }
    public required IReadOnlyList<ItInformationFlowPath> Paths { get; init; }
    public string? SourceNote { get; init; }
    public string SearchText => string.Join(" ", new[] { Title, Summary, SourceNote ?? "" }.Concat(Paths.Select(path => string.Join(" ", new[] { path.Title }.Concat(path.Steps)))));
}

public sealed class ItInformationFlowPath
{
    public required string Title { get; init; }
    public required IReadOnlyList<string> Steps { get; init; }
}

public sealed class ItInformationCard
{
    public required string Label { get; init; }
    public required string Title { get; init; }
    public required string Text { get; init; }
}

public sealed class ItInformationIndexViewModel
{
    public string Query { get; init; } = "";
    public IReadOnlyList<ItInformationResource> Resources { get; init; } = [];
}
