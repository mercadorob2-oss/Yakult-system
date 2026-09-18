using System.ComponentModel.DataAnnotations;

namespace Yakult.SystemsPortal.Models;

public sealed class EmployeeResourcesViewModel
{
    public string? Query { get; init; }
    public string? SelectedCategory { get; init; }
    public string? SelectedType { get; init; }
    public bool IsDemoData { get; init; } = true;
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> ResourceTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<EmployeeResourceItem> Items { get; init; } = Array.Empty<EmployeeResourceItem>();
}

public sealed class EmployeeResourceDetailsViewModel
{
    public required EmployeeResourceItem Item { get; init; }
}

public sealed class EmployeeResourceItem
{
    public int ResourceId { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public required string Slug { get; init; }
    public required string Title { get; init; }
    public required string Summary { get; init; }
    public required string Overview { get; init; }
    public int? CategoryId { get; init; }
    public required string Category { get; init; }
    public string? CategorySlug { get; init; }
    public required string ResourceType { get; init; }
    public int? OwnerDepartmentId { get; init; }
    public required string OwnerDepartment { get; init; }
    public required string Version { get; init; }
    public required string Status { get; init; }
    public bool IsDemoData { get; init; } = true;
    public DateTime? PublishStartUtc { get; init; }
    public DateTime? PublishEndUtc { get; init; }
    public DateTime LastUpdatedUtc { get; init; }
    public DateTime? ReviewDateUtc { get; init; }
    public DateTime CreatedAtUtc { get; init; }
    public string? AuthorName { get; init; }
    public string? ApproverName { get; init; }
    public IReadOnlyList<string> Highlights { get; init; } = Array.Empty<string>();
    public IReadOnlyList<EmployeeResourceAttachment> Attachments { get; init; } = Array.Empty<EmployeeResourceAttachment>();
    public IReadOnlyList<EmployeeResourceRevision> Revisions { get; init; } = Array.Empty<EmployeeResourceRevision>();
    public string SearchText => string.Join(" ", new[]
    {
        Title,
        Summary,
        Category,
        ResourceType,
        OwnerDepartment,
        string.Join(" ", Attachments.Select(attachment => attachment.FileName))
    });
    public bool IsReviewDue => ReviewDateUtc.HasValue && ReviewDateUtc.Value < DateTime.UtcNow;
}

public sealed class EmployeeResourceAttachment
{
    public int AttachmentId { get; init; }
    public required string FileName { get; init; }
    public string? StoredPath { get; init; }
    public string? DownloadUrl { get; init; }
    public required string Format { get; init; }
    public required string SizeLabel { get; init; }
    public long SizeBytes { get; init; }
    public string? MimeType { get; init; }
    public string? Description { get; init; }
    public DateTime? UploadedAtUtc { get; init; }
    public string? UploadedByName { get; init; }
    public bool IsDemoOnly { get; init; } = true;
}

public sealed class EmployeeResourceRevision
{
    public int RevisionId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Remarks { get; init; }
    public string? SnapshotJson { get; init; }
    public string ChangedByName { get; init; } = string.Empty;
    public DateTime ChangedAtUtc { get; init; }
}

public sealed class EmployeeResourceAdminViewModel
{
    public IReadOnlyList<EmployeeResourceItem> Items { get; init; } = Array.Empty<EmployeeResourceItem>();
    public EmployeeResourceItem? SelectedItem { get; init; }
    public IReadOnlyList<EmployeeResourceOption> Categories { get; init; } = Array.Empty<EmployeeResourceOption>();
    public IReadOnlyList<EmployeeResourceOption> Departments { get; init; } = Array.Empty<EmployeeResourceOption>();
    public bool IsDatabaseAvailable { get; init; }
    public bool IsDemoData { get; init; }
    public string? SetupMessage { get; init; }
    public bool CanPublish { get; init; }
    public int? SelectedId { get; init; }
}

public sealed class EmployeeResourceOption
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Slug { get; init; }
}

public sealed class SaveEmployeeResourceRequest
{
    public int? ResourceId { get; set; }
    public string? RowVersion { get; set; }
    [Required, MaxLength(50)] public string ResourceType { get; set; } = "Guide";
    [Required, MaxLength(180)] public string Title { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Slug { get; set; } = string.Empty;
    [MaxLength(500)] public string Summary { get; set; } = string.Empty;
    public string Overview { get; set; } = string.Empty;
    [Range(1, int.MaxValue)] public int CategoryId { get; set; }
    public int? OwnerDepartmentId { get; set; }
    [Required, MaxLength(20)] public string Version { get; set; } = "v1.0";
    public DateTime? ReviewDateUtc { get; set; }
    public DateTime? PublishStartUtc { get; set; }
    public DateTime? PublishEndUtc { get; set; }
    public string HighlightsText { get; set; } = string.Empty;

    public IReadOnlyList<string> GetHighlights() => HighlightsText
        .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(value => value.Length <= 200)
        .Take(20)
        .ToList();
}

public sealed class EmployeeResourceWorkflowRequest
{
    public int ResourceId { get; set; }
    [MaxLength(500)] public string? Remarks { get; set; }
    public DateTime? PublishStartUtc { get; set; }
}

public sealed class EmployeeResourceDeleteRequest
{
    public int ResourceId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public sealed class EmployeeResourceDownload
{
    public int AttachmentId { get; init; }
    public int ResourceId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string StoredPath { get; init; } = string.Empty;
    public string MimeType { get; init; } = "application/octet-stream";
}

