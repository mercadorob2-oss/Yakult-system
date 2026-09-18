using System.ComponentModel.DataAnnotations;

namespace Yakult.SystemsPortal.Models;

public sealed class PublicHomeViewModel
{
    public IReadOnlyList<PortalContentItem> Advisories { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalContentItem> Featured { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalContentItem> Latest { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalSystemLink> Services { get; init; } = Array.Empty<PortalSystemLink>();
    public IReadOnlyList<PortalContentItem> Learning { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalContentItem> Resources { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<EmployeeResourceItem> EmployeeResources { get; init; } = Array.Empty<EmployeeResourceItem>();
    public bool EmployeeResourcesAreDemo { get; init; } = true;
    public IReadOnlyList<PortalSectionLink> Sections { get; init; } = Array.Empty<PortalSectionLink>();
    public PortalNoticeSettings Notice { get; init; } = new();
}

public sealed class ContentListViewModel
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public string? Query { get; init; }
    public string? Eyebrow { get; init; }
    public string? SelectedType { get; init; }
    public string? SelectedTrack { get; init; }
    public IReadOnlyList<ContentTrackOption> Tracks { get; init; } = Array.Empty<ContentTrackOption>();
    public IReadOnlyList<string> AvailableTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<PortalContentItem> Items { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalSystemLink> Services { get; init; } = Array.Empty<PortalSystemLink>();
    public IReadOnlyDictionary<int, PortalCourseProgress> ProgressByContentId { get; init; } = new Dictionary<int, PortalCourseProgress>();
}

public sealed class ContentTrackOption
{
    public required string Value { get; init; }
    public required string Label { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> CategorySlugs { get; init; } = Array.Empty<string>();
}

public sealed class ServicesDirectoryViewModel
{
    public IReadOnlyList<PortalServiceCardViewModel> ActiveSystems { get; init; } = Array.Empty<PortalServiceCardViewModel>();
    public IReadOnlyList<PortalServiceCardViewModel> UpcomingSystems { get; init; } = Array.Empty<PortalServiceCardViewModel>();
    public IReadOnlyList<PortalContentItem> Guides { get; init; } = Array.Empty<PortalContentItem>();
}

public sealed class PortalServiceCardViewModel
{
    public required PortalSystemLink System { get; init; }
    public required string StatusLabel { get; init; }
    public required string StatusKind { get; init; }
    public bool IsUpcoming { get; init; }
    public IReadOnlyList<PortalServiceActionViewModel> Actions { get; init; } = Array.Empty<PortalServiceActionViewModel>();
}

public sealed class PortalServiceActionViewModel
{
    public required string Label { get; init; }
    public string? Url { get; init; }
    public required string Kind { get; init; }
    public bool IsEnabled { get; init; }
    public bool OpensNewWindow { get; init; }
}

public sealed class PortalContentDetailsViewModel
{
    public required PortalContentItem Item { get; init; }
    public PortalMediaViewModel Media { get; init; } = new();
    public PortalCourseProgress? Progress { get; init; }
    public IReadOnlyList<PortalContentItem> RelatedItems { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyDictionary<int, PortalCourseProgress> RelatedProgress { get; init; } = new Dictionary<int, PortalCourseProgress>();
}

public sealed class PortalCourseProgress
{
    public int ContentId { get; init; }
    public int ProgressPercent { get; init; }
    public int LastPositionSec { get; init; }
    public int DurationSec { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
}

public sealed class SaveCourseProgressRequest
{
    public int ContentId { get; init; }
    public int PositionSec { get; init; }
    public int DurationSec { get; init; }
}

public sealed class CourseCompletionStatItem
{
    public int UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CompletedAtUtc { get; init; }
}

public sealed class MyLearningItem
{
    public int ContentId { get; init; }
    public string ContentType { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? MediaUrl { get; init; }
    public int ProgressPercent { get; init; }
    public int LastPositionSec { get; init; }
    public int DurationSec { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime? CompletedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class MyLearningViewModel
{
    public IReadOnlyList<MyLearningItem> InProgress { get; init; } = Array.Empty<MyLearningItem>();
    public IReadOnlyList<MyLearningItem> Completed { get; init; } = Array.Empty<MyLearningItem>();
}

public sealed class CourseCompletionStats
{
    public int StartedCount { get; init; }
    public int CompletedCount { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
    public int TotalPages { get; init; }
    public IReadOnlyList<CourseCompletionStatItem> Completed { get; init; } = Array.Empty<CourseCompletionStatItem>();
}

public sealed class PortalMediaViewModel
{
    public string Kind { get; init; } = "None";
    public string? SourceUrl { get; init; }
    public string? EmbedUrl { get; init; }
}

public sealed class PortalSectionLink
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Action { get; init; }
    public required string Label { get; init; }
}

public sealed class ContentEditorViewModel
{
    public IReadOnlyList<PortalContentItem> Items { get; init; } = Array.Empty<PortalContentItem>();
    public IReadOnlyList<PortalContentCategory> Categories { get; init; } = Array.Empty<PortalContentCategory>();
    public bool CanPublish { get; init; }
}

public sealed class PortalContentItem
{
    public int ContentId { get; init; }
    public bool IsDemo => ContentId < 0;
    public string ContentType { get; init; } = "Article";
    public string Slug { get; init; } = string.Empty;
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string CategorySlug { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? MediaUrl { get; init; }
    public string Status { get; init; } = "Draft";
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
    public DateTime? PublishStartUtc { get; init; }
    public DateTime? PublishEndUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
    public string? AuthorName { get; init; }
    public string? ApproverName { get; init; }
    public IReadOnlyList<PortalContentLink> Links { get; init; } = Array.Empty<PortalContentLink>();
}

public sealed class PortalContentCategory
{
    public int CategoryId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? Description { get; init; }
}

public sealed class PortalContentLink
{
    public int LinkId { get; init; }
    public string Label { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string LinkType { get; init; } = "Related";
}

public sealed class SavePortalContentRequest
{
    public int? ContentId { get; init; }
    [Required, MaxLength(30)] public string ContentType { get; init; } = "Article";
    [Required, MaxLength(180)] public string Title { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string Slug { get; init; } = string.Empty;
    [MaxLength(500)] public string Summary { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public int CategoryId { get; init; }
    [MaxLength(1000)] public string? ThumbnailUrl { get; init; }
    [MaxLength(1000)] public string? MediaUrl { get; init; }
    public bool IsFeatured { get; init; }
    public int SortOrder { get; init; }
    public DateTime? PublishStartUtc { get; init; }
    public DateTime? PublishEndUtc { get; init; }
    public List<SavePortalContentLinkRequest> Links { get; init; } = new();
}

public sealed class SavePortalContentLinkRequest
{
    [Required, MaxLength(120)] public string Label { get; init; } = string.Empty;
    [Required, MaxLength(1000)] public string Url { get; init; } = string.Empty;
    [MaxLength(30)] public string LinkType { get; init; } = "Related";
}

public sealed class ImportUrlRequest
{
    [Required, MaxLength(2000)] public string? Url { get; init; }
}

public sealed class ContentWorkflowRequest
{
    public int ContentId { get; init; }
    [MaxLength(500)] public string? Remarks { get; init; }
    public DateTime? PublishStartUtc { get; init; }
}

public sealed class TrackPortalMetricRequest
{
    [Required, MaxLength(30)] public string EventType { get; init; } = string.Empty;
    [Required, MaxLength(100)] public string TargetType { get; init; } = string.Empty;
    [Required, MaxLength(200)] public string TargetKey { get; init; } = string.Empty;
}
