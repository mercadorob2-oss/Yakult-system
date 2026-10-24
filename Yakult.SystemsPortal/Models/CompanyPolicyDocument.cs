using System.Text.Json;

namespace Yakult.SystemsPortal.Models;

/// <summary>Versioned structured content stored in the existing CompanyPolicy Body field.</summary>
public sealed class CompanyPolicyDocument
{
    public const string Prefix = "yakult-policy:v1:";
    private const int MaxBodyLength = 1_000_000;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public int Version { get; set; } = 1;
    public string Accent { get; set; } = "policy";
    public List<CompanyPolicySection> Sections { get; set; } = [];

    public static CompanyPolicyDocument? Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body) || !body.StartsWith(Prefix, StringComparison.Ordinal)) return null;
        try
        {
            var document = JsonSerializer.Deserialize<CompanyPolicyDocument>(body[Prefix.Length..], JsonOptions);
            return document?.Version == 1 ? document : null;
        }
        catch (JsonException) { return null; }
    }

    public static CompanyPolicyDocument Empty() => new()
    {
        Sections = [new CompanyPolicySection { Id = "section-1", Title = "Policy overview", Intro = "", Blocks = [new CompanyPolicyBlock { Type = "text", Title = "Guidance", Text = "" }] }]
    };

    public string ToBody()
    {
        Validate();
        var json = JsonSerializer.Serialize(this, JsonOptions);
        var body = Prefix + json;
        if (body.Length > MaxBodyLength) throw new ArgumentException("The structured policy is too large. Reduce its text or remove unused blocks.");
        return body;
    }

    public void Validate()
    {
        if (Version != 1) throw new ArgumentException("This policy uses an unsupported content format.");
        if (Accent is not ("policy" or "team")) throw new ArgumentException("Choose a supported policy appearance.");
        if (Sections is null || Sections.Count is < 1 or > 40) throw new ArgumentException("Add between 1 and 40 policy sections.");
        foreach (var section in Sections)
        {
            if (section is null) throw new ArgumentException("Remove the empty policy section and add it again.");
            Required(section.Id, 100, "Section identifier");
            if (!section.Id.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_')) throw new ArgumentException("Section identifiers can use letters, numbers, hyphens, and underscores.");
            Required(section.Title, 180, "Section title");
            Max(section.Intro, 2_000, "Section introduction");
            if (section.Blocks is null || section.Blocks.Count is < 1 or > 40) throw new ArgumentException($"Section '{section.Title}' needs 1 to 40 content blocks.");
            foreach (var block in section.Blocks) ValidateBlock(block, section.Title);
        }
    }

    private static void ValidateBlock(CompanyPolicyBlock block, string sectionTitle)
    {
        if (block is null) throw new ArgumentException($"Section '{sectionTitle}' contains an empty block.");
        if (!new[] { "text", "bullets", "steps", "process", "cards", "flowchart" }.Contains(block.Type, StringComparer.Ordinal))
            throw new ArgumentException($"Section '{sectionTitle}' contains an unsupported block type.");
        Max(block.Title, 180, "Block title");
        Max(block.Text, 30_000, "Block text");
        switch (block.Type)
        {
            case "text":
                Required(block.Text, 30_000, "Text block");
                break;
            case "bullets":
            case "steps":
                ValidateStrings(block.Items, 60, 2_000, "List item", sectionTitle);
                break;
            case "process":
                if (block.Stages is null || block.Stages.Count is < 1 or > 20) throw new ArgumentException($"Add 1 to 20 stages to the process in '{sectionTitle}'.");
                foreach (var stage in block.Stages)
                {
                    if (stage is null) throw new ArgumentException($"Remove the empty process stage in '{sectionTitle}'.");
                    Required(stage.Owner, 120, "Process owner");
                    Required(stage.Title, 180, "Process stage title");
                    ValidateStrings(stage.Points, 30, 2_000, "Process detail", sectionTitle);
                }
                break;
            case "cards":
                if (block.Cards is null || block.Cards.Count is < 1 or > 30) throw new ArgumentException($"Add 1 to 30 cards to the role cards block in '{sectionTitle}'.");
                foreach (var card in block.Cards)
                {
                    if (card is null) throw new ArgumentException($"Remove the empty role card in '{sectionTitle}'.");
                    Required(card.Label, 100, "Card label");
                    Required(card.Title, 180, "Card title");
                    Required(card.Text, 3_000, "Card description");
                }
                break;
            case "flowchart":
                if (!IsPolicyImageUrl(block.ImageUrl)) throw new ArgumentException($"Choose a valid uploaded diagram for '{sectionTitle}'.");
                Required(block.AltText, 300, "Diagram alt text");
                ValidateStrings(block.Paths, 30, 3_000, "Diagram path description", sectionTitle);
                Max(block.SourceNote, 2_000, "Diagram source note");
                break;
        }
    }

    public static bool IsPolicyImageUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("/media/company-policies/", StringComparison.Ordinal)) return false;
        var name = value["/media/company-policies/".Length..];
        return name.Length is > 0 and <= 120 && name.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '.' or '-' or '_') &&
               !name.Contains("..", StringComparison.Ordinal) &&
               new[] { ".png", ".jpg", ".jpeg", ".webp" }.Contains(Path.GetExtension(name), StringComparer.OrdinalIgnoreCase);
    }

    private static void ValidateStrings(IReadOnlyCollection<string>? values, int maxCount, int maxLength, string label, string sectionTitle)
    {
        if (values is null || values.Count is < 1 || values.Count > maxCount) throw new ArgumentException($"Add 1 to {maxCount} {label.ToLowerInvariant()}s in '{sectionTitle}'.");
        foreach (var value in values) Required(value, maxLength, label);
    }

    private static void Required(string? value, int maxLength, string label)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{label} is required.");
        Max(value, maxLength, label);
    }

    private static void Max(string? value, int maxLength, string label)
    {
        if ((value?.Length ?? 0) > maxLength) throw new ArgumentException($"{label} must be {maxLength} characters or fewer.");
    }
}

public sealed class CompanyPolicySection
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Intro { get; set; } = "";
    public List<CompanyPolicyBlock> Blocks { get; set; } = [];
}

public sealed class CompanyPolicyBlock
{
    public string Type { get; set; } = "text";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public List<string> Items { get; set; } = [];
    public List<CompanyPolicyProcessStage> Stages { get; set; } = [];
    public List<CompanyPolicyCard> Cards { get; set; } = [];
    public string ImageUrl { get; set; } = "";
    public string AltText { get; set; } = "";
    public List<string> Paths { get; set; } = [];
    public string SourceNote { get; set; } = "";
}

public sealed class CompanyPolicyProcessStage
{
    public string Owner { get; set; } = "";
    public string Title { get; set; } = "";
    public List<string> Points { get; set; } = [];
}

public sealed class CompanyPolicyCard
{
    public string Label { get; set; } = "";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
}

public sealed class CompanyPolicyStructuredViewModel
{
    public required CompanyPolicyItem Policy { get; init; }
    public required CompanyPolicyDocument Document { get; init; }
}
