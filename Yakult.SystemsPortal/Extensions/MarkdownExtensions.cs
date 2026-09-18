using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Yakult.SystemsPortal.Extensions;

public static class MarkdownExtensions
{
    private static readonly Regex LinkPattern = new(@"\[([^\]]+)\]\(([^\)\s]+)\)", RegexOptions.Compiled);
    private static readonly Regex BoldPattern = new(@"\*\*([^*]+)\*\*", RegexOptions.Compiled);
    private static readonly Regex OrderedListPattern = new(@"^\d+\.\s+", RegexOptions.Compiled);

    public static IHtmlContent SafeMarkdown(this IHtmlHelper html, string? markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return HtmlString.Empty;

        var output = new StringBuilder();
        var paragraph = new List<string>();
        var inUnorderedList = false;
        var inOrderedList = false;

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(output, paragraph);
                CloseLists(output, ref inUnorderedList, ref inOrderedList);
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("### ", StringComparison.Ordinal))
            {
                FlushParagraph(output, paragraph);
                CloseLists(output, ref inUnorderedList, ref inOrderedList);
                output.Append("<h3>").Append(RenderInline(trimmed[4..])).AppendLine("</h3>");
                continue;
            }

            if (trimmed.StartsWith("## ", StringComparison.Ordinal))
            {
                FlushParagraph(output, paragraph);
                CloseLists(output, ref inUnorderedList, ref inOrderedList);
                output.Append("<h2>").Append(RenderInline(trimmed[3..])).AppendLine("</h2>");
                continue;
            }

            if (trimmed.StartsWith("# ", StringComparison.Ordinal))
            {
                FlushParagraph(output, paragraph);
                CloseLists(output, ref inUnorderedList, ref inOrderedList);
                output.Append("<h2>").Append(RenderInline(trimmed[2..])).AppendLine("</h2>");
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                FlushParagraph(output, paragraph);
                if (inOrderedList) { output.AppendLine("</ol>"); inOrderedList = false; }
                if (!inUnorderedList) { output.AppendLine("<ul>"); inUnorderedList = true; }
                output.Append("<li>").Append(RenderInline(trimmed[2..])).AppendLine("</li>");
                continue;
            }

            if (OrderedListPattern.IsMatch(trimmed))
            {
                FlushParagraph(output, paragraph);
                if (inUnorderedList) { output.AppendLine("</ul>"); inUnorderedList = false; }
                if (!inOrderedList) { output.AppendLine("<ol>"); inOrderedList = true; }
                output.Append("<li>").Append(RenderInline(OrderedListPattern.Replace(trimmed, string.Empty, 1))).AppendLine("</li>");
                continue;
            }

            CloseLists(output, ref inUnorderedList, ref inOrderedList);
            paragraph.Add(trimmed);
        }

        FlushParagraph(output, paragraph);
        CloseLists(output, ref inUnorderedList, ref inOrderedList);
        return new HtmlString(output.ToString());
    }

    private static void FlushParagraph(StringBuilder output, List<string> paragraph)
    {
        if (paragraph.Count == 0) return;
        output.Append("<p>").Append(RenderInline(string.Join(" ", paragraph))).AppendLine("</p>");
        paragraph.Clear();
    }

    private static void CloseLists(StringBuilder output, ref bool inUnorderedList, ref bool inOrderedList)
    {
        if (inUnorderedList) { output.AppendLine("</ul>"); inUnorderedList = false; }
        if (inOrderedList) { output.AppendLine("</ol>"); inOrderedList = false; }
    }

    private static string RenderInline(string value)
    {
        var output = new StringBuilder();
        var current = 0;
        foreach (Match match in LinkPattern.Matches(value))
        {
            output.Append(ApplyBold(WebUtility.HtmlEncode(value[current..match.Index])));
            var label = ApplyBold(WebUtility.HtmlEncode(match.Groups[1].Value));
            var url = match.Groups[2].Value.Trim();
            if (IsSafeUrl(url))
            {
                output.Append("<a href=\"")
                    .Append(WebUtility.HtmlEncode(url))
                    .Append("\" target=\"_blank\" rel=\"noopener noreferrer\">")
                    .Append(label)
                    .Append("</a>");
            }
            else
            {
                output.Append(label);
            }
            current = match.Index + match.Length;
        }

        output.Append(ApplyBold(WebUtility.HtmlEncode(value[current..])));
        return output.ToString();
    }

    private static string ApplyBold(string encoded) => BoldPattern.Replace(encoded, "<strong>$1</strong>");

    private static bool IsSafeUrl(string value)
    {
        if (value.StartsWith("/media/", StringComparison.OrdinalIgnoreCase) && !value.Contains("..", StringComparison.Ordinal) && !value.Contains('\\')) return true;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps;
    }
}