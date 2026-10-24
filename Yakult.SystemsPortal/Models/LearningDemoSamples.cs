using System.Text.Json;

namespace Yakult.SystemsPortal.Models;

/// <summary>Code-only learning samples for the existing signed-in demo view.</summary>
public static class LearningDemoSamples
{
    public const string SlugPrefix = "demo-learning-";
    private static readonly DateTime SampleDate = new(2026, 10, 23, 0, 0, 0, DateTimeKind.Utc);
    public static IReadOnlyList<PortalContentCategory> Categories { get; } =
    [
        new() { CategoryId = -301, Name = "IT Cybersecurity", Slug = "cybersecurity" },
        new() { CategoryId = -302, Name = "IT Technical", Slug = "learning" },
        new() { CategoryId = -303, Name = "IT Support", Slug = "it-help" }
    ];
    public static IReadOnlyList<PortalContentItem> Items { get; } = BuildItems();
    public static bool IsSampleSlug(string? slug) => slug?.StartsWith(SlugPrefix, StringComparison.OrdinalIgnoreCase) == true;
    public static PortalContentItem? Find(string slug) => Items.FirstOrDefault(item => item.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

    public static LearningCatalogPage Catalog(IReadOnlyCollection<string> categories, string? query, string? type, string sort, int page, int pageSize = 12)
    {
        IEnumerable<PortalContentItem> items = Items.Where(item => categories.Contains(item.CategorySlug, StringComparer.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query)) items = items.Where(item => $"{item.Title} {item.Summary} {item.CategoryName}".Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
        var types = items.Select(item => item.ContentType).Distinct().OrderBy(value => value).ToList();
        if (!string.IsNullOrWhiteSpace(type)) items = items.Where(item => item.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase));
        items = sort switch
        {
            "title" => items.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
            "newest" => items.OrderByDescending(item => item.UpdatedAtUtc).ThenBy(item => item.SortOrder),
            _ => items.OrderByDescending(item => item.IsFeatured).ThenBy(item => item.SortOrder)
        };
        var filtered = items.ToList();
        pageSize = Math.Clamp(pageSize, 1, 48);
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(filtered.Count / (double)pageSize)));
        return new LearningCatalogPage { Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(), Types = types, TotalCount = filtered.Count, Page = page };
    }

    private static IReadOnlyList<PortalContentItem> BuildItems()
    {
        (string Title, string Slug, int Category, string Summary)[] topics =
        [
            ("Cybersecurity essentials", "cybersecurity-essentials", 0, "Spot suspicious requests, protect your accounts, and practice a safer response."),
            ("Your first week with Yakult IT", "it-onboarding", 1, "Explore a sample onboarding journey from finding your application to requesting access."),
            ("Create a better IT support ticket", "better-support-tickets", 2, "Practice explaining an issue, gathering useful details, and following up clearly."),
            ("Recognize a phishing email", "phishing-email", 0, "Compare sample messages and learn which warning signs deserve a closer look."),
            ("Passwords and multi-factor authentication", "passwords-mfa", 0, "Build safer sign-in habits and learn how to respond to an unexpected approval request."),
            ("Handle workplace information carefully", "information-handling", 0, "Choose a careful approach to sharing, checking recipients, and reporting a mistake."),
            ("Keep your work device secure", "device-security", 0, "Review everyday habits for locking, updating, and protecting a work device."),
            ("Find the right application", "application-access", 1, "Use a sample service directory journey to identify the right system and access path."),
            ("Get started with inventory requests", "inventory-requests", 1, "Practice planning a clear inventory request and checking its details before submission."),
            ("Work confidently with shared files", "shared-files", 1, "Organize sample files, check permissions, and avoid sharing more than intended."),
            ("Prepare for a system change", "system-changes", 1, "Use a short checklist to prepare, verify the result, and record follow-up questions."),
            ("Troubleshoot before you report", "first-troubleshooting", 2, "Collect simple observations and try approved first steps without changing system settings."),
            ("Describe an access issue", "access-issues", 2, "Separate sign-in problems from application permissions and provide useful context."),
            ("Follow up on a support request", "ticket-follow-up", 2, "Write a useful update with timing, impact, and what has changed since the last report."),
            ("Capture a useful screenshot", "support-screenshots", 2, "Show the relevant error while keeping passwords and unrelated information out of view.")
        ];
        return topics.Select((topic, index) =>
        {
            var category = Categories[topic.Category];
            var course = Document(topic.Title, topic.Category);
            course.Validate();
            return new PortalContentItem
            {
                ContentId = -3001 - index, ContentType = "Course", Slug = SlugPrefix + topic.Slug,
                Title = topic.Title, Summary = topic.Summary, CategoryName = category.Name, CategorySlug = category.Slug,
                Body = LearningCourseDocument.Prefix + JsonSerializer.Serialize(course, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                ThumbnailUrl = "/media/learning/demo/" + (topic.Category == 0 ? "email-example.svg" : topic.Category == 1 ? "access-journey.svg" : "support-checklist.svg"),
                Status = "Published", IsFeatured = index < 3, SortOrder = index, UpdatedAtUtc = SampleDate.AddDays(-index), CreatedAtUtc = SampleDate.AddDays(-index), AuthorName = "Demo learning team"
            };
        }).ToList();
    }

    private static LearningCourseDocument Document(string title, int category)
    {
        var security = category == 0;
        var technical = category == 1;
        var scenario = security ? "An unexpected message asks you to approve a sign-in. What is the safest next step?"
            : technical ? "You cannot open an application. What should you check first?" : "Which detail helps IT investigate your issue?";
        var options = security ? new List<string> { "Approve it immediately", "Verify the request using an official channel", "Share your password with the sender" }
            : technical ? ["Use any sign-in link found online", "Check the official service directory and your access instructions", "Ask a colleague to share their account"]
            : new List<string> { "Only write that it does not work", "Include the system name, exact message, timing, and impact", "Include your password in the description" };
        var steps = security ? "Pause before acting on an unexpected request.\nCheck the sender and the destination carefully.\nVerify through a contact or channel you already trust.\nReport concerns using your approved IT support process."
            : technical ? "Find the application in the official service directory.\nRead the access instructions before opening it.\nUse your own authorized account.\nIf access is missing, record the exact message and request help."
            : "Name the affected system or device.\nRecord when the issue started and the exact message.\nExplain the task that is blocked and its impact.\nAttach a relevant screenshot with sensitive details removed.";
        return new LearningCourseDocument
        {
            Audience = "All employees · demo preview", Level = "Foundation",
            Objectives = ["Recognize the important details in a sample workplace scenario.", "Choose an appropriate next step and explain your decision.", "Use a short checklist to put the lesson into practice."],
            Modules =
            [
                new() { Title = "Explore the topic", Lessons =
                [
                    new() { Id = "introduction", Title = "Start with the essentials", Minutes = 4, Blocks =
                    [
                        new() { Type = "text", Title = title, Text = "Welcome to this **sample learning course**. Work through the modules, compare an example, and try the practice question.\n\nThe examples demonstrate the new learning experience. Follow your current company procedures for real work." },
                        new() { Type = "video", Title = "Try the lesson player", Url = "/media/learning/demo/lesson-preview.webm", Text = "This short, silent clip is a demo player preview. It is not a training recording.\n\nIn a real lesson, this space can contain a system demonstration, captions, and a transcript." },
                        new() { Type = "callout", Title = "A small habit makes a difference", Text = security ? "Take a moment to verify an unexpected request before you respond." : technical ? "Use the official instructions and your own authorized account." : "Clear observations help the support team understand what is happening." }
                    ] },
                    new() { Id = "worked-example", Title = "Look at a worked example", Minutes = 5, Blocks =
                    [
                        new() { Type = "image", Title = security ? "Demo email showing an urgent request, a suspicious sender, and an unfamiliar link." : technical ? "Sample access journey from the service directory through sign-in to the application." : "Sample support checklist with the system, message, timing, and impact.", Url = "/media/learning/demo/" + (security ? "email-example.svg" : technical ? "access-journey.svg" : "support-checklist.svg"), Text = "Illustrative demo example. No real employee information is shown." },
                        new() { Type = "comparison", Title = "Compare the approach", Text = "## A useful approach\n" + (security ? "Verify the request through an official contact before taking action." : technical ? "Read the service directory instructions and identify which account is required." : "Describe the system, exact message, timing, and work that is affected."), Extra = "## An approach to avoid\n" + (security ? "Act immediately because the message sounds urgent." : technical ? "Try an unfamiliar link or borrow another person’s sign-in details." : "Send a vague description or include passwords and unrelated personal details.") },
                        new() { Type = "accordion", Title = "What if I am unsure?", Text = "Use your approved IT support channel. Explain what you observed and ask for guidance before making changes you do not understand." }
                    ] }
                ] },
                new() { Title = "Practice and apply", Lessons =
                [
                    new() { Id = "practice", Title = "Choose your next step", Minutes = 4, Blocks =
                    [
                        new() { Type = "steps", Title = "Your practical checklist", Text = steps },
                        new() { Type = "quiz", Title = scenario, Options = options, Answer = 1, Text = "Use trusted channels, provide relevant information, and keep account credentials private. This is a practice question, not an official assessment." },
                        new() { Type = "download", Title = "Download the demo learning checklist", Text = "A sample reference you can open or save while exploring this course.", Url = "/media/learning/demo/learning-checklist.txt" }
                    ] }
                ] }
            ]
        };
    }
}
