using FeedMe.Core.Models;
using FeedMe.Core.Services;

namespace FeedMe.Tests;

/// <summary>An in-memory <see cref="IFeedFetcher"/> for tests: canned feeds and simulated failures.</summary>
internal sealed class FakeFeedFetcher : IFeedFetcher
{
    private readonly Dictionary<string, FetchedFeed> _feeds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failing = new(StringComparer.OrdinalIgnoreCase);

    public FakeFeedFetcher Add(string url, FetchedFeed feed)
    {
        _feeds[url] = feed;
        return this;
    }

    public FakeFeedFetcher Fail(string url)
    {
        _failing.Add(url);
        return this;
    }

    public Task<FetchedFeed> FetchAsync(string feedUrl, CancellationToken ct = default)
    {
        if (_failing.Contains(feedUrl))
            throw new InvalidOperationException($"Simulated fetch failure for {feedUrl}");

        return Task.FromResult(_feeds.TryGetValue(feedUrl, out var feed)
            ? feed
            : new FetchedFeed { Title = feedUrl, SiteUrl = null, Items = Array.Empty<FetchedItem>() });
    }
}

internal static class Sample
{
    public static FetchedFeed Feed(string title, params FetchedItem[] items)
        => new() { Title = title, SiteUrl = "https://site.example", Items = items };

    public static FetchedItem Item(string externalId, string? title = null)
        => new()
        {
            ExternalId = externalId,
            Title = title ?? $"Item {externalId}",
            Summary = "summary",
            Content = "<p>content</p>",
            Link = $"https://site.example/{externalId}",
            PublishedUtc = DateTimeOffset.UtcNow
        };
}
