using System.ServiceModel.Syndication;
using System.Xml;
using FeedMe.Core.Models;

namespace FeedMe.Core.Services;

public interface IFeedFetcher
{
    Task<FetchedFeed> FetchAsync(string feedUrl, CancellationToken ct = default);
}

/// <summary>Downloads and parses RSS/Atom feeds using <see cref="SyndicationFeed"/>.</summary>
public sealed class FeedFetcher : IFeedFetcher
{
    private readonly HttpClient _http;

    public FeedFetcher(HttpClient http) => _http = http;

    public async Task<FetchedFeed> FetchAsync(string feedUrl, CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(feedUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });

        var feed = SyndicationFeed.Load(reader)
            ?? throw new InvalidOperationException($"No valid RSS/Atom feed found at {feedUrl}.");

        var items = feed.Items
            .Select(MapItem)
            .Where(i => i is not null)
            .Select(i => i!)
            .ToList();

        return new FetchedFeed
        {
            Title = Coalesce(feed.Title?.Text, feedUrl),
            SiteUrl = PickLink(feed.Links),
            Items = items
        };
    }

    private static FetchedItem? MapItem(SyndicationItem item)
    {
        var link = PickLink(item.Links);
        var externalId = !string.IsNullOrWhiteSpace(item.Id) ? item.Id : link;
        if (string.IsNullOrWhiteSpace(externalId))
            return null;

        var summaryHtml = item.Summary?.Text;
        var contentHtml = ReadContentEncoded(item)
            ?? (item.Content as TextSyndicationContent)?.Text
            ?? summaryHtml;

        return new FetchedItem
        {
            ExternalId = externalId,
            Title = Coalesce(item.Title?.Text, "(untitled)"),
            Author = item.Authors.FirstOrDefault()?.Name,
            Summary = HtmlText.ToPlainText(summaryHtml ?? contentHtml),
            Content = contentHtml,
            Link = link,
            PublishedUtc = PickDate(item)
        };
    }

    /// <summary>Reads the RSS <c>content:encoded</c> element when present (fuller article HTML).</summary>
    private static string? ReadContentEncoded(SyndicationItem item)
    {
        try
        {
            return item.ElementExtensions
                .ReadElementExtensions<string>("encoded", "http://purl.org/rss/1.0/modules/content/")
                .FirstOrDefault();
        }
        catch
        {
            // Some feeds embed markup that won't deserialize to a string; fall back to other content.
            return null;
        }
    }

    private static string? PickLink(IEnumerable<SyndicationLink> links)
    {
        var list = links.ToList();
        var preferred = list.FirstOrDefault(l => l.RelationshipType is null or "alternate") ?? list.FirstOrDefault();
        return preferred?.Uri?.ToString();
    }

    private static DateTimeOffset? PickDate(SyndicationItem item)
    {
        if (item.PublishDate != default)
            return item.PublishDate;
        if (item.LastUpdatedTime != default)
            return item.LastUpdatedTime;
        return null;
    }

    private static string Coalesce(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value;
}
