using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Yakult.SystemsPortal.Models;
using Yakult.SystemsPortal.Repositories;
using Yakult.SystemsPortal.Services;

namespace Yakult.SystemsPortal.Controllers;

[AllowAnonymous]
public sealed class PublicController : Controller
{
    private static readonly string[] RetiredCategories = ["news-advisories", "events"];
    private static readonly string[] ResourceCategories = ["employee-resources", "policies-faqs"];
    private static readonly string[] LearningCategories = ["learning", "cybersecurity"];
    private static readonly string[] TechnicalLearningCategories = ["learning", "it-services"];
    private static readonly string[] SupportLearningCategories = ["it-help", "it-help-center"];
    private static readonly string[] AllLearningCategories = ["learning", "cybersecurity", "it-services", "it-help", "it-help-center"];
    private static readonly string[] HelpCategories = ["it-help", "it-help-center", "it-services"];
    private static readonly IReadOnlyList<ContentTrackOption> LearningTracks =
    [
        new() { Value = "cybersecurity", Label = "IT Cybersecurity", Description = "Phishing, passwords, MFA, data handling, and safe device habits.", CategorySlugs = ["cybersecurity"] },
        new() { Value = "technical", Label = "IT Technical", Description = "Technical guides, system learning, access basics, and IT service references.", CategorySlugs = TechnicalLearningCategories },
        new() { Value = "support", Label = "IT Support", Description = "Help desk guidance, troubleshooting, account help, and support processes.", CategorySlugs = SupportLearningCategories }
    ];
    private readonly IPortalContentRepository _content;
    private readonly IPortalCardRepository _cards;
    private readonly IEmployeeResourceCatalog _employeeResources;
    private readonly IEmployeeResourceRepository _employeeResourceRepository;
    private readonly IDemoModeService _demoMode;
    private readonly IWebHostEnvironment _environment;
    private readonly PortalOptions _options;
    private readonly ILogger<PublicController> _logger;

    public PublicController(IPortalContentRepository content, IPortalCardRepository cards, IEmployeeResourceCatalog employeeResources, IEmployeeResourceRepository employeeResourceRepository, IDemoModeService demoMode, IOptions<PortalOptions> options, ILogger<PublicController> logger, IWebHostEnvironment environment)
    { _content = content; _cards = cards; _employeeResources = employeeResources; _employeeResourceRepository = employeeResourceRepository; _demoMode = demoMode; _options = options.Value; _logger = logger; _environment = environment; }

