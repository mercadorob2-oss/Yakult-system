using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Yakult.SystemsPortal.Models;

// A versioned document in the existing BodyMarkdown field; legacy Markdown stays unchanged.
public sealed class LearningCourseDocument
{
    public const string Prefix = "yakult-course:v1\n";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { MaxDepth = 16 };
    private static readonly HashSet<string> BlockTypes = ["text", "image", "video", "callout", "steps", "accordion", "comparison", "quiz", "download"];
    public int Version { get; set; } = 1;
    public string Audience { get; set; } = "All employees";
    public string Level { get; set; } = "Foundation";
    public List<string> Objectives { get; set; } = [];
    public List<LearningModule> Modules { get; set; } = [];
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<LearningLesson> Lessons => Modules.SelectMany(x => x.Lessons);
    [System.Text.Json.Serialization.JsonIgnore]
    public int Minutes => Lessons.Sum(x => x.Minutes);

    public static LearningCourseDocument? Parse(string? body)
    {
        if (body?.StartsWith(Prefix, StringComparison.Ordinal) != true || body.Length > 1_000_000) return null;
        try
        {
            var document = JsonSerializer.Deserialize<LearningCourseDocument>(body[Prefix.Length..], JsonOptions);
            document?.Validate();
            return document;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or NullReferenceException or InvalidOperationException or OverflowException) { return null; }
    }

    public void Validate()
    {
        if (Version != 1 || Modules is null || Modules.Count is < 1 or > 100)
            throw new ArgumentException("A course needs between 1 and 100 modules.");
        CheckText(Audience, 200, "Audience");
        if (Level is not ("Foundation" or "Intermediate" or "Advanced")) throw new ArgumentException("Choose a valid course level.");
        if (Objectives is null || Objectives.Count > 20) throw new ArgumentException("Use up to 20 learning objectives.");
        foreach (var objective in Objectives) CheckText(objective, 500, "Learning objective");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var count = 0;
        foreach (var module in Modules)
        {
            CheckText(module.Title, 180, "Module title");
            if (module.Lessons is null || module.Lessons.Count is < 1 or > 100) throw new ArgumentException("Each module needs between 1 and 100 lessons.");
            foreach (var lesson in module.Lessons)
            {
                if (++count > 500) throw new ArgumentException("Use up to 500 lessons per course.");
                if (!Regex.IsMatch(lesson.Id ?? "", "^[a-zA-Z0-9_-]{1,64}$") || !ids.Add(lesson.Id!)) throw new ArgumentException("Lesson identifiers must be unique.");
                CheckText(lesson.Title, 180, "Lesson title");
                if (lesson.Minutes is < 1 or > 480) throw new ArgumentException("Lesson duration must be between 1 and 480 minutes.");
                if (lesson.Blocks is null || lesson.Blocks.Count is < 1 or > 100) throw new ArgumentException("Each lesson needs between 1 and 100 content blocks.");
                foreach (var block in lesson.Blocks)
                {
                    if (!BlockTypes.Contains(block.Type ?? "")) throw new ArgumentException("Choose a supported content block.");
                    if ((block.Title?.Length ?? 0) > 500 || (block.Text?.Length ?? 0) > 50_000 || (block.Extra?.Length ?? 0) > 50_000)
                        throw new ArgumentException("A content block exceeds its text limit.");
                    if (block.Type is "image" or "video" or "download")
                    {
                        if (!IsSafeUrl(block.Url)) throw new ArgumentException("Block media needs an HTTPS URL or a /media/ path.");
                    }
                    if (block.Type == "quiz")
                    {
                        CheckText(block.Title, 500, "Question");
                        if (block.Options is null || block.Options.Count is < 2 or > 6 || block.Answer < 0 || block.Answer >= block.Options.Count)
                            throw new ArgumentException("A knowledge check needs 2 to 6 choices and a correct answer.");
                        foreach (var option in block.Options) CheckText(option, 500, "Answer choice");
                    }
                }
            }
        }
    }

    private static void CheckText(string? value, int maximum, string label)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maximum) throw new ArgumentException($"{label} is required (up to {maximum} characters).");
    }

    public static bool IsSafeUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2000 || value.Any(char.IsControl) || value.Contains('\\')) return false;
        if (value.StartsWith("/media/", StringComparison.Ordinal) && !Uri.UnescapeDataString(value).Contains("..")) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo);
    }

    public static string? YouTubeEmbed(string? value)
    {
        if (!IsSafeUrl(value) || !Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        string? id = null;
        if (uri.Host is "youtu.be") id = uri.AbsolutePath.Trim('/').Split('/')[0];
        if (uri.Host is "youtube.com" or "www.youtube.com" or "youtube-nocookie.com" or "www.youtube-nocookie.com")
            id = uri.AbsolutePath.StartsWith("/embed/") ? uri.AbsolutePath[7..].Split('/')[0]
                : Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query).TryGetValue("v", out var videoId) ? videoId.ToString() : null;
        return id != null && Regex.IsMatch(id, "^[a-zA-Z0-9_-]{11}$") ? "https://www.youtube-nocookie.com/embed/" + id : null;
    }

    public static string BrowserKey(PortalContentItem item, string? user)
    {
        return $"{BrowserScope(user, item.IsDemo)}{item.ContentId}.{Hash(item.Body)}";
    }
    public static string BrowserScope(string? user, bool demo = false) => $"yakult.learning.v1.{Hash(user ?? "guest")}.{(demo ? "demo." : "")}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];
}

public sealed class LearningModule
{
    public string Title { get; set; } = "";
    public List<LearningLesson> Lessons { get; set; } = [];
}

public sealed class LearningLesson
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public int Minutes { get; set; } = 5;
    public List<LearningBlock> Blocks { get; set; } = [];
}

public sealed class LearningBlock
{
    public string Type { get; set; } = "text";
    public string Title { get; set; } = "";
    public string Text { get; set; } = "";
    public string Extra { get; set; } = "";
    public string Url { get; set; } = "";
    public List<string> Options { get; set; } = [];
    public int Answer { get; set; }
}
