using System.ComponentModel.DataAnnotations;

namespace Yakult.SystemsPortal.Models;

public sealed class CompanyFaqItem
{
    public int FaqId { get; init; }
    public bool IsDemo => FaqId < 0;
    public string Question { get; init; } = string.Empty;
    public string Answer { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class SaveCompanyFaqRequest
{
    public int? FaqId { get; init; }
    [Required, MaxLength(300)] public string Question { get; init; } = string.Empty;
    [Required] public string Answer { get; init; } = string.Empty;
    [MaxLength(100)] public string Category { get; init; } = "General";
    public int SortOrder { get; init; }
}

public sealed class FaqPageViewModel
{
    public IReadOnlyList<CompanyFaqItem> Items { get; init; } = Array.Empty<CompanyFaqItem>();
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public IReadOnlyList<CompanyPolicyItem> Policies { get; init; } = Array.Empty<CompanyPolicyItem>();
    public IReadOnlyList<string> PolicyCategories { get; init; } = Array.Empty<string>();
    public string ActiveTab { get; init; } = "faq";
    public bool IsDemoData { get; init; }
}

public sealed class CompanyPolicyItem
{
    public int PolicyId { get; init; }
    public bool IsDemo => PolicyId < 0;
    public string Title { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Category { get; init; } = "General";
    public DateTime? EffectiveDate { get; init; }
    public int SortOrder { get; init; }
    public bool IsPublished { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}

public sealed class SaveCompanyPolicyRequest
{
    public int? PolicyId { get; init; }
    [Required, MaxLength(180)] public string Title { get; init; } = string.Empty;
    [MaxLength(500)] public string Summary { get; init; } = string.Empty;
    [Required] public string Body { get; init; } = string.Empty;
    [MaxLength(100)] public string Category { get; init; } = "General";
    public DateTime? EffectiveDate { get; init; }
    public int SortOrder { get; init; }
}

public sealed class CompanyPolicyListViewModel
{
    public IReadOnlyList<CompanyPolicyItem> Items { get; init; } = Array.Empty<CompanyPolicyItem>();
    public IReadOnlyList<string> Categories { get; init; } = Array.Empty<string>();
    public bool IsDemoData { get; init; }
}

public sealed class CompanyInfoAdminViewModel
{
    public IReadOnlyList<CompanyFaqItem> Faqs { get; init; } = Array.Empty<CompanyFaqItem>();
    public IReadOnlyList<CompanyPolicyItem> Policies { get; init; } = Array.Empty<CompanyPolicyItem>();
    public bool CanPublish { get; init; }
}
