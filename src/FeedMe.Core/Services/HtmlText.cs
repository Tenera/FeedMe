using System.Net;
using System.Text.RegularExpressions;

namespace FeedMe.Core.Services;

/// <summary>Helpers for turning feed HTML summaries into clean plain-text snippets.</summary>
public static partial class HtmlText
{
    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"<\s*br\s*/?\s*>|</\s*(?:p|div|li|h[1-6]|blockquote|tr|ul|ol|section|article|header|footer)\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex BlockBreakRegex();

    /// <summary>Strips HTML tags, decodes entities and collapses whitespace into one line. Returns null if empty.</summary>
    public static string? ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var noTags = TagRegex().Replace(html, " ");
        var decoded = WebUtility.HtmlDecode(noTags);
        var collapsed = WhitespaceRegex().Replace(decoded, " ").Trim();
        return collapsed.Length == 0 ? null : collapsed;
    }

    /// <summary>
    /// Converts HTML into readable text for the reading pane: block-level tags become
    /// paragraph breaks, remaining tags are stripped and entities decoded. Returns null if empty.
    /// </summary>
    public static string? ToReadableText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
            return null;

        var withBreaks = BlockBreakRegex().Replace(html, "\n");
        var stripped = TagRegex().Replace(withBreaks, string.Empty);
        var decoded = WebUtility.HtmlDecode(stripped).Replace("\r", string.Empty);

        var paragraphs = decoded
            .Split('\n')
            .Select(line => WhitespaceRegex().Replace(line, " ").Trim())
            .Where(line => line.Length > 0);

        var result = string.Join("\n\n", paragraphs);
        return result.Length == 0 ? null : result;
    }
}