    public async Task<IActionResult> Index()
    {
        IReadOnlyList<PortalContentItem> all = Array.Empty<PortalContentItem>();
        IReadOnlyList<PortalSystemLink> services = _options.Systems;
        var notice = new PortalNoticeSettings();
        try
        {
            all = ExcludeRetiredContent(await _content.GetPublishedAsync(take: 100)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Public homepage loaded without database content.");
        }
        try
        {
            services = await _cards.GetVisibleSystemCardsAsync();
            notice = await _cards.GetNoticeSettingsAsync();
        }
        catch (Exception ex) { _logger.LogWarning(ex, "Public homepage loaded with configured system cards."); }

        var resourceContent = IncludeCodeOnlyEmployeeResources(all, _demoMode.IsEnabled(HttpContext));
        var (employeeResources, employeeResourcesAreDemo) = await LoadPublishedEmployeeResourcesAsync(null, null, null);
        var learning = InCategories(all, LearningCategories).ToList();

        return View(new PublicHomeViewModel
        {
            Advisories = all.Where(x => x.ContentType is "Advisory" or "Announcement").Take(4).ToList(),
            Featured = all.Where(x => x.IsFeatured).Take(6).ToList(),
            Latest = all.Take(8).ToList(),
            Learning = learning.OrderByDescending(x => x.ContentType == "Video").ThenBy(x => x.SortOrder).Take(4).ToList(),
            Resources = InCategories(resourceContent, ResourceCategories).Take(4).ToList(),
            EmployeeResources = employeeResources.Take(4).ToList(),
            EmployeeResourcesAreDemo = employeeResourcesAreDemo,
            Services = ApplyPublicCardRules(services).Take(8).ToList(),
            Sections = GetSections(),
            Notice = notice
        });
    }

    public async Task<IActionResult> Services()
    {
        IReadOnlyList<PortalSystemLink> systems;
        try { systems = await _cards.GetVisibleSystemCardsAsync(); } catch { systems = _options.Systems; }
        IReadOnlyList<PortalContentItem> guides = Array.Empty<PortalContentItem>();
        try { guides = await _content.GetPublishedAsync(categorySlug: "it-services", take: 12); }
        catch (Exception ex) { _logger.LogWarning(ex, "Services directory loaded without published guides."); }
        var cards = ApplyPublicCardRules(systems).Select(BuildServiceCard).ToList();
        return View(new ServicesDirectoryViewModel
        {
            ActiveSystems = cards.Where(x => !x.IsUpcoming).ToList(),
            UpcomingSystems = cards.Where(x => x.IsUpcoming).ToList(),
            Guides = guides
        });
    }

    public Task<IActionResult> Company(string? type = null) => ContentSection("Company", "About Yakult", "Company information, leadership messages, and organizational updates for employees.", ["company", "company-information"], type);
    public Task<IActionResult> Resources(string? type = null) => ContentSection("Employee Resources", "IT workplace references", "IT policies, access forms, cybersecurity FAQs, and technology references.", ResourceCategories, type, includeCodeOnlyEmployeeResources: true);

    [HttpGet("/EmployeeResources", Name = "EmployeeResourcesIndex")]
    public async Task<IActionResult> EmployeeResources(string? q = null, string? category = null, string? type = null)
    {
        var query = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        var selectedCategory = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        var selectedType = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
        var (items, isDemoData) = await LoadPublishedEmployeeResourcesAsync(query, selectedCategory, selectedType);
        return View("EmployeeResources", new EmployeeResourcesViewModel
        {
            Query = query,
            SelectedCategory = selectedCategory,
            SelectedType = selectedType,
            IsDemoData = isDemoData,
            Categories = items.Select(item => item.Category).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            ResourceTypes = items.Select(item => item.ResourceType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(value => value).ToList(),
            Items = items
        });
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public IActionResult SetDemoMode(bool enabled, string? returnUrl = null)
    {
        if (!_demoMode.CanUse(HttpContext)) return Forbid();

        _demoMode.SetEnabled(HttpContext, enabled);
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!);
    }

    [HttpGet("/EmployeeResources/{slug}", Name = "EmployeeResourceDetails")]
    public async Task<IActionResult> EmployeeResourceDetails(string slug)
    {
        var demoModeEnabled = _demoMode.IsEnabled(HttpContext);
        EmployeeResourceItem? item = demoModeEnabled
            ? _employeeResources.FindPublishedBySlug(slug)
            : null;

        if (item is null)
        {
            try
            {
                item = await _employeeResourceRepository.GetPublishedBySlugAsync(slug);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Managed Employee Resource lookup failed for {Slug}.", slug);
            }
        }

        return item is null
            ? NotFound()
            : View("EmployeeResourceDetails", new EmployeeResourceDetailsViewModel { Item = item });
    }

    [HttpGet("/EmployeeResources/attachments/{attachmentId:int}")]
    public async Task<IActionResult> EmployeeResourceAttachment(int attachmentId)
    {
        EmployeeResourceDownload? download;
        try
        {
            download = await _employeeResourceRepository.GetPublishedDownloadAsync(attachmentId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Employee Resource attachment lookup failed for {AttachmentId}.", attachmentId);
            return NotFound();
        }
        if (download is null) return NotFound();

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot)) webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        if (!download.StoredPath.StartsWith("/media/employee-resources/", StringComparison.OrdinalIgnoreCase)
            || download.StoredPath.Contains("..", StringComparison.Ordinal)
            || download.StoredPath.Contains('\\')) return NotFound();
        var root = Path.GetFullPath(webRoot);
        var relative = download.StoredPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.GetFullPath(Path.Combine(root, relative));
        if (!physicalPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !System.IO.File.Exists(physicalPath)) return NotFound();
        return PhysicalFile(physicalPath, download.MimeType, download.FileName, enableRangeProcessing: true);
    }

    public Task<IActionResult> Learning(string? type = null, string? track = null) => LearningSection(type, track);
    public Task<IActionResult> Help(string? type = null) => ContentSection("IT Help Center", "Get assistance", "Support guides, account help, troubleshooting information, and service contacts.", HelpCategories, type);

    public IActionResult Cybersecurity(string? type = null) => RedirectToAction(nameof(Learning), new { type, track = "cybersecurity" });
    public IActionResult Faqs() => RedirectToAction(nameof(Resources), new { type = "FAQ" });

    [Route("content/{slug}")]
    public async Task<IActionResult> ContentDetails(string slug)
    {
        var demoModeEnabled = _demoMode.IsEnabled(HttpContext);
        PortalContentItem? item = demoModeEnabled
            ? PublicEmployeeResourceSamples.Items.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
            : null;

        if (item is null)
        {
            try { item = await _content.GetPublishedBySlugAsync(slug); }
            catch (Exception ex) { _logger.LogWarning(ex, "Published content lookup failed for {Slug}.", slug); }
        }

        if (item == null || IsRetiredContent(item)) return NotFound();
        if (item.ContentId > 0)
        {
            try { await _content.TrackAsync("View", "Content", item.ContentId.ToString()); }
            catch (Exception ex) { _logger.LogDebug(ex, "Content view metric was not recorded."); }
        }

        PortalCourseProgress? progress = null;
        var isCourse = item.ContentType.Equals("Course", StringComparison.OrdinalIgnoreCase);
        var isTrackable = isCourse || item.ContentType.Equals("Video", StringComparison.OrdinalIgnoreCase);
        if (isTrackable)
        {
            if (isCourse && User.Identity?.IsAuthenticated != true)
                return RedirectToAction("Login", "Home", new { returnUrl = Url.Action(nameof(ContentDetails), "Public", new { slug }) });
            if (User.Identity?.IsAuthenticated == true && TryGetUserId(out var userId))
            {
                try { progress = await _content.GetCourseProgressAsync(userId, item.ContentId); }
                catch (Exception ex) { _logger.LogWarning(ex, "Course progress lookup failed for {ContentId}.", item.ContentId); }
            }
        }

        IReadOnlyList<PortalContentItem> related = Array.Empty<PortalContentItem>();
        if (isTrackable)
        {
            try
            {
                related = ExcludeRetiredContent(await _content.GetPublishedAsync(categorySlug: item.CategorySlug, take: 6))
                    .Where(x => x.ContentId != item.ContentId)
                    .Take(3)
                    .ToList();
            }
            catch (Exception ex) { _logger.LogWarning(ex, "Related learning could not be loaded for {ContentId}.", item.ContentId); }
        }
        var relatedProgress = await LoadCourseProgressAsync(related);

        return View("Content", new PortalContentDetailsViewModel { Item = item, Media = ResolveMedia(item.MediaUrl), Progress = progress, RelatedItems = related, RelatedProgress = relatedProgress });
    }

    public async Task<IActionResult> Search(string? q, string? type = null)
    {
        var query = (q ?? "").Trim();
        IReadOnlyList<PortalContentItem> items = Array.Empty<PortalContentItem>();
        if (query.Length >= 2)
        {
            var results = new List<PortalContentItem>();
            try { results.AddRange(ExcludeRetiredContent(await _content.GetPublishedAsync(query: query, take: 50))); }
            catch (Exception ex) { _logger.LogWarning(ex, "Public search loaded without database content."); }
            if (_demoMode.IsEnabled(HttpContext))
            {
                results.AddRange(PublicEmployeeResourceSamples.Items.Where(item =>
                    $"{item.Title} {item.Summary} {item.Body} {item.CategoryName} {item.ContentType}"
                        .Contains(query, StringComparison.OrdinalIgnoreCase)));
            }
            items = results.GroupBy(x => x.Slug, StringComparer.OrdinalIgnoreCase).Select(x => x.First()).ToList();
        }
        IReadOnlyList<PortalSystemLink> services = Array.Empty<PortalSystemLink>();
        if (query.Length >= 2)
        {
            try { services = (await _cards.GetVisibleSystemCardsAsync()).Where(x => ($"{x.Name} {x.Description}").Contains(query, StringComparison.OrdinalIgnoreCase)).ToList(); }
            catch { services = _options.Systems.Where(x => ($"{x.Name} {x.Description}").Contains(query, StringComparison.OrdinalIgnoreCase)).ToList(); }
        }

        var visibleServices = ApplyPublicCardRules(services).ToList();
        var availableTypes = items.Select(x => x.ContentType).ToList();
        if (visibleServices.Count > 0) availableTypes.Add("Services");
        availableTypes = availableTypes.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (string.Equals(type, "Services", StringComparison.OrdinalIgnoreCase))
            {
                items = Array.Empty<PortalContentItem>();
            }
            else
            {
                items = items.Where(x => x.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
                visibleServices.Clear();
            }
        }

        return View("ContentList", new ContentListViewModel
        {
            Title="Search the employee portal",
            Eyebrow="Portal search",
            Description="Find employee information, services, advisories, guides, and learning resources.",
            Query=query,
            SelectedType=type,
            AvailableTypes=availableTypes,
            Items=items,
            ProgressByContentId=await LoadCourseProgressAsync(items),
            Services=visibleServices
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Track([FromBody] TrackPortalMetricRequest request)
    { await _content.TrackAsync(request.EventType, request.TargetType, request.TargetKey); return NoContent(); }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Progress([FromBody] SaveCourseProgressRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized();
        if (request == null || request.ContentId <= 0)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return Json(new { success = false, message = "Content not found." });
        }
        try
        {
            var progress = await _content.SaveCourseProgressAsync(userId, request.ContentId, request.PositionSec, request.DurationSec);
            return Json(new { success = true, isCompleted = progress.IsCompleted, percent = progress.ProgressPercent, positionSec = progress.LastPositionSec });
        }
        catch (ArgumentException ex)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return Json(new { success = false, message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return Json(new { success = false, message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Course progress could not be saved for content {ContentId}.", request.ContentId);
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            return Json(new { success = false, message = "Unable to save course progress." });
        }
    }

    [Authorize]
    public async Task<IActionResult> MyLearning()
    {
        var viewModel = new MyLearningViewModel();
        if (!TryGetUserId(out var userId))
            return View(viewModel);
        try
        {
            var items = await _content.GetUserCourseProgressAsync(userId);
            viewModel = new MyLearningViewModel
            {
                InProgress = items.Where(x => !x.IsCompleted).ToList(),
                Completed = items.Where(x => x.IsCompleted).ToList()
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Course progress reporting is not configured.");
        }
        return View(viewModel);
    }

    private async Task<IActionResult> LearningSection(string? type = null, string? track = null)
    {
        IReadOnlyList<PortalContentItem> all = Array.Empty<PortalContentItem>();
        try { all = await _content.GetPublishedAsync(take: 100); }
        catch (Exception ex) { _logger.LogWarning(ex, "Public learning section loaded without database content."); }

        var selectedTrack = ResolveLearningTrack(track);
        var categorySlugs = selectedTrack?.CategorySlugs ?? AllLearningCategories;
        var items = InCategories(ExcludeRetiredContent(all), categorySlugs).ToList();

        var availableTypes = items.Select(x => x.ContentType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        if (!string.IsNullOrWhiteSpace(type)) items = items.Where(x => x.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();

        return View("Learning", new ContentListViewModel
        {
            Title = "Learning & Awareness",
            Eyebrow = "Build knowledge",
            Description = "Choose an IT learning focus, then browse cybersecurity videos, technical references, and support guidance.",
            SelectedType = type,
            SelectedTrack = selectedTrack?.Value,
            Tracks = LearningTracks,
            AvailableTypes = availableTypes,
            Items = items.OrderByDescending(x => x.ContentType is "Video" or "Course").ThenBy(x => x.SortOrder).ThenByDescending(x => x.UpdatedAtUtc).ToList(),
            ProgressByContentId = await LoadCourseProgressAsync(items)
        });
    }

    private async Task<IActionResult> ContentSection(string title, string eyebrow, string description, IReadOnlyCollection<string> categorySlugs, string? type = null, bool includeCodeOnlyEmployeeResources = false)
    {
        IReadOnlyList<PortalContentItem> all = Array.Empty<PortalContentItem>();
        try { all = await _content.GetPublishedAsync(take: 100); }
        catch (Exception ex) { _logger.LogWarning(ex, "Public section {Section} loaded without database content.", title); }
        var content = includeCodeOnlyEmployeeResources
            ? IncludeCodeOnlyEmployeeResources(all, _demoMode.IsEnabled(HttpContext))
            : all;
        var items = InCategories(ExcludeRetiredContent(content), categorySlugs).ToList();
        var availableTypes = items.Select(x => x.ContentType).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        if (!string.IsNullOrWhiteSpace(type)) items = items.Where(x => x.ContentType.Equals(type, StringComparison.OrdinalIgnoreCase)).ToList();
        return View("ContentList", new ContentListViewModel
        {
            Title = title,
            Eyebrow = eyebrow,
            Description = description,
            SelectedType = type,
            AvailableTypes = availableTypes,
            Items = items.ToList(),
            ProgressByContentId = await LoadCourseProgressAsync(items)
        });
    }

    private async Task<(IReadOnlyList<EmployeeResourceItem> Items, bool IsDemoData)> LoadPublishedEmployeeResourcesAsync(string? query, string? category, string? type)
    {
        if (_demoMode.IsEnabled(HttpContext))
            return (FilterDemoEmployeeResources(query, category, type), true);

        try
        {
            var managed = await _employeeResourceRepository.GetPublishedAsync(take: 100);
            if (managed.Count == 0)
                return (Array.Empty<EmployeeResourceItem>(), false);

            if (string.IsNullOrWhiteSpace(query) && string.IsNullOrWhiteSpace(category) && string.IsNullOrWhiteSpace(type))
                return (managed, false);

            return (await _employeeResourceRepository.GetPublishedAsync(type, category, query, 100), false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Managed Employee Resources are unavailable; demo mode is disabled, so no sample resources will be shown.");
            return (Array.Empty<EmployeeResourceItem>(), false);
        }
    }

    private IReadOnlyList<EmployeeResourceItem> FilterDemoEmployeeResources(string? query, string? category, string? type)
    {
        var items = _employeeResources.GetPublished().AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query)) items = items.Where(item => item.SearchText.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(category)) items = items.Where(item => item.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(type)) items = items.Where(item => item.ResourceType.Equals(type, StringComparison.OrdinalIgnoreCase));
        return items.ToList();
    }

    private async Task<IReadOnlyDictionary<int, PortalCourseProgress>> LoadCourseProgressAsync(IReadOnlyCollection<PortalContentItem> items)
    {
        if (User.Identity?.IsAuthenticated != true || !TryGetUserId(out var userId)) return new Dictionary<int, PortalCourseProgress>();
        var courseIds = items.Where(x => x.ContentType.Equals("Course", StringComparison.OrdinalIgnoreCase) || x.ContentType.Equals("Video", StringComparison.OrdinalIgnoreCase)).Select(x => x.ContentId).Distinct().ToList();
        if (courseIds.Count == 0) return new Dictionary<int, PortalCourseProgress>();
        try { return await _content.GetCourseProgressBatchAsync(userId, courseIds); }
        catch (Exception ex) { _logger.LogWarning(ex, "Course progress batch could not be loaded."); return new Dictionary<int, PortalCourseProgress>(); }
    }

    private bool TryGetUserId(out int userId)
    {
        userId = 0;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0;
    }

    private static IReadOnlyList<PortalContentItem> IncludeCodeOnlyEmployeeResources(IEnumerable<PortalContentItem> items, bool includeSamples)
    {
        var published = items.ToList();
        if (!includeSamples) return published;

        var existingSlugs = published
            .Select(x => x.Slug)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return PublicEmployeeResourceSamples.Items
            .Where(sample => !existingSlugs.Contains(sample.Slug))
            .Concat(published)
            .ToList();
    }

    private static IEnumerable<PortalContentItem> InCategories(IEnumerable<PortalContentItem> items, IReadOnlyCollection<string> categorySlugs) =>
        items.Where(x => categorySlugs.Contains(x.CategorySlug, StringComparer.OrdinalIgnoreCase));

    private static ContentTrackOption? ResolveLearningTrack(string? track) =>
        string.IsNullOrWhiteSpace(track)
            ? null
            : LearningTracks.FirstOrDefault(x => x.Value.Equals(track, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyList<PortalSectionLink> GetSections() =>
    [
        new() { Label="01", Name="Company", Description="Learn about Yakult, its organization, and company-wide information.", Action=nameof(Company) },
        new() { Label="02", Name="Employee Resources", Description="Find IT policies, access forms, cybersecurity FAQs, and technology references.", Action=nameof(EmployeeResources) },
        new() { Label="03", Name="Learning & Awareness", Description="Watch cybersecurity videos and access employee learning resources.", Action=nameof(Learning) },
        new() { Label="04", Name="Services & Portals", Description="Open Yakult business systems and employee service portals.", Action=nameof(Services) },
        new() { Label="05", Name="IT Help Center", Description="Get support, account guidance, and troubleshooting information.", Action=nameof(Help) }
    ];

    private IReadOnlyList<PortalSystemLink> ApplyPublicCardRules(IEnumerable<PortalSystemLink> cards)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var roles = User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nowUtc = DateTime.UtcNow;

        return cards
            .Where(card => card.IsVisible)
            .Where(card => CanCurrentUserSee(card, userId, roles))
            .Select(card => ApplyMaintenanceWindow(card, nowUtc))
            .ToList();
    }

    private static bool CanCurrentUserSee(PortalSystemLink card, string userId, HashSet<string> roles)
    {
        var allowedUsers = SplitCsv(card.AllowedUserIds);
        if (allowedUsers.Count > 0 && allowedUsers.Contains(userId, StringComparer.OrdinalIgnoreCase))
            return true;

        var allowedRoles = SplitCsv(card.AllowedRoles);
        if (allowedRoles.Count == 0 && allowedUsers.Count == 0)
            return true;

        return allowedRoles.Any(role => roles.Contains(role));
    }

    private static PortalSystemLink ApplyMaintenanceWindow(PortalSystemLink card, DateTime nowUtc)
    {
        if (card.MaintenanceStartUtc.HasValue &&
            card.MaintenanceEndUtc.HasValue &&
            nowUtc >= DateTime.SpecifyKind(card.MaintenanceStartUtc.Value, DateTimeKind.Utc) &&
            nowUtc <= DateTime.SpecifyKind(card.MaintenanceEndUtc.Value, DateTimeKind.Utc))
        {
            card.Status = "Maintenance";
            if (string.IsNullOrWhiteSpace(card.MaintenanceNote))
                card.MaintenanceNote = $"Scheduled maintenance until {card.MaintenanceEndUtc.Value:yyyy-MM-dd HH:mm} UTC.";
            card.IsOperational = false;
        }

        return card;
    }

    private static List<string> SplitCsv(string value) =>
        (value ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .ToList();

    private static IEnumerable<PortalContentItem> ExcludeRetiredContent(IEnumerable<PortalContentItem> items) =>
        items.Where(x => !IsRetiredContent(x));

    private static bool IsRetiredContent(PortalContentItem item) =>
        RetiredCategories.Contains(item.CategorySlug, StringComparer.OrdinalIgnoreCase);

    private static PortalMediaViewModel ResolveMedia(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return new PortalMediaViewModel();
        if (IsSafeMediaPath(value))
        {
            if (value.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || value.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
                return new PortalMediaViewModel { Kind="HostedVideo", SourceUrl=value };
            return new PortalMediaViewModel { Kind="External", SourceUrl=value };
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return new PortalMediaViewModel();

        var host = uri.Host.ToLowerInvariant();
        string? videoId = null;
        if (host is "youtu.be") videoId = uri.AbsolutePath.Trim('/').Split('/')[0];
        else if (host is "youtube.com" or "youtube-nocookie.com" || host.EndsWith(".youtube.com", StringComparison.Ordinal) || host.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal))
        {
            if (uri.AbsolutePath.StartsWith("/embed/", StringComparison.OrdinalIgnoreCase)) videoId = uri.AbsolutePath[7..].Split('/')[0];
            else videoId = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).TryGetValue("v", out var id) ? id.ToString() : null;
        }
        if (!string.IsNullOrWhiteSpace(videoId) && videoId.All(c => char.IsLetterOrDigit(c) || c is '-' or '_'))
            return new PortalMediaViewModel { Kind="YouTube", SourceUrl=value, EmbedUrl=$"https://www.youtube-nocookie.com/embed/{videoId}" };

        if (uri.AbsolutePath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) || uri.AbsolutePath.EndsWith(".webm", StringComparison.OrdinalIgnoreCase))
            return new PortalMediaViewModel { Kind="HostedVideo", SourceUrl=value };

        return new PortalMediaViewModel { Kind="External", SourceUrl=value };
    }

    private static bool IsSafeMediaPath(string value) =>
        value.StartsWith("/media/", StringComparison.OrdinalIgnoreCase)
        && !value.Contains("..", StringComparison.Ordinal)
        && !value.Contains('\\')
        && Uri.TryCreate(value, UriKind.Relative, out _);

    private PortalServiceCardViewModel BuildServiceCard(PortalSystemLink system)
    {
        var status = string.IsNullOrWhiteSpace(system.Status)
            ? system.IsOperational ? "Connected" : "NotConnected"
            : system.Status;
        var hasWebUrl = !string.IsNullOrWhiteSpace(system.Url) && system.Url != "#";
        var isConfigured = hasWebUrl || system.IsRdpDownload || system.IsInstallerDownload;
        var isMaintenance = status.Equals("Maintenance", StringComparison.OrdinalIgnoreCase);
        var isDisabled = status.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
        var isUpcoming = !isConfigured || status.Equals("NotConnected", StringComparison.OrdinalIgnoreCase) || isDisabled;
        var actions = new List<PortalServiceActionViewModel>();

        if (!isUpcoming)
        {
            if (isMaintenance)
            {
                actions.Add(new PortalServiceActionViewModel { Label="Temporarily unavailable", Kind="Unavailable", IsEnabled=false });
            }
            else
            {
                if (system.IsRdpDownload)
                    actions.Add(new PortalServiceActionViewModel { Label="Launch via Remote Desktop", Url=Url.Action("DownloadRdp", "Home"), Kind="Rdp", IsEnabled=true });
                if (system.IsInstallerDownload)
                {
                    var hasInstallerPath = !string.IsNullOrWhiteSpace(system.InstallerPath);
                    actions.Add(new PortalServiceActionViewModel { Label=hasInstallerPath ? "Download installer" : "Installer unavailable", Url=hasInstallerPath ? Url.Action("DownloadInstaller", "Home", new { cardKey = system.CardKey }) : null, Kind="Installer", IsEnabled=hasInstallerPath });
                }
                if (!system.IsRdpDownload && !system.IsInstallerDownload && hasWebUrl)
                    actions.Add(new PortalServiceActionViewModel { Label="Open system", Url=system.Url, Kind="Web", IsEnabled=true, OpensNewWindow=true });
            }
        }

        return new PortalServiceCardViewModel
        {
            System = system,
            StatusLabel = isDisabled ? "Planned" : isUpcoming ? "Not yet connected" : isMaintenance ? "Under maintenance" : "Operational",
            StatusKind = isUpcoming ? "upcoming" : isMaintenance ? "maintenance" : "operational",
            IsUpcoming = isUpcoming,
            Actions = actions
        };
    }
}
