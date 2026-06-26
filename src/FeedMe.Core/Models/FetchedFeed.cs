namespace FeedMe.Core.Models;

/// <summary>The result of fetching and parsing a feed, before it is persisted.</summary>
public sealed class FetchedFeed
{
    public required string Title { get; init; }
    public string? SiteUrl { get; init; }
    public required IReadOnlyList<FetchedItem> Items { get; init; }
}

/// <summary>A parsed entry from a fetched feed, before it is persisted.</summary>
public sealed class FetchedItem
{
    public required string ExternalId { get; init; }
    public required string Title { get; init; }
    public string? Author { get; init; }
    public string? Summary { get; init; }
    public string? Content { get; init; }
    public string? Link { get; init; }
    public DateTimeOffset? PublishedUtc { get; init; }
}
