namespace Yakult.SystemsPortal.Models;

/// <summary>Prepared policy content with diagrams and screenshots. Loading it never writes to the database.</summary>
public static class ItPolicyContent
{
    public const string Slug = "it-policy---employee-guidelines";
    public const string PendingTitle = "[Confirm title:";

    public static bool TryLoad(string contentRoot, out CompanyPolicyItem? item)
    {
        try { item = Load(contentRoot); return true; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { item = null; return false; }
    }

    public static CompanyPolicyItem Load(string contentRoot)
    {
        var json = File.ReadAllText(Path.Combine(contentRoot, "App_Data", "PolicyDrafts", "it-policy.json"));
        var document = CompanyPolicyDocument.Parse(CompanyPolicyDocument.Prefix + json)
            ?? throw new InvalidOperationException("Prepared IT policy content could not be read.");
        document.Validate();
        return new CompanyPolicyItem {
            Title = "IT Policy & Employee Guidelines", Slug = Slug, Category = "IT Policy",
            Summary = "Computer user credentials; equipment and ink/toner requests; employee and guest internet access; software installation and access; hardware and software support.",
            Body = document.ToBody(), IsPublished = false
        };
    }
}
