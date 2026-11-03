using System.Buffers.Binary;

namespace Yakult.SystemsPortal.Models;

/// <summary>Presentation decisions only; the saved policy is never rewritten.</summary>
public static class CompanyPolicyPresentation
{
    public static List<CompanyPolicyBlock> PairedScreenshots(CompanyPolicySection section, CompanyPolicyBlock steps)
    {
        if (section.Id != "guest-internet" || steps.Type != "steps") return [];
        var start = section.Blocks.IndexOf(steps) + 1;
        var screenshots = section.Blocks.Skip(start).Take(steps.Items.Count).ToList();
        return screenshots.Count == steps.Items.Count && screenshots.Select((block, i) =>
            block.Type == "flowchart" && block.Title.StartsWith($"Screenshot {i + 1} |", StringComparison.Ordinal)).All(x => x)
            ? screenshots : [];
    }

    public static bool IsCallout(CompanyPolicyBlock block) =>
        block.Title.StartsWith("Important", StringComparison.OrdinalIgnoreCase) || block.Title == "Implementation note";

    public static (int Width, int Height) ImageSize(string root, string url)
    {
        if (!CompanyPolicyDocument.IsPolicyImageUrl(url)) return (1200, 900);
        try
        {
            using var file = File.OpenRead(Path.Combine(root, "App_Data", "CompanyPolicyMedia", Path.GetFileName(url)));
            Span<byte> header = stackalloc byte[24];
            file.ReadExactly(header);
            if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            {
                var width = BinaryPrimitives.ReadInt32BigEndian(header[16..20]);
                var height = BinaryPrimitives.ReadInt32BigEndian(header[20..24]);
                if (width > 0 && height > 0) return (width, height);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return (1200, 900);
    }
}

public sealed record CompanyPolicyImageViewModel(CompanyPolicyBlock Block, string? PairedInstruction = null);
