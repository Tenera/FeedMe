using System.Xml.Linq;

namespace FeedMe.Core.Services;

/// <summary>A feed entry parsed from OPML: its URL and the category folder it lived under.</summary>
public sealed record OpmlFeed(string Url, string? Category);

/// <summary>Extracts feeds (and their categories) from an OPML subscription file.</summary>
public static class OpmlParser
{
    public static IReadOnlyList<OpmlFeed> ExtractFeeds(Stream opml) => Extract(XDocument.Load(opml));

    public static IReadOnlyList<OpmlFeed> ExtractFeeds(string opml) => Extract(XDocument.Parse(opml));

    private static IReadOnlyList<OpmlFeed> Extract(XDocument document)
    {
        var feeds = new List<OpmlFeed>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var outline in document.Descendants("outline"))
        {
            var url = ((string?)outline.Attribute("xmlUrl") ?? (string?)outline.Attribute("xmlURL"))?.Trim();
            if (string.IsNullOrWhiteSpace(url) || !seen.Add(url))
                continue;

            feeds.Add(new OpmlFeed(url, CategoryOf(outline)));
        }

        return feeds;
    }

    /// <summary>The category is the title/text of the containing folder outline, if any.</summary>
    private static string? CategoryOf(XElement outline)
    {
        if (outline.Parent is not { Name.LocalName: "outline" } parent)
            return null;

        var category = ((string?)parent.Attribute("title") ?? (string?)parent.Attribute("text"))?.Trim();
        return string.IsNullOrWhiteSpace(category) ? null : category;
    }
}
